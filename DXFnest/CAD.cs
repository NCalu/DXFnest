using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Runtime.ConstrainedExecution;

namespace DXFnest
{
    public class CAD
    {
        internal const float tol0 = 1E-4f;
        internal const float tolCross = 1f;

        #region feature
        public class Feature
        {
            public string Tag = "";

            public int PartSequenceIndex = -1;

            public int Depth = -1;
            public int Order = -1;

            public Vector3 Min;
            public Vector3 Max;

            public enum FeatureType
            {
                NONE,

                DXF_OPEN_ENTITY,
                DXF_CLOSED_ENTITY,
                DXF_IGNORE,

                EXT,
                INT,
                OPEN,
            }

            public FeatureType Type = FeatureType.NONE;
            public TriMesh Mesh = new TriMesh();

            public Feature()
            {

            }
            public Feature(string tag, FeatureType type)
            {
                Tag = tag;
                Type = type;
            }
            public Feature(TriMesh mesh)
            {
                Mesh = mesh;
            }
        }
        #endregion

        #region 2d
        public static List<List<Feature>> ExtractParts(List<Feature> features, double mergeDist, double linkDist, bool mergeLayers, double minIntArea, bool addNoParts = false)
        {
            List<List<Feature>> parts = new List<List<Feature>>();
            List<Feature> open_ctrs = new List<Feature>();

            if (features.Count == 0) return parts;

            var groups = features.GroupBy(f => f.Tag);

            if (mergeLayers) groups = new[] { features.GroupBy(_ => "ALL").First() };

            foreach (var group in groups)
            {
                // *******************************************************************************************
                // CONNECT
                // *******************************************************************************************

                HashSet<Edge> used = new HashSet<Edge>();
                List<Feature> closed_ctrs = new List<Feature>();

                foreach (Feature f in group)
                {
                    if (f.Type == Feature.FeatureType.DXF_CLOSED_ENTITY)
                    {
                        f.Type = Feature.FeatureType.DXF_CLOSED_ENTITY;
                        closed_ctrs.Add(f);
                        foreach (Edge seg in f.Mesh.Edges)
                        {
                            used.Add(seg);
                        }
                    }
                    else if (f.Type == Feature.FeatureType.DXF_IGNORE)
                    {
                        f.Type = Feature.FeatureType.DXF_OPEN_ENTITY;
                        open_ctrs.Add(f);
                        foreach (Edge seg in f.Mesh.Edges)
                        {
                            used.Add(seg);
                        }
                    }
                }

                List<Edge> edges = group.SelectMany(f => f.Mesh.Edges).ToList();

                for (int i = 0; i < edges.Count; i++)
                //foreach (Edge seg in edges)
                {
                    if ((edges[i].V0 - edges[i].V1).Length < mergeDist)
                    {
                        used.Add(edges[i]);
                    }

                    if (used.Contains(edges[i]))
                    {
                        continue;
                    }

                    List<Edge> contour = new List<Edge> { edges[i] };
                    used.Add(edges[i]);

                    Vector3 currentEnd = edges[i].V1;
                    bool closed = false;

                    while (!closed)
                    {

                        Edge next = edges
                            .Where(s => !used.Contains(s))
                            .OrderBy(s => Math.Min((s.V0 - currentEnd).Length, (s.V1 - currentEnd).Length))
                            .FirstOrDefault();

                        if (next == null)
                        {
                            break;
                        }

                        double distToV0 = (next.V0 - currentEnd).Length;
                        double distToV1 = (next.V1 - currentEnd).Length;
                        double minDist = Math.Min(distToV0, distToV1);

                        if (minDist > linkDist)
                        {
                            break;
                        }

                        if (distToV1 < distToV0)
                        {
                            Vector3 tmp = next.V0;
                            next.V0 = next.V1;
                            next.V1 = tmp;
                            minDist = distToV1;
                        }

                        if (minDist > mergeDist)
                        {
                            Edge bridge = new Edge { V0 = currentEnd, V1 = next.V0 };
                            contour.Add(bridge);
                        }

                        contour.Add(next);
                        used.Add(next);
                        currentEnd = next.V1;

                        if ((currentEnd - contour[0].V0).Length < mergeDist)
                        {
                            closed = true;
                            break;
                        }
                    }

                    if (!closed)
                    {
                        double endGap = (currentEnd - contour[0].V0).Length;
                        if (endGap <= linkDist)
                        {
                            if (endGap > mergeDist)
                            {
                                Edge closingBridge = new Edge { V0 = currentEnd, V1 = contour[0].V0 };
                                contour.Add(closingBridge);
                            }
                            closed = true;
                        }
                    }

                    if (closed)
                    {
                        Feature fclosed = new Feature();
                        fclosed.Type = Feature.FeatureType.DXF_CLOSED_ENTITY;
                        fclosed.Tag = group.First().Tag;
                        fclosed.Mesh.Edges = contour;
                        closed_ctrs.Add(fclosed);
                    }
                    else
                    {
                        Feature fopen = new Feature();
                        fopen.Type = Feature.FeatureType.DXF_OPEN_ENTITY;
                        fopen.Tag = group.First().Tag;
                        fopen.Mesh.Edges = contour;
                        open_ctrs.Add(fopen);
                    }
                }

                // *******************************************************************************************
                // INT & EXT
                // *******************************************************************************************

                closed_ctrs = closed_ctrs.OrderByDescending(ctr => GetArea(ctr.Mesh.Edges)).ToList();

                int n = closed_ctrs.Count;
                int[] depth = new int[n];
                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        if (i == j)
                        {
                            continue;
                        }

                        bool inside = true;

                        foreach (Edge edg in closed_ctrs[i].Mesh.Edges)
                        {
                            if (!PointInPolygon(edg.V1, closed_ctrs[j].Mesh.Edges))
                            {
                                inside = false;
                                break;
                            }
                        }

                        if (inside)
                        {
                            depth[i]++;
                        }
                    }
                }

                for (int i = 0; i < n; i++)
                {
                    if (depth[i] % 2 == 0)
                    {
                        List<Feature> part = new List<Feature>();

                        Feature fext = new Feature();
                        fext.Tag = group.First().Tag;
                        fext.Depth = depth[i];
                        fext.Type = Feature.FeatureType.EXT;
                        double signedArea = GetArea(closed_ctrs[i].Mesh.Edges, true);
                        if (Math.Abs(signedArea) < tol0)
                        {
                            fext.Depth = -1;
                            fext.Type = Feature.FeatureType.OPEN;
                            fext.Mesh.Edges.AddRange(closed_ctrs[i].Mesh.Edges);
                            part.Add(fext);
                        }
                        else
                        {
                            if (signedArea > 0)
                            {
                                fext.Mesh.Edges.AddRange(GetReversed(closed_ctrs[i].Mesh.Edges));
                            }
                            else
                            {
                                fext.Mesh.Edges.AddRange(closed_ctrs[i].Mesh.Edges);
                            }
                            part.Add(fext);

                            for (int j = 0; j < n; j++)
                            {
                                if (depth[j] == depth[i] + 1)
                                {
                                    Vector3 testPt = closed_ctrs[j].Mesh.Edges.First().V0;

                                    if (PointInPolygon(testPt, closed_ctrs[i].Mesh.Edges))
                                    {
                                        Feature fint = new Feature();
                                        fint.Tag = group.First().Tag;
                                        fint.Depth = depth[i] + 1;
                                        fint.Type = Feature.FeatureType.INT;
                                        if (GetArea(closed_ctrs[j].Mesh.Edges, true) < 0)
                                        {
                                            fint.Mesh.Edges.AddRange(GetReversed(closed_ctrs[j].Mesh.Edges));
                                        }
                                        else
                                        {
                                            fint.Mesh.Edges.AddRange(closed_ctrs[j].Mesh.Edges);
                                        }
                                        part.Add(fint);
                                    }
                                }
                            }
                        }

                        parts.Add(part);
                    }
                }
            }

            if (open_ctrs.Any())
            {
                if (parts.Count == 1) // ONLY 1 PART, ALL OPEN_CTRS GO THERE
                {
                    for (int j = 0; j < open_ctrs.Count(); j++)
                    {
                        Feature fopen = new Feature();
                        fopen.Tag = open_ctrs[j].Tag;
                        fopen.Depth = -1;
                        fopen.Type = Feature.FeatureType.OPEN;
                        fopen.Mesh.Edges.AddRange(open_ctrs[j].Mesh.Edges);

                        parts[0].Add(fopen);

                        open_ctrs.RemoveAt(j);
                        j--;
                    }
                }
                else
                {
                    foreach (List<Feature> part in parts)
                    {
                        List<Feature> toAdd = new List<Feature>();

                        foreach (Feature f in part)
                        {
                            if (f.Type == Feature.FeatureType.EXT)
                            {
                                for (int j = 0; j < open_ctrs.Count(); j++)
                                {
                                    if (open_ctrs[j].Mesh.Edges.Any())
                                    {
                                        for (int test = 0; test < 4; test++)
                                        {
                                            Vector3 testPt = open_ctrs[j].Mesh.Edges.First().V0;
                                            if (test == 1) testPt = open_ctrs[j].Mesh.Edges.Last().V1;
                                            if (test == 2) testPt = 0.5f * (open_ctrs[j].Mesh.Edges.First().V0 + open_ctrs[j].Mesh.Edges.First().V1);
                                            if (test == 3) testPt = 0.5f * (open_ctrs[j].Mesh.Edges.Last().V0 + open_ctrs[j].Mesh.Edges.Last().V1);

                                            if (PointInPolygon(testPt, f.Mesh.Edges))
                                            {
                                                Feature fopen = new Feature();
                                                fopen.Tag = open_ctrs[j].Tag;
                                                fopen.Depth = f.Depth + 1;
                                                fopen.Type = Feature.FeatureType.OPEN;
                                                fopen.Mesh.Edges.AddRange(open_ctrs[j].Mesh.Edges);

                                                toAdd.Add(fopen);

                                                open_ctrs.RemoveAt(j);
                                                j--;
                                                test = 999;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        part.AddRange(toAdd);
                    }
                }
            }

            if (open_ctrs.Any() && addNoParts)
            {
                List<Feature> part = new List<Feature>();
                foreach (Feature f in open_ctrs)
                {
                    Feature fopen = new Feature();
                    fopen.Tag = f.Tag;
                    fopen.Depth = -1;
                    fopen.Type = Feature.FeatureType.OPEN;
                    fopen.Mesh.Edges.AddRange(f.Mesh.Edges);
                    part.Add(fopen);
                }
                parts.Add(part);
            }

            for (int i = 0; i < parts.Count; i++)
            {
                foreach (Feature f in parts[i])
                {
                    f.PartSequenceIndex = i;
                }
            }

            // *******************************************************************************************
            // TRIANGULATIONS
            // *******************************************************************************************

            foreach (List<Feature> part in parts)
            {
                Feature fext = null;
                List<double> vertices = new List<double>();
                List<int> holeIds = new List<int>();

                foreach (Feature f in part)
                {
                    if (f.Type == Feature.FeatureType.EXT)
                    {
                        foreach (Edge edge in f.Mesh.Edges)
                        {
                            vertices.Add(edge.V0.X);
                            vertices.Add(edge.V0.Y);
                        }
                        fext = f;
                    }

                    if (f.Type == Feature.FeatureType.INT)
                    {
                        if (GetArea(f.Mesh.Edges) > minIntArea)
                        {
                            holeIds.Add(vertices.Count / 2);

                            foreach (Edge edge in f.Mesh.Edges)
                            {
                                vertices.Add(edge.V0.X);
                                vertices.Add(edge.V0.Y);
                            }
                        }
                    }
                }

                if (fext != null)
                {
                    var tessellation = Triangulation.Tessellate(vertices, holeIds);

                    for (int i = 0; i < tessellation.Count; i += 3)
                    {
                        int i0 = tessellation[i];
                        int i1 = tessellation[i + 1];
                        int i2 = tessellation[i + 2];

                        double v0x = vertices[2 * i0];
                        double v0y = vertices[2 * i0 + 1];

                        double v1x = vertices[2 * i1];
                        double v1y = vertices[2 * i1 + 1];

                        double v2x = vertices[2 * i2];
                        double v2y = vertices[2 * i2 + 1];

                        fext.Mesh.Triangles.Add(new Triangle(v0x, v0y, 0, v1x, v1y, 0, v2x, v2y, 0));
                    }
                }
            }

            //if (DateTime.Now.ToString("yyyy") != "2026") return new List<List<Feature>>();
            return parts;
        }       
        public static List<Feature> RotoTranslatePartXY(List<Feature> part, double x, double y, double r)
        {
            List<Feature> tPart = new List<Feature>();

            float dx = (float)x;
            float dy = (float)y;
            float xRot0;
            float yRot0;
            float xRot1;
            float yRot1;
            float xRot2;
            float yRot2;
            float cos = (float)Math.Cos(r);
            float sin = (float)Math.Sin(r);

            foreach (Feature f in part)
            {
                Feature tFeature = new Feature();
                tFeature.PartSequenceIndex = f.PartSequenceIndex;
                tFeature.Depth = f.Depth;
                tFeature.Order = f.Order;
                tFeature.Tag = f.Tag;
                tFeature.Type = f.Type;

                foreach (Edge edge in f.Mesh.Edges)
                {
                    xRot0 = edge.V0.X * cos - edge.V0.Y * sin + dx;
                    yRot0 = edge.V0.X * sin + edge.V0.Y * cos + dy;
                    xRot1 = edge.V1.X * cos - edge.V1.Y * sin + dx;
                    yRot1 = edge.V1.X * sin + edge.V1.Y * cos + dy;
                    tFeature.Mesh.Edges.Add(new Edge(xRot0, yRot0, 0, xRot1, yRot1, 0, edge.R, edge.NS));
                }
                foreach (Triangle tri in f.Mesh.Triangles)
                {
                    xRot0 = tri.V0.X * cos - tri.V0.Y * sin + dx;
                    yRot0 = tri.V0.X * sin + tri.V0.Y * cos + dy;
                    xRot1 = tri.V1.X * cos - tri.V1.Y * sin + dx;
                    yRot1 = tri.V1.X * sin + tri.V1.Y * cos + dy;
                    xRot2 = tri.V2.X * cos - tri.V2.Y * sin + dx;
                    yRot2 = tri.V2.X * sin + tri.V2.Y * cos + dy;
                    tFeature.Mesh.Triangles.Add(new Triangle(xRot0, yRot0, 0, xRot1, yRot1, 0, xRot2, yRot2, 0));
                }

                tPart.Add(tFeature);
            }

            return tPart;
        }       
        public static double GetRotMinHeight(List<Edge> edges)
        {
            if (edges == null || !edges.Any()) return 0;

            List<Vector2> points = MATH.GetConvexHull(edges.SelectMany(e => new[] { new Vector2(e.V1.X, e.V1.Y) }).ToList());
            if (points.Count < 3) return 0;

            double bestAngle = 0;
            double bestDirL = double.MaxValue;

            for (int i = 0; i < points.Count; i++)
            {
                Vector2 p0 = points[i];
                Vector2 p1 = points[(i + 1) % points.Count];

                double dx = p1.X - p0.X;
                double dy = p1.Y - p0.Y;
                if (dx == 0 && dy == 0) continue;

                double angle = Math.Atan2(dy, dx);

                double cos = Math.Cos(-angle);
                double sin = Math.Sin(-angle);

                double minY = double.MaxValue;
                double maxY = double.MinValue;

                foreach (Vector2 v in points)
                {
                    double yRot = v.X * sin + v.Y * cos;
                    if (yRot < minY) minY = yRot;
                    if (yRot > maxY) maxY = yRot;
                }

                double height = maxY - minY;
                if (height < bestDirL && Math.Abs(height - bestDirL) > tol0)
                {
                    bestDirL = height;
                    bestAngle = angle;
                }
            }

            return -bestAngle;
        }
        public static List<Edge> GetBulgeSegmented(double bulge, double x0, double y0, double x1, double y1, double aStep, double maxStepL)
        {
            double chordL = Math.Sqrt(Math.Pow(x0 - x1, 2) + Math.Pow(y0 - y1, 2));
            double theta = 4.0 * Math.Atan(bulge);
            double r = Math.Abs(chordL / (2.0 * Math.Sin(theta / 2.0)));

            double xm = (x0 + x1) / 2.0;
            double ym = (y0 + y1) / 2.0;

            double xp = -(y1 - y0);
            double yp = (x1 - x0);
            double lp = Math.Sqrt(Math.Pow(xp, 2) + Math.Pow(yp, 2));
            xp = xp / lp;
            yp = yp / lp;

            double offset = Math.Sqrt(Math.Max(0, r * r - (chordL * chordL) / 4.0));

            if (Math.Abs(bulge) > 1.0)
            {
                offset *= -1;
            }

            double cx = xm + xp * offset * Math.Sign(bulge);
            double cy = ym + yp * offset * Math.Sign(bulge);

            double a0 = Math.Atan2(y0 - cy, x0 - cx);
            double a1 = Math.Atan2(y1 - cy, x1 - cx);

            if (bulge < 0)
            {
                if (a1 > a0)
                {
                    a1 -= 2.0 * Math.PI;
                }

                List<Edge> segs = GetArcSegments(cx, cy, r, a1, a0, aStep, maxStepL);
                segs.Reverse();
                float tmp;
                foreach (Edge edg in segs)
                {
                    tmp = edg.V0.X;
                    edg.V0.X = edg.V1.X;
                    edg.V1.X = tmp;

                    tmp = edg.V0.Y;
                    edg.V0.Y = edg.V1.Y;
                    edg.V1.Y = tmp;
                }
                return segs;
            }
            else
            {
                if (a0 > a1)
                {
                    a0 -= 2.0 * Math.PI;
                }

                return GetArcSegments(cx, cy, r, a0, a1, aStep, maxStepL);
            }
        }
        public static List<Edge> GetArcSegments(double cx, double cy, double r, double a0, double a1, double aStep, double maxStepL)
        {
            List<Edge> segs = new List<Edge>();

            double x0, y0, x1, y1;

            double da = a1 - a0;
            int ns = Math.Max(1, (int)Math.Ceiling(Math.Abs(da) / aStep));

            double totL = Math.Abs(da) * r;
            int nsl = (int)Math.Ceiling(totL / maxStepL);

            if (ns < nsl) { ns = nsl; }
            if (ns < 2) { ns = 2; } // =>2 TO DETECT ARC_CW/ARC_CCW 

            double step = da / ns;
            for (int i = 1; i <= ns; i++)
            {
                x0 = cx + r * Math.Cos(a0 + (i - 1) * step);
                y0 = cy + r * Math.Sin(a0 + (i - 1) * step);
                x1 = cx + r * Math.Cos(a0 + i * step);
                y1 = cy + r * Math.Sin(a0 + i * step);

                segs.Add(new Edge(x0, y0, 0, x1, y1, 0, r, ns));
            }

            return segs;
        }
        public static List<Edge> GetArcTangentSegments(double cx, double cy, double r, double a0, double a1, double aStep, double maxStepL)
        {
            List<Edge> segs = new List<Edge>();

            double x0, y0, x1, y1;

            double da = a1 - a0;
            int ns = Math.Max(1, (int)Math.Ceiling(Math.Abs(da) / aStep));

            double totL = Math.Abs(da) * r;
            int nsl = (int)Math.Ceiling(totL / maxStepL);

            if (ns < nsl) { ns = nsl; }
            ns++;

            double step = da / ns;
            double step0 = step / 2;

            double c = r * (1 - Math.Cos(Math.Abs(step / 2)));

            x0 = cx + r * Math.Cos(a0);
            y0 = cy + r * Math.Sin(a0);
            x1 = cx + (r + c) * Math.Cos(a0 + step0);
            y1 = cy + (r + c) * Math.Sin(a0 + step0);

            segs.Add(new Edge(x0, y0, 0, x1, y1, 0, 0, 0));

            for (int i = 1; i <= ns - 1; i++)
            {
                x0 = cx + (r + c) * Math.Cos(a0 + step0 + (i - 1) * step);
                y0 = cy + (r + c) * Math.Sin(a0 + step0 + (i - 1) * step);
                x1 = cx + (r + c) * Math.Cos(a0 + step0 + i * step);
                y1 = cy + (r + c) * Math.Sin(a0 + step0 + i * step);

                segs.Add(new Edge(x0, y0, 0, x1, y1, 0, 0, 0));
            }

            x0 = cx + (r + c) * Math.Cos(a1 - step0);
            y0 = cy + (r + c) * Math.Sin(a1 - step0);
            x1 = cx + r * Math.Cos(a1);
            y1 = cy + r * Math.Sin(a1);

            segs.Add(new Edge(x0, y0, 0, x1, y1, 0, 0, 0));

            return segs;
        }
        public static double GetArea(List<Edge> edges, bool signed = false)
        {
            if (edges == null || edges.Count == 0) return 0.0;

            List<Vector3> vertices = new List<Vector3>();
            vertices.Add(edges[0].V0);
            Vector3 current = edges[0].V1;

            while (vertices.Count < edges.Count)
            {
                vertices.Add(current);
                Edge next = edges.FirstOrDefault(e => e.V0.EqualsApprox(current));
                if (next == null) return 0.0;
                current = next.V1;
            }

            double area = 0.0;
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 p1 = vertices[i];
                Vector3 p2 = vertices[(i + 1) % vertices.Count];
                area += (p1.X * p2.Y) - (p2.X * p1.Y);
            }

            if (signed)
            {
                return area * 0.5;
            }
            else
            {
                return Math.Abs(area) * 0.5;
            }
        }
        internal static bool PointInPolygon(Vector3 pt, List<Edge> edges)
        {
            int n = edges.Count;
            if (n == 0) return false;

            var vertices = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                vertices[i] = edges[i].V0;
            }

            bool inside = false;
            double x = pt.X;
            double y = pt.Y;

            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double xi = vertices[i].X, yi = vertices[i].Y;
                double xj = vertices[j].X, yj = vertices[j].Y;

                if (PointOnSegment(x, y, xi, yi, xj, yj)) return false;

                if (((yi > y) != (yj > y)) &&
                    (x < (xj - xi) * (y - yi) / ((yj - yi) + double.Epsilon) + xi))
                {
                    inside = !inside;
                }
            }

            return inside;
        }
        internal static bool PointOnSegment(double px, double py, double x1, double y1, double x2, double y2)
        {
            double cross = (px - x1) * (y2 - y1) - (py - y1) * (x2 - x1);
            if (Math.Abs(cross) > tol0)
                return false;

            return (px >= Math.Min(x1, x2) - tol0 && px <= Math.Max(x1, x2) + tol0 &&
                    py >= Math.Min(y1, y2) - tol0 && py <= Math.Max(y1, y2) + tol0);
        }
        internal static List<Edge> GetReversed(List<Edge> edges)
        {
            var reversed = new List<Edge>(edges.Count);

            for (int i = edges.Count - 1; i >= 0; i--)
            {
                var e = edges[i];
                reversed.Add(new Edge
                {
                    V0 = e.V1,
                    V1 = e.V0,
                    R = e.R,
                    NS = e.NS,
                });
            }

            return reversed;
        }
        public static List<Edge> SimplifyForNest(List<Edge> edges, bool sheet, bool inner, double gap, double paveLimit, double aStep, double maxStepL, out double minHrot, out bool pave)
        {
            minHrot = 0;
            pave = false;

            double minX = double.MaxValue;
            double maxX = double.MinValue;
            double minY = double.MaxValue;
            double maxY = double.MinValue;

            for (int i = 0; i < edges.Count; i++)
            {
                minX = Math.Min(Math.Min(edges[i].V0.X, edges[i].V1.X), minX);
                maxX = Math.Max(Math.Max(edges[i].V0.X, edges[i].V1.X), maxX);
                minY = Math.Min(Math.Min(edges[i].V0.Y, edges[i].V1.Y), minY);
                maxY = Math.Max(Math.Max(edges[i].V0.Y, edges[i].V1.Y), maxY);
            }

            if (paveLimit > 0 && !inner)
            {
                if (GetArea(edges, false) / ((maxX - minX) * (maxY - minY)) > paveLimit)
                {
                    pave = true;

                    if (maxY - minY > maxX - minX)
                    {
                        minHrot = 90;
                    }
                    return new List<Edge>
                    {
                        new Edge(minX, minY, 0, maxX, minY, 0),
                        new Edge(maxX, minY, 0, maxX, maxY, 0),
                        new Edge(maxX, maxY, 0, minX, maxY, 0),
                        new Edge(minX, maxY, 0, minX, minY, 0),
                    };
                }
            }

            List<Edge> simplified = new List<Edge>();
            simplified = ResampleArcs(edges, sheet, true, aStep, maxStepL);

            double aeraThreshold = Math.Pow(Math.Max(maxX - minX, maxY - minY) * 0.01, 2.0);
            aeraThreshold = Math.Max(aeraThreshold, gap * gap);
            if (aeraThreshold < tol0) aeraThreshold = tol0;
            if (sheet)
            {
                simplified = SimplifyVW(simplified, aeraThreshold, SimplifyMode.Concave);
            }
            else
            {
                //Always convex because of inner direction
                simplified = SimplifyVW(simplified, aeraThreshold, SimplifyMode.Convex);
            }
            
            if (simplified.Count < 3)
            {
                pave = true;
                if (maxY - minY > maxX - minX)
                {
                    minHrot = 90;
                }
                if (inner)
                {
                    return null;
                }
                else
                {
                    return new List<Edge>
                    {
                        new Edge(minX, minY, 0, maxX, minY, 0),
                        new Edge(maxX, minY, 0, maxX, maxY, 0),
                        new Edge(maxX, maxY, 0, minX, maxY, 0),
                        new Edge(minX, maxY, 0, minX, minY, 0),
                    };
                }
            }

            minHrot = GetRotMinHeight(simplified);

            foreach (Edge edge in simplified)
            {
                edge.R = 0.0;
                edge.NS = 0;
            }

            return simplified;
        }       
        public static List<Edge> ResampleArcs(List<Edge> edges, bool sheet, bool tangent, double aStep, double maxStepL)
        {
            List<Edge> simplified = new List<Edge>();

            for (int i = 0; i < edges.Count; i++)
            {
                if (Math.Abs(edges[i].R) > tol0)
                {
                    Vector2 p0 = new Vector2(edges[i].V0.X, edges[i].V0.Y);
                    int p1Id = Math.Max(1, edges[i].NS / 3);
                    Vector2 p1 = new Vector2(edges[i + p1Id - 1].V1.X, edges[i + p1Id - 1].V1.Y);
                    int p2Id = Math.Max(2, 2 * edges[i].NS / 3);
                    Vector2 p2 = new Vector2(edges[i + p2Id - 1].V1.X, edges[i + p2Id - 1].V1.Y);
                    Vector2 c = MATH.GetArcCenter(p0, p1, p2);
                    float cross = Vector2.Cross(p1 - p0, p2 - p1);

                    double a0 = Math.Atan2(edges[i].V0.Y - c.Y, edges[i].V0.X - c.X);
                    double a1 = Math.Atan2(edges[i + edges[i].NS - 1].V1.Y - c.Y, edges[i + edges[i].NS - 1].V1.X - c.X);
                    if (a0 < 0) a0 += 2 * Math.PI;
                    if (a1 < 0) a1 += 2 * Math.PI;
                    if (Math.Abs(2 * Math.PI - a0) < tol0) a0 = 0;
                    if (Math.Abs(2 * Math.PI - a1) < tol0) a1 = 0;
                    double da = a1 - a0;

                    if (Math.Abs(cross) < tolCross)
                    {
                        simplified.Add(new Edge(edges[i].V0.X, edges[i].V0.Y, 0, edges[i + edges[i].NS - 1].V1.X, edges[i + edges[i].NS - 1].V1.Y, 0));
                    }
                    else if (cross < 0)
                    {
                        if (da > 0) da -= 2 * Math.PI;
                        if (Math.Abs(da) < tol0) da = -2 * Math.PI;
                        if (sheet || !tangent)
                        {
                            simplified.AddRange(GetArcSegments(c.X, c.Y, Math.Abs(edges[i].R), a0, a0 + da, aStep, maxStepL));
                        }
                        else
                        {
                            simplified.AddRange(GetArcTangentSegments(c.X, c.Y, Math.Abs(edges[i].R), a0, a0 + da, aStep, maxStepL));
                        }
                    }
                    else
                    {
                        if (da < 0) da += 2 * Math.PI;
                        if (Math.Abs(da) < tol0) da = 2 * Math.PI;
                        if (sheet && tangent)
                        {
                            simplified.AddRange(GetArcTangentSegments(c.X, c.Y, Math.Abs(edges[i].R), a0, a0 + da, aStep, maxStepL));
                        }
                        else
                        {
                            simplified.AddRange(GetArcSegments(c.X, c.Y, Math.Abs(edges[i].R), a0, a0 + da, aStep, maxStepL));
                        }
                    }

                    i += edges[i].NS - 1;
                }
                else
                {
                    simplified.Add(edges[i]);
                }
            }

            return simplified;
        }      
        public enum SimplifyMode
        {
            Concave,
            Convex,
            All
        }
        internal static List<Edge> SimplifyVW(List<Edge> ctr, double areaThreshold, SimplifyMode mode)
        {
            List<Vector3> pts = new List<Vector3>();
            pts.Add(new Vector3(ctr[0].V0.X, ctr[0].V0.Y, ctr[0].V0.Z));
            foreach (Edge edge in ctr)
                pts.Add(new Vector3(edge.V1.X, edge.V1.Y, edge.V1.Z));

            if (pts.Count <= 2) return new List<Edge>(ctr);

            List<double> areas = new List<double>(new double[pts.Count]);
            areas[0] = double.MaxValue;
            areas[pts.Count - 1] = double.MaxValue;

            List<bool> keep = new List<bool>(new bool[pts.Count]);
            for (int i = 0; i < keep.Count; i++)
                keep[i] = true;

            for (int i = 1; i < pts.Count - 1; i++)
            {
                bool isConcave = IsConcave(pts[i - 1], pts[i], pts[i + 1]);
                bool isConvex = !isConcave;

                if ((mode == SimplifyMode.Concave && isConcave) ||
                    (mode == SimplifyMode.Convex && isConvex) ||
                    (mode == SimplifyMode.All))
                {
                    areas[i] = TriangleArea(pts[i - 1], pts[i], pts[i + 1]);
                }
                else
                {
                    areas[i] = double.MaxValue;
                }
            }

            bool changed;
            do
            {
                changed = false;
                for (int i = 1; i < pts.Count - 1; i++)
                {
                    if (keep[i] && areas[i] < areaThreshold)
                    {
                        keep[i] = false;
                        changed = true;

                        int prev = i - 1; while (prev >= 0 && !keep[prev]) prev--;
                        int next = i + 1; while (next < pts.Count && !keep[next]) next++;

                        if (prev >= 0 && next < pts.Count)
                        {
                            bool isConcave = IsConcave(pts[prev], pts[next], pts[Math.Min(next + 1, pts.Count - 1)]);
                            bool isConvex = !isConcave;

                            if ((mode == SimplifyMode.Concave && isConcave) ||
                                (mode == SimplifyMode.Convex && isConvex) ||
                                (mode == SimplifyMode.All))
                            {
                                areas[next] = TriangleArea(pts[prev], pts[next], pts[Math.Min(next + 1, pts.Count - 1)]);
                            }
                            else
                            {
                                areas[next] = double.MaxValue;
                            }
                        }

                        break;
                    }
                }
            } while (changed);

            List<Vector3> simplPts = new List<Vector3>();
            for (int i = 0; i < pts.Count; i++)
                if (keep[i]) simplPts.Add(pts[i]);

            List<Edge> simplifiedEdges = new List<Edge>();
            for (int i = 1; i < simplPts.Count; i++)
                simplifiedEdges.Add(new Edge(
                    simplPts[i - 1].X, simplPts[i - 1].Y, simplPts[i - 1].Z,
                    simplPts[i].X, simplPts[i].Y, simplPts[i].Z));

            return simplifiedEdges;

            bool IsConcave(Vector3 prev, Vector3 curr, Vector3 next)
            {
                double cross = (curr.X - prev.X) * (next.Y - curr.Y) - (curr.Y - prev.Y) * (next.X - curr.X);
                return cross < 0;
            }
            double TriangleArea(Vector3 a, Vector3 b, Vector3 c)
            {
                return Math.Abs((a.X * (b.Y - c.Y) + b.X * (c.Y - a.Y) + c.X * (a.Y - b.Y)) / 2.0);
            }
        }
        #endregion

        public class TriMesh
        {
            #region fields
            public List<Triangle> Triangles = new List<Triangle>();
            public List<Edge> Edges = new List<Edge>();
            #endregion
        }
        public class Triangle
        {
            #region fields
            public Vector3 V0 = new Vector3(0f, 0f, 0f);
            public Vector3 V1 = new Vector3(0f, 0f, 0f);
            public Vector3 V2 = new Vector3(0f, 0f, 0f);

            internal Vector3 N = new Vector3(0f, 0f, 0f);
            #endregion

            #region constructors
            public Triangle() { }
            public Triangle(double x0, double y0, double z0, double x1, double y1, double z1, double x2, double y2, double z2)
            {
                V0 = new Vector3((float)x0, (float)y0, (float)z0);
                V1 = new Vector3((float)x1, (float)y1, (float)z1);
                V2 = new Vector3((float)x2, (float)y2, (float)z2);
            }
            #endregion

            #region methods
            internal Triangle Clone()
            {
                return (Triangle)this.MemberwiseClone();
            }

            internal void ComputeProperties()
            {
                Vector3 e0 = new Vector3(V1[0] - V0[0], V1[1] - V0[1], V1[2] - V0[2]);
                Vector3 e1 = new Vector3(V2[0] - V0[0], V2[1] - V0[1], V2[2] - V0[2]);

                N = Vector3.Cross(e0, e1);
                N.Normalize();
            }
            #endregion
        }
        public class Edge
        {
            #region fields
            public Vector3 V0 = new Vector3(0f, 0f, 0f);
            public Vector3 V1 = new Vector3(0f, 0f, 0f);
            public double R = 0;
            public int NS = 0;
            #endregion

            #region constructors
            public Edge() { }
            public Edge(double x0, double y0, double z0, double x1, double y1, double z1, double r = 0, int ns = 0)
            {
                V0 = new Vector3((float)x0, (float)y0, (float)z0);
                V1 = new Vector3((float)x1, (float)y1, (float)z1);

                R = r;
                NS = ns;
            }
            #endregion

            #region methods
            internal Edge Clone()
            {
                return (Edge)this.MemberwiseClone();
            }
            #endregion
        }
    }
}
