using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NCnetic.Cam
{
    public class CAD
    {
        internal const float Ctol = 0.9f;
        internal const float tol0 = 1E-4f;
        internal const float tolOffset = 1E-2f;
        internal const float tolNxyz = 1E-2f;
        internal const float tolNz = 1E-2f;
        internal const float tolCross = 1f;

        #region feature
        public class Feature
        {
            public enum FeatureType
            {
                EXT,
                INT,
                OPEN,
                NO_CUT,
            }

            public enum ImportDocType
            {
                OPEN_ENTITY,
                CLOSED_ENTITY,
                NO_CUT_ENTITY,
            }

            public string Source = string.Empty;

            public ImportDocType ImportType = ImportDocType.OPEN_ENTITY;
            public FeatureType Type = FeatureType.EXT;
            public List<Edge> Edges = new List<Edge>();

            public string FeatureGuid = string.Empty;

            public string Tag = "";
            public int ToolNb = 0;
            public int Depth = -1;

            internal int PartIndex = -1;
            internal int Order = -1;
            internal int WebN = 0;

            //internal Vector3 Min;
            //internal Vector3 Max;

            public Feature()
            {

            }
            public Feature(string tag, ImportDocType importType)
            {
                Tag = tag;
                ImportType = importType;
            }
        }
        public class MeshFeature : Feature
        {
            public TriMesh Mesh = new TriMesh();
            public MeshFeature(TriMesh mesh)
            {
                Mesh = mesh;
            }
        }
        #endregion

        #region 2d
        public static List<List<Feature>> ExtractParts(List<Feature> features, double mergeDist, double linkDist, bool mergeLayers, bool addNoParts = false, 
            bool layerNb = true, int cutNb = 0, int mrkNb = 101)
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
                    if (layerNb)
                    {
                        int.TryParse(f.Tag, out f.ToolNb);
                    }
                    if (f.ToolNb < 0 || f.Tag == "_CUT")
                    {
                        f.ToolNb = cutNb;
                    }
                    if (f.Tag == "_MRK")
                    {
                        f.ImportType = Feature.ImportDocType.NO_CUT_ENTITY;
                    }

                    if (f.ImportType == Feature.ImportDocType.CLOSED_ENTITY)
                    {
                        //f.ImportType = Feature.ImportDocType.CLOSED_ENTITY;
                        if (f.ToolNb.ToString() != f.Tag || !layerNb) f.ToolNb = cutNb;
                        closed_ctrs.Add(f);
                        foreach (Edge seg in f.Edges)
                        {
                            used.Add(seg);
                        }
                    }
                    else if (f.ImportType == Feature.ImportDocType.NO_CUT_ENTITY)
                    {
                        //f.ImportType = Feature.ImportDocType.OPEN_ENTITY;
                        f.Type = Feature.FeatureType.NO_CUT;
                        if (f.ToolNb.ToString() != f.Tag || !layerNb) f.ToolNb = mrkNb;
                        open_ctrs.Add(f);
                        foreach (Edge seg in f.Edges)
                        {
                            used.Add(seg);
                        }
                    }
                }

                List<Edge> edges = group.SelectMany(f => f.Edges).ToList();

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
                        fclosed.ImportType = Feature.ImportDocType.CLOSED_ENTITY;
                        fclosed.Source = group.First().Source;
                        fclosed.Tag = group.First().Tag;
                        fclosed.ToolNb = group.First().ToolNb;
                        fclosed.Edges = contour;
                        closed_ctrs.Add(fclosed);
                    }
                    else
                    {
                        Feature fopen = new Feature();
                        fopen.ImportType = Feature.ImportDocType.OPEN_ENTITY;
                        fopen.Source = group.First().Source;
                        fopen.Tag = group.First().Tag;
                        fopen.ToolNb = group.First().ToolNb;
                        fopen.Edges = contour;
                        open_ctrs.Add(fopen);
                    }
                }

                // *******************************************************************************************
                // INT & EXT
                // *******************************************************************************************

                closed_ctrs = closed_ctrs.OrderByDescending(ctr => GetArea(ctr.Edges)).ToList();

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

                        foreach (Edge edg in closed_ctrs[i].Edges)
                        {
                            if (!PointInPolygon(edg.V1, closed_ctrs[j].Edges))
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
                        fext.Source = group.First().Source;
                        fext.Tag = group.First().Tag;
                        fext.ToolNb = group.First().ToolNb;
                        fext.Depth = depth[i];
                        fext.Type = Feature.FeatureType.EXT;
                        double signedArea = GetArea(closed_ctrs[i].Edges, true);
                        if (Math.Abs(signedArea) < tol0)
                        {
                            fext.Depth = -1;
                            fext.Type = Feature.FeatureType.OPEN;
                            fext.Edges.AddRange(closed_ctrs[i].Edges);
                            part.Add(fext);
                        }
                        else
                        {
                            if (signedArea > 0)
                            {
                                fext.Edges.AddRange(GetReversed(closed_ctrs[i].Edges));
                            }
                            else
                            {
                                fext.Edges.AddRange(closed_ctrs[i].Edges);
                            }
                            part.Add(fext);

                            for (int j = 0; j < n; j++)
                            {
                                if (depth[j] == depth[i] + 1)
                                {
                                    Vector3 testPt = closed_ctrs[j].Edges.First().V0;

                                    if (PointInPolygon(testPt, closed_ctrs[i].Edges))
                                    {
                                        Feature fint = new Feature();
                                        fint.Source = group.First().Source;
                                        fint.Tag = group.First().Tag;
                                        fint.ToolNb = group.First().ToolNb;
                                        fint.Depth = depth[i] + 1;
                                        fint.Type = Feature.FeatureType.INT;
                                        if (GetArea(closed_ctrs[j].Edges, true) < 0)
                                        {
                                            fint.Edges.AddRange(GetReversed(closed_ctrs[j].Edges));
                                        }
                                        else
                                        {
                                            fint.Edges.AddRange(closed_ctrs[j].Edges);
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
                        fopen.Source = open_ctrs[j].Source;
                        fopen.Tag = open_ctrs[j].Tag;
                        fopen.ToolNb = open_ctrs[j].ToolNb;
                        fopen.Depth = -1;
                        if (open_ctrs[j].Type == Feature.FeatureType.NO_CUT)
                        {
                            fopen.Type = Feature.FeatureType.NO_CUT;
                        }
                        else
                        {
                            fopen.Type = Feature.FeatureType.OPEN;
                        }
                        fopen.Edges.AddRange(open_ctrs[j].Edges);

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
                                    if (open_ctrs[j].Edges.Any())
                                    {
                                        for (int test = 0; test < 4; test++)
                                        {
                                            Vector3 testPt = open_ctrs[j].Edges.First().V0;
                                            if (test == 1) testPt = open_ctrs[j].Edges.Last().V1;
                                            if (test == 2) testPt = 0.5f * (open_ctrs[j].Edges.First().V0 + open_ctrs[j].Edges.First().V1);
                                            if (test == 3) testPt = 0.5f * (open_ctrs[j].Edges.Last().V0 + open_ctrs[j].Edges.Last().V1);

                                            if (PointInPolygon(testPt, f.Edges))
                                            {
                                                Feature fopen = new Feature();
                                                fopen.Source = open_ctrs[j].Source;
                                                fopen.Tag = open_ctrs[j].Tag;
                                                fopen.ToolNb = open_ctrs[j].ToolNb;
                                                fopen.Depth = f.Depth + 1;
                                                if (open_ctrs[j].Type == Feature.FeatureType.NO_CUT)
                                                {
                                                    fopen.Type = Feature.FeatureType.NO_CUT;
                                                }
                                                else
                                                {
                                                    fopen.Type = Feature.FeatureType.OPEN;
                                                }
                                                fopen.Edges.AddRange(open_ctrs[j].Edges);

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
                    fopen.Source = f.Source;
                    fopen.Tag = f.Tag;
                    fopen.ToolNb = f.ToolNb;
                    fopen.Depth = -1;
                    fopen.Type = Feature.FeatureType.OPEN;
                    fopen.Edges.AddRange(f.Edges);
                    part.Add(fopen);
                }
                parts.Add(part);
            }

            for (int i = 0; i < parts.Count; i++)
            {
                foreach (Feature f in parts[i])
                {
                    f.PartIndex = i;
                }
            }

            //if (DateTime.Now.ToString("yyyy") != "2027") return new List<List<Feature>>();

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
            float cos = (float)Math.Cos(r);
            float sin = (float)Math.Sin(r);

            foreach (Feature f in part)
            {
                Feature tFeature = new Feature();
                tFeature.PartIndex = f.PartIndex;
                tFeature.Depth = f.Depth;
                tFeature.Order = f.Order;
                tFeature.Source = f.Source;
                tFeature.Tag = f.Tag;
                tFeature.ToolNb = f.ToolNb;
                tFeature.Type = f.Type;

                foreach (Edge edge in f.Edges)
                {
                    xRot0 = edge.V0.X * cos - edge.V0.Y * sin + dx;
                    yRot0 = edge.V0.X * sin + edge.V0.Y * cos + dy;
                    xRot1 = edge.V1.X * cos - edge.V1.Y * sin + dx;
                    yRot1 = edge.V1.X * sin + edge.V1.Y * cos + dy;
                    Edge tedge = new Edge(xRot0, yRot0, xRot1, yRot1, edge.R, edge.NS);
                    tedge.Bevel = edge.Bevel.Clone();
                    tFeature.Edges.Add(tedge);
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
        public static List<Edge> GetBulgeSegments(double bulge, double x0, double y0, double x1, double y1, double aStep, double maxStepL)
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

            if (da > Math.PI * 0.999)
            {
                segs.AddRange(GetArcSegments(cx, cy, r, a0, (a0 + a1) / 2.0, aStep, maxStepL));
                segs.AddRange(GetArcSegments(cx, cy, r, (a0 + a1) / 2.0, a1, aStep, maxStepL));
                return segs;
            }

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

                segs.Add(new Edge(x0, y0, x1, y1, r, ns));
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

            segs.Add(new Edge(x0, y0, x1, y1));

            for (int i = 1; i <= ns - 1; i++)
            {
                x0 = cx + (r + c) * Math.Cos(a0 + step0 + (i - 1) * step);
                y0 = cy + (r + c) * Math.Sin(a0 + step0 + (i - 1) * step);
                x1 = cx + (r + c) * Math.Cos(a0 + step0 + i * step);
                y1 = cy + (r + c) * Math.Sin(a0 + step0 + i * step);

                segs.Add(new Edge(x0, y0, x1, y1));
            }

            x0 = cx + (r + c) * Math.Cos(a1 - step0);
            y0 = cy + (r + c) * Math.Sin(a1 - step0);
            x1 = cx + r * Math.Cos(a1);
            y1 = cy + r * Math.Sin(a1);

            segs.Add(new Edge(x0, y0, x1, y1));

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
                    Bevel = e.Bevel.Clone(),
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

            if (!inner)
            {
                if (GetArea(edges, false) / ((maxX - minX) * (maxY - minY)) > paveLimit || GetArea(edges, false) < gap * gap)
                {
                    pave = true;

                    if (maxY - minY > maxX - minX)
                    {
                        minHrot = 90;
                    }
                    return new List<Edge>
                    {
                        new Edge(minX, minY, maxX, minY),
                        new Edge(maxX, minY, maxX, maxY),
                        new Edge(maxX, maxY, minX, maxY),
                        new Edge(minX, maxY, minX, minY),
                    };
                }
            }

            List<Edge> simplified = new List<Edge>();
            simplified = ResampleArcs(edges, sheet, true, aStep, maxStepL);

            double aeraThreshold = Math.Pow(Math.Max(maxX - minX, maxY - minY) * 0.01, 2.0);
            aeraThreshold = Math.Max(aeraThreshold, gap * gap);
            if (aeraThreshold < tol0) aeraThreshold = tol0;

            simplified = SimplifyVW(simplified, aeraThreshold * 0.1, SimplifyMode.All);
            if (sheet)
            {
                simplified = SimplifyVW(simplified, aeraThreshold, SimplifyMode.Concave);
            }
            else
            {
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
                        new Edge(minX, minY, maxX, minY),
                        new Edge(maxX, minY, maxX, maxY),
                        new Edge(maxX, maxY, minX, maxY),
                        new Edge(minX, maxY, minX, minY),
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
                        simplified.Add(new Edge(edges[i].V0.X, edges[i].V0.Y, edges[i + edges[i].NS - 1].V1.X, edges[i + edges[i].NS - 1].V1.Y));
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
        public static void Split(ref List<Edge> edges, int id, float dist, bool micro, bool reverse)
        {
            if (!reverse)
            {
                for (int i = id; i < edges.Count; i++)
                {
                    if (Math.Abs(edges[i].R) < tol0)
                    {
                        float L = (edges[i].V1 - edges[i].V0).Length;
                        if (L > dist)
                        {
                            Vector3 n = edges[i].V1 - edges[i].V0;
                            n.Z = 0f;
                            n.Normalize();

                            edges[i].V0 = edges[i].V0 + n * dist;

                            edges.Insert(i, edges[i].Clone());
                            edges[i].V1 = edges[i + 1].V0;
                            edges[i].V0 = edges[i].V1 - n * dist;

                            if (micro) edges[i].Micro = true;

                            i = edges.Count;
                        }
                        else
                        {
                            dist -= L;
                        }
                    }
                    else
                    {
                        int ns = edges[i].NS;

                        Vector2 p0 = new Vector2(edges[i].V0.X, edges[i].V0.Y);
                        int p1Id = Math.Max(1, ns / 3);
                        Vector2 p1 = new Vector2(edges[i + p1Id - 1].V1.X, edges[i + p1Id - 1].V1.Y);
                        int p2Id = Math.Max(2, 2 * ns / 3);
                        Vector2 p2 = new Vector2(edges[i + p2Id - 1].V1.X, edges[i + p2Id - 1].V1.Y);
                        Vector2 c = MATH.GetArcCenter(p0, p1, p2);
                        float cross = Vector2.Cross(p1 - p0, p2 - p1);

                        double a0 = Math.Atan2(edges[i].V0.Y - c.Y, edges[i].V0.X - c.X);
                        double a1 = Math.Atan2(edges[i + ns - 1].V1.Y - c.Y, edges[i + ns - 1].V1.X - c.X);
                        if (a0 < 0) a0 += 2 * Math.PI;
                        if (a1 < 0) a1 += 2 * Math.PI;
                        if (Math.Abs(2 * Math.PI - a0) < tol0) a0 = 0;
                        if (Math.Abs(2 * Math.PI - a1) < tol0) a1 = 0;
                        double da = a1 - a0;

                        if (cross < 0)
                        {
                            if (da > 0) da -= 2 * Math.PI;
                        }
                        else
                        {
                            if (da < 0) da += 2 * Math.PI;
                        }

                        BevelDefinition bevel = edges[i].Bevel.Clone();

                        double R = Math.Abs(edges[i].R);
                        float L = (float)(Math.Abs(da) * R);
                        double aStep = Math.Abs(da / (double)(edges[i].NS + 1));

                        if (L > dist)
                        {
                            float a00 = (float)a0;
                            if (da < 0)
                            {
                                a0 -= dist / R;
                                da += dist / R;
                            }
                            else
                            {
                                a0 += dist / R;
                                da -= dist / R;
                            }
                            edges.RemoveRange(i, ns);
                            List<Edge> add = GetArcSegments(c.X, c.Y, R, a0, a0 + da, aStep, 999999.0);
                            foreach (Edge e in add)
                            {
                                e.Micro = micro;
                                e.Bevel = bevel;
                            }
                            edges.InsertRange(i, add);
                            List<Edge> add2 = GetArcSegments(c.X, c.Y, R, a00, a0, aStep, 999999.0);
                            foreach (Edge e in add2)
                            {
                                e.Micro = micro;
                                e.Bevel = bevel;
                            }
                            //if (micro)
                            //{
                            //    foreach (Edge e in add2)
                            //    {
                            //        e.Micro = true;
                            //    }
                            //}
                            edges.InsertRange(i, add2);

                            i = edges.Count;
                        }
                        else
                        {
                            dist -= L;
                            i = i + ns - 1;
                        }
                    }
                }
            }
            else
            {
                for (int i = id; i >= 0; i--)
                {
                    if (Math.Abs(edges[i].R) < tol0)
                    {
                        float L = (edges[i].V1 - edges[i].V0).Length;
                        if (L > dist)
                        {
                            Vector3 n = edges[i].V1 - edges[i].V0;
                            n.Z = 0f;
                            n.Normalize();

                            edges[i].V1 = edges[i].V1 - n * dist;

                            edges.Insert(i, edges[i].Clone());
                            edges[i + 1].V0 = edges[i].V1;
                            edges[i + 1].V1 = edges[i + 1].V0 + n * dist;

                            if (micro) edges[i + 1].Micro = true;

                            i = -1;
                        }
                        else
                        {
                            dist -= L;
                        }
                    }
                    else
                    {
                        int ns = edges[i].NS;

                        Vector2 p0 = new Vector2(edges[i - ns + 1].V0.X, edges[i - ns + 1].V0.Y);
                        int p1Id = Math.Max(1, ns / 3);
                        Vector2 p1 = new Vector2(edges[i - ns + p1Id].V1.X, edges[i - ns + p1Id].V1.Y);
                        int p2Id = Math.Max(2, 2 * ns / 3);
                        Vector2 p2 = new Vector2(edges[i - ns + p2Id].V1.X, edges[i - ns + p2Id].V1.Y);
                        Vector2 c = MATH.GetArcCenter(p0, p1, p2);
                        float cross = Vector2.Cross(p1 - p0, p2 - p1);

                        double a0 = Math.Atan2(edges[i - ns + 1].V0.Y - c.Y, edges[i - ns + 1].V0.X - c.X);
                        double a1 = Math.Atan2(edges[i].V1.Y - c.Y, edges[i].V1.X - c.X);
                        if (a0 < 0) a0 += 2 * Math.PI;
                        if (a1 < 0) a1 += 2 * Math.PI;
                        if (Math.Abs(2 * Math.PI - a0) < tol0) a0 = 0;
                        if (Math.Abs(2 * Math.PI - a1) < tol0) a1 = 0;
                        double da = a1 - a0;

                        if (cross < 0)
                        {
                            if (da > 0) da -= 2 * Math.PI;
                        }
                        else
                        {
                            if (da < 0) da += 2 * Math.PI;
                        }

                        double R = Math.Abs(edges[i].R);
                        float L = (float)(Math.Abs(da) * R);
                        double aStep = Math.Abs(da / (double)(ns + 1));

                        if (L > dist)
                        {
                            float da0 = (float)da;
                            if (da < 0)
                            {
                                da += dist / R;
                            }
                            else
                            {
                                da -= dist / R;
                            }

                            BevelDefinition bevel = edges[i].Bevel.Clone();

                            edges.RemoveRange(i - ns + 1, ns);
                            List<Edge> add = GetArcSegments(c.X, c.Y, R, a0, a0 + da, aStep, 999999.0);
                            foreach (Edge e in add)
                            {
                                e.Micro = micro;
                                e.Bevel = bevel;
                            }
                            edges.InsertRange(i - ns + 1, add);
                            List<Edge> add2 = GetArcSegments(c.X, c.Y, R, a0 + da, a0 + da0, aStep, 999999.0);
                            foreach (Edge e in add2)
                            {
                                e.Micro = micro;
                                e.Bevel = bevel;
                            }
                            //if (micro)
                            //{
                            //    foreach (Edge e in add2)
                            //    {
                            //        e.Micro = true;
                            //    }
                            //}
                            edges.InsertRange(i - ns + 1 + add.Count, add2);

                            i = -1;
                        }
                        else
                        {
                            dist -= L;
                            i = i - ns + 1;
                        }
                    }
                }
            }
        }
        public static void OffsetPropertiesRecovery(ref List<Edge> offset, List<Edge> ctr, double dist)
        {
            // CLEAN OFFSET FROM L0
            for (int j = 0; j < offset.Count; j++)
            {
                if ((offset[j].V1 - offset[j].V0).Length < tol0)
                {
                    offset.RemoveAt(j);
                    j--;
                }
            }

            // FIND A GOOD START
            int bestStart = -1;
            for (int i = 0; i < ctr.Count; i++)
            {
                if (Math.Abs(ctr[i].R) < tol0) 
                {
                    for (int j = 0; j < offset.Count; j++)
                    {
                        if (IsOffsetOf(ctr[i], offset[j], dist))
                        {
                            bestStart = j;
                            break;
                        }
                    }

                    if (bestStart != -1)
                        break;
                }
            }
            if (bestStart == -1)
            {
                for (int i = 0; i < ctr.Count;)
                {
                    if (Math.Abs(ctr[i].R) > tol0)
                    {
                        for (int j = 0; j < offset.Count; j++)
                        {
                            if (IsOffsetOf(ctr[i], offset[j], dist))
                            {
                                bestStart = j;
                                break;
                            }
                        }

                        if (bestStart != -1)
                            break;

                        i += ctr[i].NS;
                    }
                    else
                    {
                        i++;
                    }
                }
            }
            if (bestStart == -1 && offset.Count > 0)
            {
                double minX = offset[0].V0.X;
                double minY = offset[0].V0.Y;
                bestStart = 0;

                for (int i = 1; i < offset.Count; i++)
                {
                    var v = offset[i].V0;

                    if (v.X < minX || (Math.Abs(v.X - minX) < tol0 && v.Y < minY))
                    {
                        minX = v.X;
                        minY = v.Y;
                        bestStart = i;
                    }
                }
            }
            if (bestStart > 0)
            {
                List<Edge> splitStart = offset.GetRange(0, bestStart);
                offset.RemoveRange(0, bestStart);
                offset.AddRange(splitStart);
            }

            // ASSOCIATE CTR 
            List<int> offsetId = null;
            int startJ = 0;
            offsetId = Enumerable.Repeat(-1, ctr.Count).ToList();
            int currentJ = startJ;

            for (int i = 0; i < ctr.Count; i++)
            {
                bool found = false;
                for (int j = currentJ; j < offset.Count; j++)
                {
                    if (IsOffsetOf(ctr[i], offset[j], dist) && !offsetId.Contains(j))
                    {
                        offsetId[i] = j;
                        currentJ = j;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    for (int j = 0; j < currentJ; j++)
                    {
                        if (IsOffsetOf(ctr[i], offset[j], dist) && !offsetId.Contains(j))
                        {
                            offsetId[i] = j;
                            currentJ = j;
                            break;
                        }
                    }
                }
            }

            for (int i = 0; i < ctr.Count; i++)
            {
                if (offsetId[i] > -1)
                {
                    offset[offsetId[i]].Bevel = ctr[i].Bevel.Clone();
                }
                else
                {
                    // TODO !!!
                    // TRANSITION TO MANAGE
                }
            }

            if (offsetId == null) return;

            for (int i = 0; i < ctr.Count; i++)
            {
                if (Math.Abs(ctr[i].R) > tol0)
                {
                    Vector2 p0 = new Vector2(ctr[i].V0.X, ctr[i].V0.Y);
                    int p1Id = Math.Max(1, ctr[i].NS / 3);
                    Vector2 p1 = new Vector2(ctr[i + p1Id - 1].V1.X, ctr[i + p1Id - 1].V1.Y);
                    int p2Id = Math.Max(2, 2 * ctr[i].NS / 3);
                    Vector2 p2 = new Vector2(ctr[i + p2Id - 1].V1.X, ctr[i + p2Id - 1].V1.Y);
                    Vector2 c = MATH.GetArcCenter(p0, p1, p2);
                    float cross = Vector2.Cross(p1 - p0, p2 - p1);
                    double offsetRadius = Math.Abs(ctr[i].R);
                    if (cross > 0)
                    {
                        offsetRadius -= dist;
                    }
                    else
                    {
                        offsetRadius += dist;
                    }

                    for (int j = i; j < i + ctr[i].NS; j++)
                    {
                        if (offsetId[j] > -1)
                        {
                            if (Math.Abs((offset[offsetId[j]].V0 - new Vector3(c.X, c.Y, 0f)).Length - offsetRadius) > dist * tolOffset ||
                                Math.Abs((offset[offsetId[j]].V1 - new Vector3(c.X, c.Y, 0f)).Length - offsetRadius) > dist * tolOffset)
                            {
                                offsetId[j] = -1;
                            }
                        }
                    }

                    int id0 = -1;
                    int id1 = -1;

                    for (int j = i; j < i + ctr[i].NS; j++)
                    {
                        if (offsetId[j] > -1)
                        {
                            if (id0 == -1)
                            {
                                id0 = j;
                                id1 = j;
                            }
                            else
                            {
                                if (id1 - id0 + 1 < ctr[i].NS)
                                {
                                    id1++;
                                }
                            }

                            if (offsetId[id1] < offsetId[id0])
                            {
                                id1--;
                                if (id0 > -1 && id1 > -1 && id1 - id0 > 1)
                                {
                                    for (int k = id0; k < id1; k++)
                                    {
                                        offset[offsetId[k]].R = offsetRadius;
                                        offset[offsetId[k]].NS = id1 - id0 + 1;
                                    }
                                }
                                id0 = -1;
                                id1 = -1;
                            }
                        }
                        else
                        {
                            if (id1 > -1)
                            {
                                j = i + ctr[i].NS;
                            }
                        }
                    }

                    if (id0 > -1 && id1 > -1 && id1 - id0 > 1)
                    {
                        for (int j = id0; j <= id1; j++)
                        {
                            offset[offsetId[j]].R = offsetRadius;
                            offset[offsetId[j]].NS = id1 - id0 + 1;
                        }
                    }

                    i += ctr[i].NS - 1;
                }
            }
        }
        private static bool IsOffsetOf(Edge edge0, Edge edge1, double offsetDistance)
        {
            double d0 = DistancePointToLine(edge1.V0, edge0.V0, edge0.V1);
            double d1 = DistancePointToLine(edge1.V1, edge0.V0, edge0.V1);

            if (Math.Abs(d0 - offsetDistance) < offsetDistance * tolOffset && Math.Abs(d1 - offsetDistance) < offsetDistance * tolOffset)
            {
                return true;
            }

            return false;

            double DistancePointToLine(Vector3 pt, Vector3 v0, Vector3 v1)
            {
                Vector3 AB = v1 - v0;
                Vector3 AP = pt - v0;
                return Vector3.Cross(AB, AP).Length / AB.Length;
            }
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
                simplifiedEdges.Add(new Edge(simplPts[i - 1].X, simplPts[i - 1].Y, simplPts[i].X, simplPts[i].Y));

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
        public static List<List<Feature>> GetWeb(List<List<Feature>> parts, double L, int nmax)
        {
            List<List<Feature>> web = new List<List<Feature>>();
            foreach (List<Feature> p in parts)
            {
                List<Feature> wp = new List<Feature>();
                foreach (Feature f in p)
                {
                    Feature fp = new Feature();
                    fp.FeatureGuid = f.FeatureGuid;
                    fp.PartIndex = f.PartIndex;
                    fp.Depth = f.Depth;
                    fp.Order = f.Order;
                    fp.Source = f.Source;
                    fp.Tag = f.Tag;
                    fp.ToolNb = f.ToolNb;
                    fp.Type = f.Type;
                    foreach (Edge e in f.Edges)
                    {
                       fp.Edges.Add(e.Clone());
                    }
                    wp.Add(fp);
                }
                web.Add(wp);
            }

            List<int> webN = Enumerable.Repeat(1, web.Count).ToList();

            for (int i = 0; i < web.Count; i++)
            {
                Feature extI = web[i].Find(p => p.Type == Feature.FeatureType.EXT);
                if (extI != null)
                {
                    for (int j = 0; j < web.Count; j++)
                    {
                        if (i != j)
                        {
                            Feature extJ = web[j].Find(p => p.Type == Feature.FeatureType.EXT);
                            if (extJ != null)
                            {
                                //float dx = Math.Max(extJ.Min.X - extI.Max.X, extI.Min.X - extJ.Max.X);
                                //float dy = Math.Max(extJ.Min.Y - extI.Max.Y, extI.Min.Y - extJ.Max.Y);

                                //double Lij;
                                //if (dx > 0 || dy > 0)
                                //{
                                //    Lij = Math.Sqrt(Math.Max(dx, 0) * Math.Max(dx, 0) + Math.Max(dy, 0) * Math.Max(dy, 0));
                                //}
                                //else
                                //{
                                //    Lij = Math.Max(dx, dy);
                                //}

                                //if (Lij < L && webN[i] + webN[j] <= nmax)
                                if (webN[i] + webN[j] <= nmax)
                                {
                                    List<Feature> newI = WebMerge(web[i], web[j], L);
                                    if (newI != null)
                                    {
                                        webN[i] += webN[j];
                                        web[i] = newI;

                                        web.RemoveAt(j);
                                        webN.RemoveAt(j);

                                        if (j < i)
                                        {
                                            i--;
                                        }
                                        j--;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return web;
        }
        public static List<Feature> WebMerge_(List<Feature> partI, List<Feature> partJ, double L)
        {
            Feature extI = partI.Find(p => p.Type == Feature.FeatureType.EXT);
            Feature extJ = partJ.Find(p => p.Type == Feature.FeatureType.EXT);

            int minI = -1;
            int minJ = -1;
            float minLij = float.MaxValue;

            for (int i = 0; i < extI.Edges.Count; i++)
            {
                for (int j = 0; j < extJ.Edges.Count; j++)
                {
                    bool noBevel = true;
                    if (extI.Edges[i].Bevel.Type != BevelType.I) noBevel = false;
                    if (extJ.Edges[j].Bevel.Type != BevelType.I) noBevel = false;
                    if (i > 1)
                    {
                        if (extI.Edges[i - 1].Bevel.Type != BevelType.I) noBevel = false;
                    }
                    else
                    {
                        if (extI.Edges[extI.Edges.Count - 1].Bevel.Type != BevelType.I) noBevel = false;
                    }
                    if (j > 1)
                    {
                        if (extJ.Edges[j - 1].Bevel.Type != BevelType.I) noBevel = false;
                    }
                    else
                    {
                        if (extJ.Edges[extJ.Edges.Count - 1].Bevel.Type != BevelType.I) noBevel = false;
                    }

                    if (noBevel)
                    {
                        float Lij = (extI.Edges[i].V0 - extJ.Edges[j].V0).Length;
                        if (Lij < L && Lij < minLij)
                        {
                            minI = i;
                            minJ = j;
                            minLij = Lij;
                        }

                        if (Math.Abs(extJ.Edges[j].R) > tol0)
                        {
                            j += extJ.Edges[j].NS - 1;
                        }
                    }
                }

                if (Math.Abs(extI.Edges[i].R) > tol0)
                {
                    i += extI.Edges[i].NS - 1;
                }
            }

            if (minI > -1 && minJ > -1)
            {
                List<Edge> tmple = new List<Edge>();
                tmple.Add(new Edge
                {
                    V0 = extI.Edges[minI].V0,
                    V1 = extJ.Edges[minJ].V0,
                });
                tmple.AddRange(extJ.Edges.GetRange(minJ, extJ.Edges.Count - minJ));
                tmple.AddRange(extJ.Edges.GetRange(0, minJ));
                tmple.Add(new Edge
                {
                    V0 = extJ.Edges[minJ].V0,
                    V1 = extI.Edges[minI].V0,
                });

                extI.Edges.InsertRange(minI, tmple);

                foreach (Feature f in partJ)
                {
                    if (f.Type != Feature.FeatureType.EXT)
                    {
                        partI.Add(f);
                    }
                }

                return partI;
            }

            return null;
        }
        public static List<Feature> WebMerge(List<Feature> partI, List<Feature> partJ, double L)
        {
            Feature extI = partI.Find(p => p.Type == Feature.FeatureType.EXT);
            Feature extJ = partJ.Find(p => p.Type == Feature.FeatureType.EXT);

            int minI = -1;
            int minJ = -1;
            float minLij = float.MaxValue;

            float bestDistI = 0f;
            float bestDistJ = 0f;

            for (int i = 0; i < extI.Edges.Count; i++)
            {
                Vector3 p1 = extI.Edges[i].V0;
                Vector3 q1 = extI.Edges[i].V1;
                float lenI = (q1 - p1).Length;

                for (int j = 0; j < extJ.Edges.Count; j++)
                {
                    bool noBevel = true;
                    if (extI.Edges[i].Bevel.Type != BevelType.I) noBevel = false;
                    if (extJ.Edges[j].Bevel.Type != BevelType.I) noBevel = false;
                    if (i > 1)
                    {
                        if (extI.Edges[i - 1].Bevel.Type != BevelType.I) noBevel = false;
                    }
                    else
                    {
                        if (extI.Edges[extI.Edges.Count - 1].Bevel.Type != BevelType.I) noBevel = false;
                    }
                    if (j > 1)
                    {
                        if (extJ.Edges[j - 1].Bevel.Type != BevelType.I) noBevel = false;
                    }
                    else
                    {
                        if (extJ.Edges[extJ.Edges.Count - 1].Bevel.Type != BevelType.I) noBevel = false;
                    }

                    if (noBevel)
                    {
                        Vector3 p2 = extJ.Edges[j].V0;
                        Vector3 q2 = extJ.Edges[j].V1;
                        float lenJ = (q2 - p2).Length;

                        float s, t;
                        Vector3 c1, c2;

                        float Lij = SegmentSegmentDistance(p1, q1, p2, q2, out s, out t, out c1, out c2 );

                        if (Lij < L && Lij < minLij)
                        {
                            minLij = Lij;

                            minI = i;
                            minJ = j;

                            bestDistI = s * lenI;
                            bestDistJ = t * lenJ;

                            if (Math.Abs(bestDistI - lenI) < tol0)
                            {
                                minI++;
                                bestDistI = 0f;
                                if (minI > extI.Edges.Count - 1) minI = 0;
                            }
                            if (Math.Abs(bestDistJ - lenJ) < tol0)
                            {
                                minJ++;
                                bestDistJ = 0f;
                                if (minJ > extJ.Edges.Count - 1) minJ = 0;
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < extI.Edges.Count; i++)
            {
                if (Math.Abs(extI.Edges[i].R) > tol0)
                {
                    float arcL = 0f;

                    for (int j = 0; j < extI.Edges[i].NS; j++)
                    {
                        float sl = (extI.Edges[i + j].V1 - extI.Edges[i + j].V0).Length;
                        if (i + j == minI)
                        {
                            minI = i;
                            bestDistI += arcL;
                            j = extI.Edges[i].NS;
                        }
                        else
                        {
                            arcL += sl;
                        }
                    }

                    i += extI.Edges[i].NS - 1;
                }
            }          
            for (int i = 0; i < extJ.Edges.Count; i++)
            {
                if (Math.Abs(extJ.Edges[i].R) > tol0)
                {
                    float arcL = 0f;

                    for (int j = 0; j < extJ.Edges[i].NS; j++)
                    {
                        float sl = (extJ.Edges[i + j].V1 - extJ.Edges[i + j].V0).Length;
                        if (i + j == minJ)
                        {
                            minJ = i;
                            bestDistJ += arcL;
                            j = extJ.Edges[i].NS;
                        }
                        else
                        {
                            arcL += sl;
                        }
                    }

                    i += extJ.Edges[i].NS - 1;
                }
            }

            if (minI > -1 && bestDistI > tol0)
            {
                Split(ref extI.Edges, minI, bestDistI, false, false);
                if (Math.Abs(extI.Edges[minI].R) > tol0)
                {
                    minI += extI.Edges[minI].NS;
                }
                else
                {
                    minI++;
                }
                if (minI > extI.Edges.Count - 1)
                {
                    minI = 0;
                }
            }

            if (minJ > -1 && bestDistJ > tol0)
            {
                Split(ref extJ.Edges, minJ, bestDistJ, false, false);
                if (Math.Abs(extJ.Edges[minJ].R) > tol0)
                {
                    minJ += extJ.Edges[minJ].NS;
                }
                else
                {
                    minJ++;
                }
                if (minJ > extJ.Edges.Count - 1)
                {
                    minJ = 0;
                }
            }

            if (minI > -1 && minJ > -1)
            {
                List<Edge> tmple = new List<Edge>();
                tmple.Add(new Edge
                {
                    V0 = extI.Edges[minI].V0,
                    V1 = extJ.Edges[minJ].V0,
                });
                tmple.AddRange(extJ.Edges.GetRange(minJ, extJ.Edges.Count - minJ));
                tmple.AddRange(extJ.Edges.GetRange(0, minJ));
                tmple.Add(new Edge
                {
                    V0 = extJ.Edges[minJ].V0,
                    V1 = extI.Edges[minI].V0,
                });

                extI.Edges.InsertRange(minI, tmple);
                extI.WebN += extJ.WebN + 1;

                foreach (Feature f in partJ)
                {
                    if (f.Type != Feature.FeatureType.EXT)
                    {
                        partI.Add(f);
                    }
                }

                return partI;
            }

            return null;

            float SegmentSegmentDistance(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2, out float s, out float t, out Vector3 c1, out Vector3 c2)
            {
                Vector3 d1 = q1 - p1;
                Vector3 d2 = q2 - p2;
                Vector3 r = p1 - p2;

                float a = Vector3.Dot(d1, d1);
                float e = Vector3.Dot(d2, d2);
                float f = Vector3.Dot(d2, r);

                if (a <= 1e-6f && e <= 1e-6f)
                {
                    s = 0f;
                    t = 0f;
                    c1 = p1;
                    c2 = p2;
                    return (c1 - c2).Length;
                }

                if (a <= 1e-6f)
                {
                    s = 0f;
                    t = f / e;
                    if (t < 0f) t = 0f;
                    else if (t > 1f) t = 1f;
                }
                else
                {
                    float c = Vector3.Dot(d1, r);

                    if (e <= 1e-6f)
                    {
                        t = 0f;
                        s = -c / a;
                        if (s < 0f) s = 0f;
                        else if (s > 1f) s = 1f;
                    }
                    else
                    {
                        float b = Vector3.Dot(d1, d2);
                        float denom = a * e - b * b;

                        if (denom != 0f)
                        {
                            s = (b * f - c * e) / denom;
                            if (s < 0f) s = 0f;
                            else if (s > 1f) s = 1f;
                        }
                        else
                        {
                            s = 0f;
                        }

                        t = (b * s + f) / e;

                        if (t < 0f)
                        {
                            t = 0f;
                            s = -c / a;
                            if (s < 0f) s = 0f;
                            else if (s > 1f) s = 1f;
                        }
                        else if (t > 1f)
                        {
                            t = 1f;
                            s = (b - c) / a;
                            if (s < 0f) s = 0f;
                            else if (s > 1f) s = 1f;
                        }
                    }
                }

                c1 = p1 + d1 * s;
                c2 = p2 + d2 * t;

                return (c1 - c2).Length;
            }
        }
        public static void NewStart(ref List<Edge> edges, double px, double py)
        {
            double MinDist = double.MaxValue;
            double tMinDist = 0.0;
            int MinId = -1;
            double splitDist = -1;

            for (int i = 0; i < edges.Count; i++)
            {
                if (Math.Abs(edges[i].R) > tol0)
                {
                    for (int j = i; j < i + edges[i].NS; j++)
                    {
                        double dx = edges[j].V1.X - edges[j].V0.X;
                        double dy = edges[j].V1.Y - edges[j].V0.Y;

                        if (dx * dx + dy * dy > 0)
                        {
                            double t = ((px - edges[j].V0.X) * dx + (py - edges[j].V0.Y) * dy) / (dx * dx + dy * dy);
                            t = Math.Max(0.0, Math.Min(1.0, t));
                            double distX = (px - edges[j].V0.X + t * dx);
                            double distY = (py - edges[j].V0.Y + t * dy);
                            double dist = Math.Sqrt(distX * distX + distY * distY);

                            if (dist < MinDist)
                            {
                                MinId = i;
                                MinDist = dist;
                                tMinDist = t;
                                distX = edges[j].V0.X + t * dx;
                                distY = edges[j].V0.Y + t * dy;
                                splitDist = Math.Sqrt(distX * distX + distY * distY);
                            }
                        }
                    }

                    i += edges[i].NS - 1;
                }
                else
                {
                    double dx = edges[i].V1.X - edges[i].V0.X;
                    double dy = edges[i].V1.Y - edges[i].V0.Y;

                    if (dx * dx + dy * dy > 0)
                    {
                        double t = ((px - edges[i].V0.X) * dx + (py - edges[i].V0.Y) * dy) / (dx * dx + dy * dy);
                        t = Math.Max(0.0, Math.Min(1.0, t));
                        double distX = (px - edges[i].V0.X + t * dx);
                        double distY = (py - edges[i].V0.Y + t * dy);
                        double dist = Math.Sqrt(distX * distX + distY * distY);
                        
                        if (dist < MinDist)
                        {
                            MinId = i;
                            MinDist = dist;
                            tMinDist = t;
                            distX = edges[i].V0.X + t * dx;
                            distY = edges[i].V0.Y + t * dy;
                            splitDist = Math.Sqrt(distX * distX + distY * distY);
                        }
                    }
                }
            }

            if (MinId > 0)
            {
                List<Edge> tmple = new List<Edge>();
                tmple.AddRange(edges.GetRange(MinId, edges.Count - MinId));
                tmple.AddRange(edges.GetRange(0, MinId));
                edges = tmple;
            }
        }

        public class Transform2d
        {
            public double X = 0.0;
            public double Y = 0.0;
            public double Rot = 0.0;

            public Transform2d(double x, double y, double rot)
            {
                X = x; 
                Y = y;
                Rot= rot;
            }
        }
        public static Transform2d GetTransform(List<Feature> partI, List<Feature> partJ, double tol)
        {
            if (partI == null || partJ == null) return null;

            List<Vector3> ptsI = new List<Vector3>();
            List<double> rI = new List<double>();
            List<Vector3> ptsJ = new List<Vector3>();
            List<double> rJ = new List<double>();

            for (int i = 0; i < partI.Count; i++)
            {
                Feature feature = partI[i];
                for (int j = 0; j < feature.Edges.Count; j++)
                {
                    if (Math.Abs(feature.Edges[j].R) > tol0)
                    {
                        j += feature.Edges[j].NS - 1;
                    }
                    Edge edge = feature.Edges[j];
                    ptsI.Add(new Vector3(edge.V1.X, edge.V1.Y, 0f));
                    rI.Add(edge.R);
                }
            }

            for (int i = 0; i < partJ.Count; i++)
            {
                Feature feature = partJ[i];
                for (int j = 0; j < feature.Edges.Count; j++)
                {
                    if (Math.Abs(feature.Edges[j].R) > tol0)
                    {
                        j += feature.Edges[j].NS - 1;
                    }
                    Edge edge = feature.Edges[j];
                    ptsJ.Add(new Vector3(edge.V1.X, edge.V1.Y, 0f));
                    rJ.Add(edge.R);
                }
            }

            if (ptsI.Count != ptsJ.Count)
            {
                return null;
            }

            int n = ptsI.Count;
            List<double> distI = new List<double>(n * n);
            List<double> distJ = new List<double>(n * n);

            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    Vector3 dI = ptsI[i] - ptsI[j];
                    distI.Add(dI.Length * dI.Length);
                    Vector3 dJ = ptsJ[i] - ptsJ[j];
                    distJ.Add(dJ.Length * dJ.Length);
                }
            }

            if (distI.Count != distJ.Count)
            {
                return null;
            }

            distI.Sort();
            distJ.Sort();

            for (int i = 0; i < distI.Count; i++)
            {
                if (Math.Abs(distI[i] - distJ[i]) > tol)
                {
                    return null;
                }
            }

            rI.Sort();
            rJ.Sort();

            for (int i = 0; i < rI.Count; i++)
            {
                if (Math.Abs(rI[i] - rJ[i]) > tol)
                {
                    return null;
                }
            }

            // ROT

            //Vector3 centroidI = new Vector3(0f, 0f, 0f);
            //Vector3 centroidJ = new Vector3(0f, 0f, 0f);
            //for (int i = 0; i < n; i++)
            //{
            //    centroidI += ptsI[i];
            //    centroidJ += ptsJ[i];
            //}
            //centroidI /= n;
            //centroidJ /= n;

            //double sumCross = 0.0;
            //double sumDot = 0.0;
            //for (int i = 0; i < n; i++)
            //{
            //    double jx = ptsJ[i].X - centroidJ.X;
            //    double jy = ptsJ[i].Y - centroidJ.Y;
            //    double ix = ptsI[i].X - centroidI.X;
            //    double iy = ptsI[i].Y - centroidI.Y;
            //    sumCross += jx * iy - jy * ix;
            //    sumDot += jx * ix + jy * iy;
            //}
            //double rot = Math.Atan2(sumCross, sumDot);

            //double cosR = Math.Cos(rot);
            //double sinR = Math.Sin(rot);

            //double tx = centroidJ.X - (centroidI.X * cosR - centroidI.Y * sinR);
            //double ty = centroidJ.Y - (centroidI.X * sinR + centroidI.Y * cosR);

            //return new Transform2d(tx, ty, rot);

            Feature fI = partI.Find(f => f.Type == Feature.FeatureType.EXT);
            Feature fJ = partJ.Find(f => f.Type == Feature.FeatureType.EXT);

            if (fI == null || fJ == null)
            {
                return null;
            }

            ptsI = new List<Vector3>();
            ptsJ = new List<Vector3>();

            foreach (Edge edge in fI.Edges)
            {
                ptsI.Add(new Vector3(edge.V1.X, edge.V1.Y, 0f));
            }

            foreach (Edge edge in fJ.Edges)
            {
                ptsJ.Add(new Vector3(edge.V1.X, edge.V1.Y, 0f));
            }

            if (ptsI.Count != ptsJ.Count)
            {
                return null;
            }

            return FindBestTransform(ptsI, ptsJ, tol);
        }
        private static Transform2d FindBestTransform(List<Vector3> ptsI, List<Vector3> ptsJ, double tol)
        {
            int n = ptsI.Count;

            Transform2d bestTransform = null;
            double bestError = double.MaxValue;

            for (int reverse = 0; reverse < 2; reverse++)
            {
                List<Vector3> candidate =
                    reverse == 0
                    ? ptsJ
                    : ptsJ.AsEnumerable().Reverse().ToList();

                for (int shift = 0; shift < n; shift++)
                {
                    List<Vector3> shifted = new List<Vector3>(n);

                    for (int i = 0; i < n; i++)
                    {
                        shifted.Add(candidate[(i + shift) % n]);
                    }

                    Transform2d tr = ComputeTransform(ptsI, shifted);

                    double error = ComputeFitError(ptsI, shifted, tr);

                    if (error < bestError)
                    {
                        bestError = error;
                        bestTransform = tr;
                    }
                }
            }

            return bestError <= tol ? bestTransform : null;
        }
        private static Transform2d ComputeTransform(List<Vector3> ptsI, List<Vector3> ptsJ)
        {
            int n = ptsI.Count;

            Vector3 centroidI = Vector3.Zero;
            Vector3 centroidJ = Vector3.Zero;

            for (int i = 0; i < n; i++)
            {
                centroidI += ptsI[i];
                centroidJ += ptsJ[i];
            }

            centroidI /= n;
            centroidJ /= n;

            double sumCross = 0.0;
            double sumDot = 0.0;

            for (int i = 0; i < n; i++)
            {
                double ix = ptsI[i].X - centroidI.X;
                double iy = ptsI[i].Y - centroidI.Y;

                double jx = ptsJ[i].X - centroidJ.X;
                double jy = ptsJ[i].Y - centroidJ.Y;

                sumCross += ix * jy - iy * jx;
                sumDot += ix * jx + iy * jy;
            }

            double rot = Math.Atan2(sumCross, sumDot);

            double cosR = Math.Cos(rot);
            double sinR = Math.Sin(rot);

            double tx =
                centroidJ.X -
                (centroidI.X * cosR - centroidI.Y * sinR);

            double ty =
                centroidJ.Y -
                (centroidI.X * sinR + centroidI.Y * cosR);

            return new Transform2d(tx, ty, rot);
        }
        private static double ComputeFitError(List<Vector3> ptsI, List<Vector3> ptsJ, Transform2d tr)
        {
            double cosR = Math.Cos(tr.Rot);
            double sinR = Math.Sin(tr.Rot);

            double error = 0.0;

            for (int i = 0; i < ptsI.Count; i++)
            {
                double x =
                    ptsI[i].X * cosR -
                    ptsI[i].Y * sinR +
                    tr.X;

                double y =
                    ptsI[i].X * sinR +
                    ptsI[i].Y * cosR +
                    tr.Y;

                double dx = x - ptsJ[i].X;
                double dy = y - ptsJ[i].Y;

                error += dx * dx + dy * dy;
            }

            return Math.Sqrt(error / ptsI.Count);
        }
        #endregion

        #region 3d
        internal static List<TriMesh> DivideParts(TriMesh mesh)
        {
            mesh.ComputeGeo();

            bool[] visited = Enumerable.Repeat(false, mesh.Triangles.Count()).ToArray();

            for (int i = 0; i < mesh.Triangles.Count(); i++)
            {
                mesh.Triangles[i].FID = -1;
            }

            int curId = -1;

            List<TriMesh> meshes = new List<TriMesh>();

            for (int i = 0; i < mesh.Triangles.Count(); i++)
            {
                if (visited[i])
                {
                    continue;
                }

                Stack<Triangle> stack = new Stack<Triangle>();
                List<Triangle> currentMesh = new List<Triangle>();
                curId++;

                stack.Push(mesh.Triangles[i]);
                visited[i] = true;

                while (stack.Count > 0)
                {
                    Triangle currentTri = stack.Pop();
                    currentTri.FID = curId;
                    currentMesh.Add(currentTri);

                    if (currentTri.IDT0 >= 0)
                    {
                        if (!visited[currentTri.IDT0])
                        {
                            if (currentTri.IDT0 >= 0)
                            {
                                visited[currentTri.IDT0] = true;
                                stack.Push(mesh.Triangles[currentTri.IDT0]);
                            }
                        }
                    }

                    if (currentTri.IDT1 >= 0)
                    {
                        if (!visited[currentTri.IDT1])
                        {
                            if (currentTri.IDT1 >= 0)
                            {
                                visited[currentTri.IDT1] = true;
                                stack.Push(mesh.Triangles[currentTri.IDT1]);
                            }
                        }
                    }

                    if (currentTri.IDT2 >= 0)
                    {
                        if (!visited[currentTri.IDT2])
                        {
                            if (currentTri.IDT2 >= 0)
                            {
                                visited[currentTri.IDT2] = true;
                                stack.Push(mesh.Triangles[currentTri.IDT2]);
                            }
                        }
                    }
                }

                TriMesh fmesh = new TriMesh();
                foreach (Triangle tri in currentMesh)
                {
                    //fmesh.Triangles.Add(tri.Clone());
                    fmesh.Triangles.Add(tri);
                }
                meshes.Add(fmesh);
            }

            return meshes;
        }
        internal static List<MeshFeature> ExtractTubeFeatures(TriMesh mesh)
        {
            mesh.ComputeGeo();

            List<TriMesh> meshes;

            meshes = DivideByPropagation(mesh);
            foreach (TriMesh m in meshes)
            {
                double meanX = m.Triangles.Average(x => Math.Abs(x.N.X));

                List<int> neighboors = new List<int>();
                for (int i = 0; i < m.Triangles.Count(); i++)
                {
                    if (m.Triangles[i].C0 != 0)
                    {
                        if (!neighboors.Contains(mesh.Triangles[m.Triangles[i].IDT0].FID))
                        {
                            neighboors.Add(mesh.Triangles[m.Triangles[i].IDT0].FID);
                        }
                    }
                    if (m.Triangles[i].C1 != 0)
                    {
                        if (!neighboors.Contains(mesh.Triangles[m.Triangles[i].IDT1].FID))
                        {
                            neighboors.Add(mesh.Triangles[m.Triangles[i].IDT1].FID);
                        }
                    }
                    if (m.Triangles[i].C2 != 0)
                    {
                        if (!neighboors.Contains(mesh.Triangles[m.Triangles[i].IDT2].FID))
                        {
                            neighboors.Add(mesh.Triangles[m.Triangles[i].IDT2].FID);
                        }
                    }
                }
                if (m.Triangles.Any())
                {
                    if (neighboors.Contains(m.Triangles.First().FID))
                    {
                        neighboors.Remove(m.Triangles.First().FID);
                    }
                }

                Triangle.FaceType type = Triangle.FaceType.UNDEFINED;

                if (meanX < tolNxyz)
                {
                    double meanY = m.Triangles.Average(x => Math.Abs(x.N.Y));
                    double meanZ = m.Triangles.Average(x => Math.Abs(x.N.Z));

                    bool slot = true;
                    for (int i = 0; i < m.Triangles.Count(); i++)
                    {
                        if (Math.Abs(m.Triangles[i].N.Y) - meanY > tolNxyz ||
                            Math.Abs(m.Triangles[i].N.Z) - meanZ > tolNxyz)
                        {
                            slot = false;
                            i = m.Triangles.Count();
                        }
                    }

                    if (slot)
                    {
                        type = Triangle.FaceType.SLOT;
                    }
                    else
                    {
                        type = Triangle.FaceType.SECTION;
                    }
                }
                else
                {
                    type = Triangle.FaceType.CUT;
                }

                if (type == Triangle.FaceType.SLOT && neighboors.Count != 4)
                {
                    type = Triangle.FaceType.SECTION;
                }

                for (int i = 0; i < m.Triangles.Count(); i++)
                {
                    m.Triangles[i].FTYPE = type;
                }
            }

            // ****************************************************************************************
            // ****************************************************************************************
            // ****************************************************************************************

            List<MeshEdge> c1xSlotEdges = new List<MeshEdge>();
            for (int i = 0; i < mesh.Edges.Count(); i++)
            {
                if (mesh.Triangles[mesh.Edges[i].IDT0].FTYPE == Triangle.FaceType.SLOT ||
                    mesh.Triangles[mesh.Edges[i].IDT1].FTYPE == Triangle.FaceType.SLOT)
                {
                    if (mesh.Edges[i].C == 1)
                    {
                        if (Math.Abs(mesh.Edges[i].V1.Y - mesh.Edges[i].V0.Y) < tol0 &&
                            Math.Abs(mesh.Edges[i].V1.Z - mesh.Edges[i].V0.Z) < tol0)
                        {
                            c1xSlotEdges.Add(mesh.Edges[i]);

                            bool isCut = false;
                            foreach (Triangle tri in mesh.Triangles)
                            {
                                if (!isCut)
                                {
                                    if (InfiniteLineIntersectsTriangle(mesh.Edges[i].V0, mesh.Edges[i].V1, tri))
                                    {
                                        isCut = true;
                                    }
                                }
                            }

                            if (!isCut)
                            {
                                switch (mesh.Edges[i].IDE0)
                                {
                                    case 0:
                                        mesh.Triangles[mesh.Edges[i].IDT0].C0 = 11;
                                        break;

                                    case 1:
                                        mesh.Triangles[mesh.Edges[i].IDT0].C1 = 11;
                                        break;

                                    case 2:
                                        mesh.Triangles[mesh.Edges[i].IDT0].C2 = 11;
                                        break;
                                }

                                switch (mesh.Edges[i].IDE1)
                                {
                                    case 0:
                                        mesh.Triangles[mesh.Edges[i].IDT1].C0 = 11;
                                        break;

                                    case 1:
                                        mesh.Triangles[mesh.Edges[i].IDT1].C1 = 11;
                                        break;

                                    case 2:
                                        mesh.Triangles[mesh.Edges[i].IDT1].C2 = 11;
                                        break;
                                }
                            }
                            else
                            {
                                if (mesh.Triangles[mesh.Edges[i].IDT0].FTYPE == Triangle.FaceType.SLOT)
                                {
                                    foreach (Triangle tri in mesh.Triangles)
                                    {
                                        if (tri.FID == mesh.Triangles[mesh.Edges[i].IDT0].FID)
                                        {
                                            tri.FTYPE = Triangle.FaceType.CUT;
                                        }
                                    }
                                }
                                if (mesh.Triangles[mesh.Edges[i].IDT1].FTYPE == Triangle.FaceType.SLOT)
                                {
                                    foreach (Triangle tri in mesh.Triangles)
                                    {
                                        if (tri.FID == mesh.Triangles[mesh.Edges[i].IDT1].FID)
                                        {
                                            tri.FTYPE = Triangle.FaceType.CUT;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < mesh.Edges.Count(); i++)
            {
                if (mesh.Edges[i].C == 1)
                {
                    bool propagate = false;

                    if (mesh.Triangles[mesh.Edges[i].IDT0].FTYPE == Triangle.FaceType.CUT &&
                        mesh.Triangles[mesh.Edges[i].IDT1].FTYPE == Triangle.FaceType.CUT)
                    {
                        propagate = true;
                    }

                    if (mesh.Triangles[mesh.Edges[i].IDT0].FTYPE == Triangle.FaceType.SECTION &&
                        mesh.Triangles[mesh.Edges[i].IDT1].FTYPE == Triangle.FaceType.SECTION)
                    {
                        propagate = true;
                    }

                    if (propagate)
                    {
                        switch (mesh.Edges[i].IDE0)
                        {
                            case 0:
                                mesh.Triangles[mesh.Edges[i].IDT0].C0 = 11;
                                break;

                            case 1:
                                mesh.Triangles[mesh.Edges[i].IDT0].C1 = 11;
                                break;

                            case 2:
                                mesh.Triangles[mesh.Edges[i].IDT0].C2 = 11;
                                break;
                        }

                        switch (mesh.Edges[i].IDE1)
                        {
                            case 0:
                                mesh.Triangles[mesh.Edges[i].IDT1].C0 = 11;
                                break;

                            case 1:
                                mesh.Triangles[mesh.Edges[i].IDT1].C1 = 11;
                                break;

                            case 2:
                                mesh.Triangles[mesh.Edges[i].IDT1].C2 = 11;
                                break;
                        }
                    }
                }
            }


            // ****************************************************************************************
            // ****************************************************************************************
            // ****************************************************************************************

            meshes = DivideByPropagation(mesh);

            List<MeshFeature> features = new List<MeshFeature>();
            foreach (TriMesh m in meshes)
            {
                features.Add(new MeshFeature(m));
            }

            foreach (MeshFeature f in features)
            {
                f.Mesh.ComputeGeo();
            }

            return features;
        }
        internal static List<MeshFeature> ExtractPlateFeatures(TriMesh mesh)
        {
            mesh.ComputeGeo();
            List<TriMesh> meshes;

            meshes = DivideByPropagation(mesh);

            foreach (TriMesh m in meshes)
            {
                double meanZ = m.Triangles.Average(x => x.N.Z);

                if (meanZ >= 1f - tolNz)
                {
                    for (int i = 0; i < m.Triangles.Count(); i++)
                    {
                        m.Triangles[i].FTYPE = Triangle.FaceType.TOP;
                    }
                }
                else if (meanZ <= -1f + tolNz)
                {
                    for (int i = 0; i < m.Triangles.Count(); i++)
                    {
                        m.Triangles[i].FTYPE = Triangle.FaceType.BOT;
                    }
                }
                else
                {
                    for (int i = 0; i < m.Triangles.Count(); i++)
                    {
                        m.Triangles[i].FTYPE = Triangle.FaceType.CUT;

                        if (Math.Abs(m.Triangles[i].V0.Z - m.Triangles[i].V1.Z) > tol0)
                        {
                            m.Triangles[i].C0 = 11;
                        }
                        if (Math.Abs(m.Triangles[i].V1.Z - m.Triangles[i].V2.Z) > tol0)
                        {
                            m.Triangles[i].C1 = 11;
                        }
                        if (Math.Abs(m.Triangles[i].V2.Z - m.Triangles[i].V0.Z) > tol0)
                        {
                            m.Triangles[i].C2 = 11;
                        }
                    }
                }
            }

            meshes = DivideByPropagation(mesh);

            List<MeshFeature> features = new List<MeshFeature>();
            foreach (TriMesh m in meshes)
            {
                features.Add(new MeshFeature(m));
            }

            foreach (MeshFeature f in features)
            {
                f.Mesh.ComputeGeo();
            }

            return features;
        }
        internal static List<TriMesh> DivideByPropagation(TriMesh mesh)
        {
            bool[] visited = Enumerable.Repeat(false, mesh.Triangles.Count()).ToArray();

            for (int i = 0; i < mesh.Triangles.Count(); i++)
            {
                mesh.Triangles[i].FID = -1;
            }

            int curId = -1;

            List<TriMesh> meshes = new List<TriMesh>();

            for (int i = 0; i < mesh.Triangles.Count(); i++)
            {
                if (visited[i])
                {
                    continue;
                }

                Stack<Triangle> stack = new Stack<Triangle>();
                List<Triangle> currentMesh = new List<Triangle>();
                curId++;

                stack.Push(mesh.Triangles[i]);
                visited[i] = true;

                while (stack.Count > 0)
                {
                    Triangle currentTri = stack.Pop();
                    currentTri.FID = curId;
                    currentMesh.Add(currentTri);

                    if (currentTri.IDT0 >= 0)
                    {
                        if (!visited[currentTri.IDT0])
                        {
                            if (currentTri.IDT0 >= 0)
                            {
                                if (currentTri.C0 == 2)
                                {
                                    visited[currentTri.IDT0] = true;
                                    stack.Push(mesh.Triangles[currentTri.IDT0]);
                                }
                                else if (currentTri.C0 == 11)
                                {
                                    visited[currentTri.IDT0] = true;
                                    stack.Push(mesh.Triangles[currentTri.IDT0]);
                                }
                            }
                        }
                    }

                    if (currentTri.IDT1 >= 0)
                    {
                        if (!visited[currentTri.IDT1])
                        {
                            if (currentTri.IDT1 >= 0)
                            {
                                if (currentTri.C1 == 2)
                                {
                                    visited[currentTri.IDT1] = true;
                                    stack.Push(mesh.Triangles[currentTri.IDT1]);
                                }
                                else if (currentTri.C1 == 11)
                                {
                                    visited[currentTri.IDT1] = true;
                                    stack.Push(mesh.Triangles[currentTri.IDT1]);
                                }
                            }
                        }
                    }

                    if (currentTri.IDT2 >= 0)
                    {
                        if (!visited[currentTri.IDT2])
                        {
                            if (currentTri.IDT2 >= 0)
                            {
                                if (currentTri.C2 == 2)
                                {
                                    visited[currentTri.IDT2] = true;
                                    stack.Push(mesh.Triangles[currentTri.IDT2]);
                                }
                                else if (currentTri.C2 == 11)
                                {
                                    visited[currentTri.IDT2] = true;
                                    stack.Push(mesh.Triangles[currentTri.IDT2]);
                                }
                            }
                        }
                    }
                }

                TriMesh fmesh = new TriMesh();
                foreach (Triangle tri in currentMesh)
                {
                    //fmesh.Triangles.Add(tri.Clone());
                    fmesh.Triangles.Add(tri);
                }
                meshes.Add(fmesh);
            }

            foreach (TriMesh m in meshes)
            {
                foreach (Triangle tri in m.Triangles)
                {
                    if (tri.C0 == 11)
                    {
                        tri.C0 = 1;
                    }
                    if (tri.C1 == 11)
                    {
                        tri.C1 = 1;
                    }
                    if (tri.C2 == 11)
                    {
                        tri.C2 = 1;
                    }
                }
            }

            return meshes;
        }
        internal static TriMesh TriMeshFromSTL(string stlpath)
        {
            //try
            //{
            TriMesh MeshSTL = new TriMesh();
            Triangle Tri;
            int def = 0;

            if (File.Exists(stlpath))
            {
                if (File.ReadAllText(stlpath).Any(ch => char.IsControl(ch) && ch != '\r' && ch != '\n'))
                {
                    using (BinaryReader br = new BinaryReader(File.OpenRead(stlpath), Encoding.ASCII))
                    {
                        br.ReadBytes(80);
                        br.ReadInt32();
                        while (br.BaseStream.Position < br.BaseStream.Length)
                        {
                            try
                            {
                                Tri = new Triangle();

                                Tri.N = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                                Tri.V0 = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                                Tri.V1 = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                                Tri.V2 = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());

                                Tri.ComputeProperties();

                                BitArray bits = new BitArray(BitConverter.GetBytes(br.ReadUInt16()));

                                MeshSTL.Triangles.Add(Tri);
                            }
                            catch { }
                        }
                    }
                }
            }

            if (!MeshSTL.Triangles.Any())
            {
                StreamReader reader = new StreamReader(stlpath);
                Tri = new Triangle();

                while (reader.Peek() >= 0)
                {
                    string[] input = reader.ReadLine().Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);

                    if (input[0] == "facet" && input[1] == "normal")
                    {
                        Tri = new Triangle();

                        Tri.N[0] = float.Parse(input[2], CultureInfo.InvariantCulture);
                        Tri.N[1] = float.Parse(input[3], CultureInfo.InvariantCulture);
                        Tri.N[2] = float.Parse(input[4], CultureInfo.InvariantCulture);
                        def = 0;
                    }
                    if (input[0] == "vertex")
                    {
                        if (def == 0)
                        {
                            Tri.V0[0] = float.Parse(input[1], CultureInfo.InvariantCulture);
                            Tri.V0[1] = float.Parse(input[2], CultureInfo.InvariantCulture);
                            Tri.V0[2] = float.Parse(input[3], CultureInfo.InvariantCulture);
                            def++;
                        }
                        else if (def == 1)
                        {
                            Tri.V1[0] = float.Parse(input[1], CultureInfo.InvariantCulture);
                            Tri.V1[1] = float.Parse(input[2], CultureInfo.InvariantCulture);
                            Tri.V1[2] = float.Parse(input[3], CultureInfo.InvariantCulture);
                            def++;
                        }
                        else if (def == 2)
                        {
                            Tri.V2[0] = float.Parse(input[1], CultureInfo.InvariantCulture);
                            Tri.V2[1] = float.Parse(input[2], CultureInfo.InvariantCulture);
                            Tri.V2[2] = float.Parse(input[3], CultureInfo.InvariantCulture);
                            def++;

                            Tri.ComputeProperties();

                            MeshSTL.Triangles.Add(Tri);
                            def = 0;
                        }
                    }
                }

            }
            return MeshSTL;
        }
        internal static bool InfiniteLineIntersectsTriangle(Vector3 lineP0, Vector3 lineP1, Triangle tri)
        {
            float dist0 = Vector3.Dot(tri.N, lineP0 - tri.V0);
            float dist1 = Vector3.Dot(tri.N, lineP1 - tri.V0);
            if (Math.Abs(dist0) > tolNxyz * 10f || Math.Abs(dist1) > tolNxyz * 10f) // NOT COPLANAR
            {
                return false;
            }

            Vector3 u = tri.V1 - tri.V0;
            u.Normalize();
            Vector3 v = Vector3.Cross(tri.N, u);
            Vector2 t0 = ProjectPointToPlane(tri.V0, tri.V0, u, v);
            Vector2 t1 = ProjectPointToPlane(tri.V1, tri.V0, u, v);
            Vector2 t2 = ProjectPointToPlane(tri.V2, tri.V0, u, v);
            Vector2 l0 = ProjectPointToPlane(lineP0, tri.V0, u, v);
            Vector2 l1 = ProjectPointToPlane(lineP1, tri.V0, u, v);

            Vector2[] triPts = { t0, t1, t2 };
            for (int i = 0; i < 3; i++)
            {
                Vector2 a = triPts[i];
                Vector2 b = triPts[(i + 1) % 3];

                bool cut = false;

                if (!cut && LineSegmentIntersection2D(l0, l1, a, b, out bool overlap))
                {
                    if (overlap) // EDGE OVERLAP
                    {
                        if (i == 0 && tri.C0 == 2)
                        {
                            cut = true;
                        }
                        if (i == 1 && tri.C1 == 2)
                        {
                            cut = true;
                        }
                        if (i == 2 && tri.C2 == 2)
                        {
                            cut = true;
                        }
                    }
                    else
                    {
                        cut = true; // EDGE INTERSECTION
                    }
                }

                if (cut)
                {
                    return true;
                }
            }

            return false;
        }
        internal static Vector2 ProjectPointToPlane(Vector3 p, Vector3 origin, Vector3 u, Vector3 v)
        {
            Vector3 diff = p - origin;
            return new Vector2(Vector3.Dot(diff, u), Vector3.Dot(diff, v));
        }
        internal static bool LineSegmentIntersection2D(Vector2 l0, Vector2 l1, Vector2 a, Vector2 b, out bool overlap)
        {
            overlap = false;

            Vector2 r = l1 - l0;
            Vector2 s = b - a;

            //float rxs = Cross2D(r, s);
            float rxs = r.X * s.Y - r.Y * s.X;
            //float q_pxr = Cross2D((a - l0), r);
            float q_pxr = (a - l0).X * r.Y - (a - l0).Y * r.X;

            if (Math.Abs(rxs) < tol0)
            {
                // Lines are parallel
                if (Math.Abs(q_pxr) < tol0)
                {
                    // Colinear - infinite intersection or overlapping
                    // Check if segments overlap (but our line is infinite so line and segment overlap)
                    // We'll say yes, the infinite line lies on this edge.
                    overlap = true; // arbitrary point on edge
                    return true;
                }
                return false; // Parallel and not colinear
            }

            //float t = Cross2D((a - l0), s) / rxs;
            float t = ((a - l0).X * s.Y - (a - l0).Y * s.X) / rxs;
            float u = q_pxr / rxs;

            // For infinite line, t can be any real number.
            // For segment, u must be between 0 and 1.

            if (u >= tol0 && u <= 1 - tol0)
            {
                //intersection = l0 + t * r;
                return true;
            }

            return false;
        }
        #endregion

        public class TriMesh
        {
            #region fields
            public List<Triangle> Triangles = new List<Triangle>();
            public List<MeshEdge> Edges = new List<MeshEdge>();
            #endregion

            #region methods
            internal void ComputeGeo()
            {
                bool[] freeEdge = new bool[3 * Triangles.Count()];
                for (int i = 0; i < Triangles.Count(); i++)
                {
                    freeEdge[3 * i] = true;
                    freeEdge[3 * i + 1] = true;
                    freeEdge[3 * i + 2] = true;

                    Triangles[i].C0 = 0;
                    Triangles[i].C1 = 0;
                    Triangles[i].C2 = 0;

                    Triangles[i].IDT0 = -1;
                    Triangles[i].IDT1 = -1;
                    Triangles[i].IDT2 = -1;
                }

                // dimesnion 0: xyz normal
                // dimension 1: ??? lol, can't even understand myself on this one. but it works so ...
                // dimension 2: first and second triangle from each side of the edge
                float[,,] EdgeNormals = new float[3, 6 * Triangles.Count(), 2];

                KD.KdTree<float, int[]> tree = new KD.KdTree<float, int[]>(3, new KdTreeMath.FloatMath(), KD.AddDuplicateBehavior.Skip);

                for (int i = 0; i < Triangles.Count(); i++)
                {
                    // [0] - [1] common edge search
                    float PX = (Triangles[i].V0[0] + Triangles[i].V1[0]) / 2f;
                    float PY = (Triangles[i].V0[1] + Triangles[i].V1[1]) / 2f;
                    float PZ = (Triangles[i].V0[2] + Triangles[i].V1[2]) / 2f;

                    var nodes = tree.RadialSearch(new[] { PX, PY, PZ }, tol0);

                    if (nodes.Any())
                    {
                        freeEdge[3 * i] = false;
                        freeEdge[(nodes[0].Value[0] - nodes[0].Value[0] % 3) / 2 + nodes[0].Value[0] % 3] = false;

                        EdgeNormals[0, nodes[0].Value[0], 1] = Triangles[i].N[0];
                        EdgeNormals[1, nodes[0].Value[0], 1] = Triangles[i].N[1];
                        EdgeNormals[2, nodes[0].Value[0], 1] = Triangles[i].N[2];

                        Vector3 e0 = new Vector3(0f, 0f, 0f);
                        e0[0] = EdgeNormals[0, nodes[0].Value[0], 0];
                        e0[1] = EdgeNormals[1, nodes[0].Value[0], 0];
                        e0[2] = EdgeNormals[2, nodes[0].Value[0], 0];

                        Vector3 e1 = new Vector3(0f, 0f, 0f);
                        e1[0] = EdgeNormals[0, nodes[0].Value[0], 1];
                        e1[1] = EdgeNormals[1, nodes[0].Value[0], 1];
                        e1[2] = EdgeNormals[2, nodes[0].Value[0], 1];

                        float C = Vector3.Dot(e0, e1);
                        C = C / Vector3.Dot(e0, e0);
                        C = C / Vector3.Dot(e1, e1);

                        int c = -1;
                        if (Math.Abs(C) < Ctol)
                        {
                            MeshEdge edg = new MeshEdge();
                            edg.V0 = new Vector3(Triangles[i].V0[0], Triangles[i].V0[1], Triangles[i].V0[2]);
                            edg.V1 = new Vector3(Triangles[i].V1[0], Triangles[i].V1[1], Triangles[i].V1[2]);
                            edg.C = 1;
                            edg.N0 = e0;
                            edg.N1 = e1;

                            edg.IDT0 = i;
                            edg.IDE0 = 0;
                            edg.IDT1 = nodes[0].Value[1];
                            edg.IDE1 = nodes[0].Value[2];

                            Edges.Add(edg);

                            c = 1;
                        }
                        else
                        {
                            c = 2;
                        }

                        Triangles[i].IDT0 = nodes[0].Value[1];
                        Triangles[i].C0 = c;
                        Triangles[i].IDE0 = nodes[0].Value[2];
                        switch (nodes[0].Value[2])
                        {
                            case 0:
                                Triangles[nodes[0].Value[1]].IDT0 = i;
                                Triangles[nodes[0].Value[1]].C0 = c;
                                Triangles[nodes[0].Value[1]].N0 = Triangles[i].N;
                                Triangles[nodes[0].Value[1]].IDE0 = 0;
                                break;

                            case 1:
                                Triangles[nodes[0].Value[1]].IDT1 = i;
                                Triangles[nodes[0].Value[1]].C1 = c;
                                Triangles[nodes[0].Value[1]].N1 = Triangles[i].N;
                                Triangles[nodes[0].Value[1]].IDE1 = 0;
                                break;

                            case 2:
                                Triangles[nodes[0].Value[1]].IDT2 = i;
                                Triangles[nodes[0].Value[1]].C2 = c;
                                Triangles[nodes[0].Value[1]].N2 = Triangles[i].N;
                                Triangles[nodes[0].Value[1]].IDE2 = 0;
                                break;
                        }
                    }
                    else
                    {
                        EdgeNormals[0, 2 * 3 * i, 0] = Triangles[i].N[0];
                        EdgeNormals[1, 2 * 3 * i, 0] = Triangles[i].N[1];
                        EdgeNormals[2, 2 * 3 * i, 0] = Triangles[i].N[2];
                    }
                    tree.Add(new[] { PX, PY, PZ }, new int[3] { 2 * 3 * i, i, 0 });

                    // [1] - [2] common edge search
                    PX = (Triangles[i].V1[0] + Triangles[i].V2[0]) / 2f;
                    PY = (Triangles[i].V1[1] + Triangles[i].V2[1]) / 2f;
                    PZ = (Triangles[i].V1[2] + Triangles[i].V2[2]) / 2f;

                    nodes = tree.RadialSearch(new[] { PX, PY, PZ }, tol0);

                    if (nodes.Any())
                    {
                        freeEdge[3 * i + 1] = false;
                        freeEdge[(nodes[0].Value[0] - nodes[0].Value[0] % 3) / 2 + nodes[0].Value[0] % 3] = false;

                        EdgeNormals[0, nodes[0].Value[0], 1] = Triangles[i].N[0];
                        EdgeNormals[1, nodes[0].Value[0], 1] = Triangles[i].N[1];
                        EdgeNormals[2, nodes[0].Value[0], 1] = Triangles[i].N[2];

                        Vector3 e0 = new Vector3(0f, 0f, 0f);
                        e0[0] = EdgeNormals[0, nodes[0].Value[0], 0];
                        e0[1] = EdgeNormals[1, nodes[0].Value[0], 0];
                        e0[2] = EdgeNormals[2, nodes[0].Value[0], 0];

                        Vector3 e1 = new Vector3(0f, 0f, 0f);
                        e1[0] = EdgeNormals[0, nodes[0].Value[0], 1];
                        e1[1] = EdgeNormals[1, nodes[0].Value[0], 1];
                        e1[2] = EdgeNormals[2, nodes[0].Value[0], 1];

                        float C = Vector3.Dot(e0, e1);
                        C = C / Vector3.Dot(e0, e0);
                        C = C / Vector3.Dot(e1, e1);

                        int c = -1;
                        if (Math.Abs(C) < Ctol)
                        {
                            MeshEdge edg = new MeshEdge();
                            edg.V0 = new Vector3(Triangles[i].V1[0], Triangles[i].V1[1], Triangles[i].V1[2]);
                            edg.V1 = new Vector3(Triangles[i].V2[0], Triangles[i].V2[1], Triangles[i].V2[2]);
                            edg.C = 1;
                            edg.N0 = e0;
                            edg.N1 = e1;

                            edg.IDT0 = i;
                            edg.IDE0 = 1;
                            edg.IDT1 = nodes[0].Value[1];
                            edg.IDE1 = nodes[0].Value[2];

                            Edges.Add(edg);

                            c = 1;
                        }
                        else
                        {
                            c = 2;
                        }

                        Triangles[i].IDT1 = nodes[0].Value[1];
                        Triangles[i].C1 = c;
                        Triangles[i].IDE1 = nodes[0].Value[2];
                        switch (nodes[0].Value[2])
                        {
                            case 0:
                                Triangles[nodes[0].Value[1]].IDT0 = i;
                                Triangles[nodes[0].Value[1]].C0 = c;
                                Triangles[nodes[0].Value[1]].N0 = Triangles[i].N;
                                Triangles[nodes[0].Value[1]].IDE0 = 1;
                                break;

                            case 1:
                                Triangles[nodes[0].Value[1]].IDT1 = i;
                                Triangles[nodes[0].Value[1]].C1 = c;
                                Triangles[nodes[0].Value[1]].N1 = Triangles[i].N;
                                Triangles[nodes[0].Value[1]].IDE1 = 1;
                                break;

                            case 2:
                                Triangles[nodes[0].Value[1]].IDT2 = i;
                                Triangles[nodes[0].Value[1]].C2 = c;
                                Triangles[nodes[0].Value[1]].N2 = Triangles[i].N;
                                Triangles[nodes[0].Value[1]].IDE2 = 1;
                                break;
                        }
                    }
                    else
                    {
                        EdgeNormals[0, 2 * 3 * i + 1, 0] = Triangles[i].N[0];
                        EdgeNormals[1, 2 * 3 * i + 1, 0] = Triangles[i].N[1];
                        EdgeNormals[2, 2 * 3 * i + 1, 0] = Triangles[i].N[2];
                    }
                    tree.Add(new[] { PX, PY, PZ }, new int[3] { 2 * 3 * i + 1, i, 1 });

                    // [2] - [0] common edge search
                    PX = (Triangles[i].V2[0] + Triangles[i].V0[0]) / 2f;
                    PY = (Triangles[i].V2[1] + Triangles[i].V0[1]) / 2f;
                    PZ = (Triangles[i].V2[2] + Triangles[i].V0[2]) / 2f;

                    nodes = tree.RadialSearch(new[] { PX, PY, PZ }, tol0);

                    if (nodes.Any())
                    {
                        freeEdge[3 * i + 2] = false;
                        freeEdge[(nodes[0].Value[0] - nodes[0].Value[0] % 3) / 2 + nodes[0].Value[0] % 3] = false;

                        EdgeNormals[0, nodes[0].Value[0], 1] = Triangles[i].N[0];
                        EdgeNormals[1, nodes[0].Value[0], 1] = Triangles[i].N[1];
                        EdgeNormals[2, nodes[0].Value[0], 1] = Triangles[i].N[2];

                        Vector3 e0 = new Vector3(0f, 0f, 0f);
                        e0[0] = EdgeNormals[0, nodes[0].Value[0], 0];
                        e0[1] = EdgeNormals[1, nodes[0].Value[0], 0];
                        e0[2] = EdgeNormals[2, nodes[0].Value[0], 0];

                        Vector3 e1 = new Vector3(0f, 0f, 0f);
                        e1[0] = EdgeNormals[0, nodes[0].Value[0], 1];
                        e1[1] = EdgeNormals[1, nodes[0].Value[0], 1];
                        e1[2] = EdgeNormals[2, nodes[0].Value[0], 1];

                        float C = Vector3.Dot(e0, e1);
                        C = C / Vector3.Dot(e0, e0);
                        C = C / Vector3.Dot(e1, e1);

                        int c = -1;
                        if (Math.Abs(C) < Ctol)
                        {
                            MeshEdge edg = new MeshEdge();
                            edg.V0 = new Vector3(Triangles[i].V0[0], Triangles[i].V0[1], Triangles[i].V0[2]);
                            edg.V1 = new Vector3(Triangles[i].V2[0], Triangles[i].V2[1], Triangles[i].V2[2]);
                            edg.C = 1;
                            edg.N0 = e0;
                            edg.N1 = e1;

                            edg.IDT0 = i;
                            edg.IDE0 = 2;
                            edg.IDT1 = nodes[0].Value[1];
                            edg.IDE1 = nodes[0].Value[2];

                            Edges.Add(edg);

                            c = 1;
                        }
                        else
                        {
                            c = 2;
                        }

                        Triangles[i].IDT2 = nodes[0].Value[1];
                        Triangles[i].C2 = c;
                        Triangles[i].IDE2 = nodes[0].Value[2];
                        switch (nodes[0].Value[2])
                        {
                            case 0:
                                Triangles[nodes[0].Value[1]].IDT0 = i;
                                Triangles[nodes[0].Value[1]].C0 = c;
                                Triangles[nodes[0].Value[1]].N0 = Triangles[i].N;
                                Triangles[nodes[0].Value[1]].IDE0 = 2;
                                break;

                            case 1:
                                Triangles[nodes[0].Value[1]].IDT1 = i;
                                Triangles[nodes[0].Value[1]].C1 = c;
                                Triangles[nodes[0].Value[1]].N1 = Triangles[i].N;
                                Triangles[nodes[0].Value[1]].IDE1 = 2;
                                break;

                            case 2:
                                Triangles[nodes[0].Value[1]].IDT2 = i;
                                Triangles[nodes[0].Value[1]].C2 = c;
                                Triangles[nodes[0].Value[1]].N2 = Triangles[i].N;
                                Triangles[nodes[0].Value[1]].IDE2 = 2;
                                break;
                        }
                    }
                    else
                    {
                        EdgeNormals[0, 2 * 3 * i + 2, 0] = Triangles[i].N[0];
                        EdgeNormals[1, 2 * 3 * i + 2, 0] = Triangles[i].N[1];
                        EdgeNormals[2, 2 * 3 * i + 2, 0] = Triangles[i].N[2];
                    }

                    tree.Add(new[] { PX, PY, PZ }, new int[3] { 2 * 3 * i + 2, i, 2 });
                }

                for (int i = 0; i < Triangles.Count(); i++)
                {
                    if (Triangles[i].IDT0 >= 0)
                    {
                        Triangles[i].N0 = Triangles[Triangles[i].IDT0].N;
                    }
                    if (Triangles[i].IDT1 >= 0)
                    {
                        Triangles[i].N1 = Triangles[Triangles[i].IDT1].N;
                    }
                    if (Triangles[i].IDT2 >= 0)
                    {
                        Triangles[i].N2 = Triangles[Triangles[i].IDT2].N;
                    }

                    if (freeEdge[3 * i])
                    {
                        MeshEdge edg = new MeshEdge();
                        edg.V0 = new Vector3(Triangles[i].V0[0], Triangles[i].V0[1], Triangles[i].V0[2]);
                        edg.V1 = new Vector3(Triangles[i].V1[0], Triangles[i].V1[1], Triangles[i].V1[2]);
                        edg.C = 0;
                        edg.N0 = Triangles[i].N;
                        edg.N1 = Triangles[i].N0;
                        edg.IDT0 = i;
                        edg.IDE0 = 0;
                        Edges.Add(edg);
                    }

                    if (freeEdge[3 * i + 1])
                    {
                        MeshEdge edg = new MeshEdge();
                        edg.V0 = new Vector3(Triangles[i].V1[0], Triangles[i].V1[1], Triangles[i].V1[2]);
                        edg.V1 = new Vector3(Triangles[i].V2[0], Triangles[i].V2[1], Triangles[i].V2[2]);
                        edg.C = 0;
                        edg.N0 = Triangles[i].N;
                        edg.N1 = Triangles[i].N1;
                        edg.IDT0 = i;
                        edg.IDE0 = 1;
                        Edges.Add(edg);
                    }

                    if (freeEdge[3 * i + 2])
                    {
                        MeshEdge edg = new MeshEdge();
                        edg.V0 = new Vector3(Triangles[i].V0[0], Triangles[i].V0[1], Triangles[i].V0[2]);
                        edg.V1 = new Vector3(Triangles[i].V2[0], Triangles[i].V2[1], Triangles[i].V2[2]);
                        edg.C = 0;
                        edg.N0 = Triangles[i].N;
                        edg.N1 = Triangles[i].N2;
                        edg.IDT0 = i;
                        edg.IDE0 = 2;
                        Edges.Add(edg);
                    }
                }

                for (int i = 0; i < Edges.Count(); i++)
                {
                    Vector3 v01 = Edges[i].V0 - Edges[i].V1;
                    if (v01.Length < tol0)
                    {
                        Edges.RemoveAt(i);
                        i--;
                    }
                }
            }
            #endregion
        }
        public class Triangle
        {
            internal enum FaceType
            {
                UNDEFINED,

                CUT,

                TOP,
                BOT,

                // TUBE
                SECTION,
                H1,
                H2,
                SLOT
            }

            #region fields
            internal FaceType FTYPE;
            internal int FID = -1;

            public Vector3 V0 = new Vector3(0f, 0f, 0f);
            public Vector3 V1 = new Vector3(0f, 0f, 0f);
            public Vector3 V2 = new Vector3(0f, 0f, 0f);

            internal Vector3 N = new Vector3(0f, 0f, 0f);

            internal Vector3 N0 = new Vector3(0f, 0f, 0f);
            internal Vector3 N1 = new Vector3(0f, 0f, 0f);
            internal Vector3 N2 = new Vector3(0f, 0f, 0f);

            internal int C0 = 0;
            internal int C1 = 0;
            internal int C2 = 0;

            internal int IDT0 = -1;
            internal int IDT1 = -1;
            internal int IDT2 = -1;

            internal int IDE0 = -1;
            internal int IDE1 = -1;
            internal int IDE2 = -1;
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
            internal int C = 0;

            public BevelDefinition Bevel = new BevelDefinition();
            
            internal bool Micro = false;
            #endregion

            #region constructors
            public Edge() { }
            public Edge(double x0, double y0, double x1, double y1, double r = 0, int ns = 0)
            {
                V0 = new Vector3((float)x0, (float)y0, 0f);
                V1 = new Vector3((float)x1, (float)y1, 0f);
                R = r;
                NS = ns;

                Bevel = new BevelDefinition();
                Micro = false;
            }
            #endregion

            #region methods
            internal Edge Clone()
            {
                Edge clone = (Edge)MemberwiseClone();
                clone.Bevel = Bevel.Clone();
                return clone;
            }
            #endregion
        }
        public class MeshEdge : Edge
        {
            internal Vector3 N0 = new Vector3(0f, 0f, 0f);
            internal Vector3 N1 = new Vector3(0f, 0f, 0f);

            internal int IDT0 = -1;
            internal int IDT1 = -1;

            internal int IDE0 = -1;
            internal int IDE1 = -1;
        }
        public enum BevelType { I, V, Y, X, K } //, EVOL }
        public class BevelDefinition
        {
            public BevelType Type = BevelType.I;
            public double A = 0.0;
            public double r = 0.5;
            public double B = 0.0;
            public double Hr = 0.5;

            #region methods
            internal BevelDefinition Clone()
            {
                return (BevelDefinition)this.MemberwiseClone();
            }

            public bool Equals(BevelDefinition other, double tolA = 1E-1, double tolL = 1E-2)
            {
                if (other == null) return false;

                return Type == other.Type
                    && Math.Abs(A - other.A) <= tolA
                    && Math.Abs(Hr - other.Hr) <= tolL
                    && Math.Abs(B - other.B) <= tolA
                    && Math.Abs(r - other.r) <= tolL;
            }

            public bool IsOk(double t = 0.0)
            {
                double atol = 1;
                double ltol = 0.01;
                switch (Type)
                {
                    case BevelType.V:
                        if (Math.Abs(A) < atol)
                        {
                            return false;
                        }
                        break;

                    case BevelType.Y:
                        if (Math.Abs(A) < atol)
                        {
                            return false;
                        }
                        if (r < 0 || r > 1.0)
                        {
                            return false;
                        }
                        break;

                    case BevelType.X:
                        if (Math.Abs(A) < atol)
                        {
                            return false;
                        }
                        if (Math.Abs(B) < atol)
                        {
                            return false;
                        }
                        if (r < 0 || r > 1.0)
                        {
                            return false;
                        }
                        break;

                    case BevelType.K:
                        if (Math.Abs(A) < atol)
                        {
                            return false;
                        }
                        if (Math.Abs(B) < atol)
                        {
                            return false;
                        }
                        if (Hr < 0 || Hr > 1.0)
                        {
                            return false;
                        }
                        if (r - Hr / 2.0 < 0 || r + Hr / 2.0 > 1.0)
                        {
                            return false;
                        }
                        if (r < 0 || r > 1.0)
                        {
                            return false;
                        }
                        break;
                }

                return true;
            }
            #endregion
        }
    }
}
