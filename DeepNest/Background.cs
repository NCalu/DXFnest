using ClipperLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DeepNestLib
{
    public class Background
    {
        public DataInfo Data;
        public Action<SheetPlacement> ResponseAction;
        NFP[] Parts;

        public void BackgroundStart(DataInfo data, CancellationToken token)
        {
            this.Data = data;
            var index = data.index;
            var individual = data.individual;

            var parts = individual.placements;
            var rotations = individual.Rotation;
            var ids = data.ids;
            var sources = data.sources;
            var children = data.children;

            for (var i = 0; i < parts.Count; i++)
            {
                parts[i].Rotation = rotations[i];
                parts[i].Id = ids[i];
                parts[i].source = sources[i];
                parts[i].children = children[i];
            }

            for (int i = 0; i < data.sheets.Count; i++)
            {
                data.sheets[i].Id = data.sheetids[i];
                data.sheets[i].source = data.sheetsources[i];
                data.sheets[i].children = data.sheetchildren[i];
            }

            this.Parts = parts.ToArray();

            var placement = placeParts(data.sheets.ToArray(), this.Parts, data.config, token);
            placement.index = data.index;
            ResponseAction(placement);
        }
        public static SheetPlacement placeParts(NFP[] sheets, NFP[] parts, NestConfig config, CancellationToken token)
        {
            if (sheets == null || sheets.Count() == 0) return null;

            //parts = parts.OrderByDescending(p => Math.Abs(GeometryUtil.polygonArea(p))).ToArray();
            sheets = sheets.OrderByDescending(s => Math.Abs(GeometryUtil.polygonArea(s))).ToArray();

            int i, j, k, m, n;
            double totalsheetarea = 0;

            NFP part = null;

            // rotate paths by given rotation
            var rotated = new List<NFP>();
            for (i = 0; i < parts.Length; i++)
            {
                var r = rotatePolygon(parts[i], parts[i].Rotation);
                r.Rotation = parts[i].Rotation;
                r.source = parts[i].source;
                r.Id = parts[i].Id;
                rotated.Add(r);
            }

            parts = rotated.ToArray();

            List<SheetPlacementItem> allplacements = new List<SheetPlacementItem>();

            double totFitness = 0;
            double sheetFitness = 0;

            NFP nfp;
            double sheetarea = -1;
            int totalPlaced = 0;
            int totalParts = parts.Count();

            while (parts.Length > 0)
            {
                if (token.IsCancellationRequested) break;

                // CHECK TO REPEAT LAST
                if (allplacements.Count > 0)
                {
                    bool canRepeat = true;

                    while (canRepeat)
                    {
                        SheetPlacementItem last = allplacements.Last();

                        if (last.sheetplacements.Count == 0)
                        {
                            canRepeat = false;
                            break;
                        }

                        List<int> repeatedIds = new List<int>();
                        if (sheets.First().source == last.sheetSource)
                        {
                            foreach (PlacementItem item in last.sheetplacements)
                            {
                                int repeatedItemId = Array.FindIndex(parts, p => !repeatedIds.Contains(p.Id) && p.source == item.source);
                                if (repeatedItemId == -1)
                                {
                                    canRepeat = false;
                                }
                                else
                                {
                                    repeatedIds.Add(parts[repeatedItemId].Id);
                                }
                            }

                            if (canRepeat)
                            {
                                sheetarea = Math.Abs(GeometryUtil.polygonArea(sheets.First()));
                                totalsheetarea += sheetarea;

                                allplacements.Add(new SheetPlacementItem()
                                {
                                    sheetId = sheets.First().Id,
                                    sheetSource = sheets.First().source.Value,
                                });

                                for (int p = 0; p < last.sheetplacements.Count; p++)
                                {
                                    allplacements.Last().sheetplacements.Add(new PlacementItem()
                                    {
                                        x = last.sheetplacements[p].x,
                                        y = last.sheetplacements[p].y,
                                        id = repeatedIds[p],
                                        rotation = last.sheetplacements[p].rotation,
                                        source = last.sheetplacements[p].source,
                                    });
                                }

                                parts = parts.Where(p => !repeatedIds.Contains(p.Id)).ToArray();
                                sheets = sheets.Skip(1).ToArray();
                                totFitness += sheetFitness;

                                if (parts.Count() == 0 || sheets.Count() == 0)
                                {
                                    canRepeat = false;
                                }
                            }
                        }
                        else
                        {
                            canRepeat = false;
                        }
                    }
                }

                if (sheets.Count() == 0)
                {
                    break;
                }

                sheetFitness = 0.0;

                List<NFP> placed = new List<NFP>();
                List<PlacementItem> placements = new List<PlacementItem>();

                // open a new sheet
                var sheet = sheets.First();
                sheets = sheets.Skip(1).ToArray();
                sheetarea = Math.Abs(GeometryUtil.polygonArea(sheet));
                totalsheetarea += sheetarea;
                sheetFitness += sheetarea; // add 1 for each new sheet opened (lower fitness is better)

                string clipkey = "";
                Dictionary<string, ClipCacheItem> clipCache = new Dictionary<string, ClipCacheItem>();

                var clipper = new Clipper();
                var combinedNfp = new List<List<IntPoint>>();
                var error = false;
                IntPoint[][] clipperSheetNfp = null;
                double? minwidth = null;
                PlacementItem position = null;
                double? minarea = null;
                for (i = 0; i < parts.Length; i++)
                {
                    float prog = 0.66f + 0.34f * (totalPlaced / (float)totalParts);

                    part = parts[i];

                    NFP[] sheetNfp = null;
                    sheetNfp = getInnerNfp(sheet, part, 0, config);

                    if (sheetNfp != null && sheetNfp.Count() > 0)
                    {
                        if (sheetNfp[0].length == 0)
                        {
                            throw new ArgumentException();
                        }
                    }

                    if (sheetNfp == null || sheetNfp.Count() == 0)
                    {
                        continue;
                    }

                    position = null;

                    if (placed.Count == 0)
                    {
                        for (j = 0; j < sheetNfp.Count(); j++)
                        {
                            for (k = 0; k < sheetNfp[j].length; k++)
                            {
                                if (position == null ||
                                   ((sheetNfp[j][k].x - part[0].x) < position.x) ||
                                   (GeometryUtil._almostEqual(sheetNfp[j][k].x - part[0].x, position.x) && ((sheetNfp[j][k].y - part[0].y) < position.y)))
                                {
                                    position = new PlacementItem()
                                    {
                                        x = sheetNfp[j][k].x - part[0].x,
                                        y = sheetNfp[j][k].y - part[0].y,
                                        id = part.Id,
                                        rotation = part.Rotation,
                                        source = part.source.Value
                                    };
                                }
                            }
                        }

                        if (position == null)
                        {
                            throw new Exception("position null");
                            //console.log(sheetNfp);
                        }
                        placements.Add(position);
                        placed.Add(part);
                        totalPlaced++;

                        continue;
                    }

                    clipper = new Clipper();
                    clipperSheetNfp = innerNfpToClipperCoordinates(sheetNfp, config);
                    combinedNfp = new List<List<IntPoint>>();

                    error = false;

                    clipkey = "s:" + part.source + "r:" + part.Rotation;
                    var startindex = 0;
                    if (clipCache.ContainsKey(clipkey))
                    {
                        var prevNfp = clipCache[clipkey].nfpp;
                        clipper.AddPaths(prevNfp.Select(z => z.ToList()).ToList(), PolyType.ptSubject, true);
                        startindex = clipCache[clipkey].index;
                    }

                    for (j = startindex; j < placed.Count; j++)
                    {
                        nfp = getOuterNfp(placed[j], part, 0);

                        if (nfp == null)
                        {
                            error = true;
                            break;
                        }

                        for (m = 0; m < nfp.length; m++)
                        {
                            nfp[m].x += placements[j].x;
                            nfp[m].y += placements[j].y;
                        }

                        if (nfp.children != null && nfp.children.Count > 0)
                        {
                            for (n = 0; n < nfp.children.Count; n++)
                            {
                                for (var o = 0; o < nfp.children[n].length; o++)
                                {
                                    nfp.children[n][o].x += placements[j].x;
                                    nfp.children[n][o].y += placements[j].y;
                                }
                            }
                        }

                        var clipperNfp = nfpToClipperCoordinates(nfp, NestConfig.clipperScale);

                        clipper.AddPaths(clipperNfp.Select(z => z.ToList()).ToList(), PolyType.ptSubject, true);
                    }

                    if (error || !clipper.Execute(ClipType.ctUnion, combinedNfp, PolyFillType.pftNonZero, PolyFillType.pftNonZero))
                    {
                        continue;
                    }

                    clipCache[clipkey] = new ClipCacheItem()
                    {
                        index = placed.Count - 1,
                        nfpp = combinedNfp.Select(z => z.ToArray()).ToArray()
                    };

                    // difference with sheet polygon
                    List<List<IntPoint>> _finalNfp = new List<List<IntPoint>>();
                    clipper = new Clipper();
                    clipper.AddPaths(combinedNfp, PolyType.ptClip, true);
                    clipper.AddPaths(clipperSheetNfp.Select(z => z.ToList()).ToList(), PolyType.ptSubject, true);

                    if (!clipper.Execute(ClipType.ctDifference, _finalNfp, PolyFillType.pftEvenOdd, PolyFillType.pftNonZero))
                    {
                        continue;
                    }

                    if (_finalNfp == null || _finalNfp.Count == 0)
                    {
                        continue;
                    }


                    List<NFP> f = new List<NFP>();
                    for (j = 0; j < _finalNfp.Count; j++)
                    {
                        // back to normal scale
                        f.Add(toNestCoordinates(_finalNfp[j].ToArray(), NestConfig.clipperScale));
                    }
                    var finalNfp = f;

                    minwidth = null;
                    minarea = null;
                    double? minx = null;
                    double? miny = null;
                    NFP nf;
                    double area = 0;
                    PlacementItem shiftvector = null;

                    NFP allpoints = new NFP();
                    for (m = 0; m < placed.Count; m++)
                    {
                        for (n = 0; n < placed[m].length; n++)
                        {
                            allpoints.AddPoint(new Point(placed[m][n].x + placements[m].x, placed[m][n].y + placements[m].y));
                        }
                    }

                    PolygonBounds allbounds = null;
                    PolygonBounds partbounds = null;
                    if (config.placementType == PlacementTypeEnum.GRAVITY || config.placementType == PlacementTypeEnum.BOX)
                    {
                        allbounds = GeometryUtil.getPolygonBounds(allpoints);

                        NFP partpoints = new NFP();
                        for (m = 0; m < part.length; m++)
                        {
                            partpoints.AddPoint(new Point(part[m].x, part[m].y));
                        }
                        partbounds = GeometryUtil.getPolygonBounds(partpoints);
                    }
                    else
                    {
                        allpoints = getHull(allpoints);
                    }
                    for (j = 0; j < finalNfp.Count; j++)
                    {
                        nf = finalNfp[j];

                        for (k = 0; k < nf.length; k++)
                        {
                            shiftvector = new PlacementItem()
                            {
                                id = part.Id,
                                x = nf[k].x - part[0].x,
                                y = nf[k].y - part[0].y,
                                source = part.source.Value,
                                rotation = part.Rotation
                            };

                            PolygonBounds rectbounds = null;

                            if (config.placementType == PlacementTypeEnum.GRAVITY || config.placementType == PlacementTypeEnum.BOX)
                            {
                                NFP poly = new NFP();
                                poly.AddPoint(new Point(allbounds.x, allbounds.y));
                                poly.AddPoint(new Point(allbounds.x + allbounds.width, allbounds.y));
                                poly.AddPoint(new Point(allbounds.x + allbounds.width, allbounds.y + allbounds.height));
                                poly.AddPoint(new Point(allbounds.x, allbounds.y + allbounds.height));

                                poly.AddPoint(new Point(partbounds.x + shiftvector.x, partbounds.y + shiftvector.y));
                                poly.AddPoint(new Point(partbounds.x + partbounds.width + shiftvector.x, partbounds.y + shiftvector.y));
                                poly.AddPoint(new Point(partbounds.x + partbounds.width + shiftvector.x, partbounds.y + partbounds.height + shiftvector.y));
                                poly.AddPoint(new Point(partbounds.x + shiftvector.x, partbounds.y + partbounds.height + shiftvector.y));

                                rectbounds = GeometryUtil.getPolygonBounds(poly);

                                // weigh width more, to help compress in direction of gravity
                                if (config.placementType == PlacementTypeEnum.GRAVITY)
                                {
                                    area = rectbounds.width * 2 + rectbounds.height;
                                }
                                else
                                {
                                    area = rectbounds.width * rectbounds.height;
                                }
                            }
                            else
                            {
                                // must be convex hull
                                var localpoints = Clone(allpoints);

                                for (m = 0; m < part.length; m++)
                                {
                                    localpoints.AddPoint(new Point(part[m].x + shiftvector.x, part[m].y + shiftvector.y));
                                }

                                area = Math.Abs(GeometryUtil.polygonArea(getHull(localpoints)));
                                shiftvector.hull = getHull(localpoints);
                                shiftvector.hullsheet = getHull(sheet);
                            }

                            if (minarea == null ||
                                area < minarea ||
                                (GeometryUtil._almostEqual(minarea, area) && (minx == null || shiftvector.x < minx)) ||
                                (GeometryUtil._almostEqual(minarea, area) && (minx != null && GeometryUtil._almostEqual(shiftvector.x, minx) && shiftvector.y < miny)))
                            {
                                minarea = area;

                                minwidth = rectbounds != null ? rectbounds.width : 0;
                                position = shiftvector;
                                if (minx == null || shiftvector.x < minx)
                                {
                                    minx = shiftvector.x;
                                }
                                if (miny == null || shiftvector.y < miny)
                                {
                                    miny = shiftvector.y;
                                }
                            }
                        }

                    }

                    if (position != null)
                    {
                        placed.Add(part);
                        totalPlaced++;
                        placements.Add(position);
                    }
                    var placednum = placed.Count;
                    for (j = 0; j < allplacements.Count; j++)
                    {
                        placednum += allplacements[j].sheetplacements.Count;
                    }

                    if (token.IsCancellationRequested) i = parts.Length;
                }

                if (!minwidth.HasValue)
                {
                    //fitness = double.NaN;
                }
                else
                {
                    sheetFitness += (minwidth.Value / sheetarea) + minarea.Value;
                }

                if (placements == null || placements.Count <= 0)
                {
                    break; // failed to compute; exit loop
                }

                foreach (var pl in placed)
                {
                    int index = Array.IndexOf(parts, pl);
                    if (index >= 0)
                    {
                        List<NFP> ret = new List<NFP>();
                        for (int p = 0; p < parts.Length; p++)
                        {
                            if (p >= index && p < index + 1) continue;
                            ret.Add(parts[p]);
                        }
                        parts = ret.ToArray();
                    }
                }

                allplacements.Add(new SheetPlacementItem()
                {
                    sheetId = sheet.Id,
                    sheetSource = sheet.source.Value,
                    sheetplacements = placements
                });

                totFitness += sheetFitness;

                if (sheets.Count() == 0)
                {
                    break;
                }
            }

            for (i = 0; i < parts.Count(); i++)
            {
                // penalty
                //totFitness += NestConfig.clipperScale * NestConfig.clipperScale * (Math.Abs(GeometryUtil.polygonArea(parts[i])) / totalsheetarea);
                totFitness += NestConfig.clipperScale * NestConfig.clipperScale * (Math.Abs(GeometryUtil.polygonArea(parts[i])));
            }

            return new SheetPlacement()
            {
                placements = new[] { allplacements.ToList() },
                Fitness = totFitness,
                area = sheetarea,
            };
        }
        public static NFP Clone(NFP nfp)
        {
            NFP newnfp = new NFP();
            newnfp.source = nfp.source;
            for (var i = 0; i < nfp.length; i++)
            {
                newnfp.AddPoint(new Point(nfp[i].x, nfp[i].y));
            }

            if (nfp.children != null && nfp.children.Count > 0)
            {
                newnfp.children = new List<NFP>();
                for (int i = 0; i < nfp.children.Count; i++)
                {
                    var child = nfp.children[i];
                    NFP newchild = new NFP();
                    for (var j = 0; j < child.length; j++)
                    {
                        newchild.AddPoint(new Point(child[j].x, child[j].y));
                    }
                    newnfp.children.Add(newchild);
                }
            }

            return newnfp;
        }
        public static NFP[] Clones(NFP[] nfp, bool inner = false)
        {
            if (!inner)
            {
                return new[] { Clone(nfp.First()) };
            }

            // inner nfp is actually an array of nfps
            List<NFP> newnfp = new List<NFP>();
            for (var i = 0; i < nfp.Count(); i++)
            {
                newnfp.Add(Clone(nfp[i]));
            }

            return newnfp.ToArray();
        }
        public static NFP getFrame(NFP A)
        {
            var bounds = GeometryUtil.getPolygonBounds(A);

            // expand bounds by 10%
            bounds.width *= 1.1;
            bounds.height *= 1.1;
            bounds.x -= 0.5 * (bounds.width - (bounds.width / 1.1));
            bounds.y -= 0.5 * (bounds.height - (bounds.height / 1.1));

            var frame = new NFP();
            frame.push(new Point(bounds.x, bounds.y));
            frame.push(new Point(bounds.x + bounds.width, bounds.y));
            frame.push(new Point(bounds.x + bounds.width, bounds.y + bounds.height));
            frame.push(new Point(bounds.x, bounds.y + bounds.height));

            frame.children = new List<NFP>() { A };
            frame.source = A.source;
            frame.Rotation = 0;

            return frame;
        }
        public static NFP[] getInnerNfp(NFP A, NFP B, int type, NestConfig config)
        {
            var frame = getFrame(A);
            var nfp = getOuterNfp(frame, B, type, true);

            if (nfp == null || nfp.children == null || nfp.children.Count == 0)
            {
                return null;
            }
            List<NFP> holes = new List<NFP>();
            if (A.children != null && A.children.Count > 0)
            {
                for (var i = 0; i < A.children.Count; i++)
                {
                    var hnfp = getOuterNfp(A.children[i], B, 1);
                    if (hnfp != null)
                    {
                        holes.Add(hnfp);
                    }
                }
            }

            if (holes.Count == 0)
            {
                return nfp.children.ToArray();
            }
            var clipperNfp = innerNfpToClipperCoordinates(nfp.children.ToArray(), config);
            var clipperHoles = innerNfpToClipperCoordinates(holes.ToArray(), config);

            List<List<IntPoint>> finalNfp = new List<List<IntPoint>>();
            var clipper = new Clipper();

            clipper.AddPaths(clipperHoles.Select(z => z.ToList()).ToList(), PolyType.ptClip, true);
            clipper.AddPaths(clipperNfp.Select(z => z.ToList()).ToList(), PolyType.ptSubject, true);

            if (!clipper.Execute(ClipType.ctDifference, finalNfp, PolyFillType.pftNonZero, PolyFillType.pftNonZero))
            {
                return nfp.children.ToArray();
            }

            if (finalNfp.Count == 0)
            {
                return null;
            }

            List<NFP> f = new List<NFP>();
            for (var i = 0; i < finalNfp.Count; i++)
            {
                f.Add(toNestCoordinates(finalNfp[i].ToArray(), NestConfig.clipperScale));
            }

            return f.ToArray();

        }
        public static NFP getOuterNfp(NFP A, NFP B, int type, bool inside = false)
        {
            NFP[] nfp = null;

            if (inside || (A.children != null && A.children.Count > 0))
            {
                nfp = NewMinkowskiSum(B, A, type, true, false);
            }
            else
            {
                var Ac = Clipper.ScaleUpPaths(A, NestConfig.clipperScale);
                var Bc = Clipper.ScaleUpPaths(B, NestConfig.clipperScale);

                for (var i = 0; i < Bc.Length; i++)
                {
                    Bc[i].X *= -1;
                    Bc[i].Y *= -1;
                }

                List<List<IntPoint>> solution = Clipper.MinkowskiSum(new List<IntPoint>(Ac), new List<IntPoint>(Bc), true);
                NFP clipperNfp = null;

                double? largestArea = null;
                for (int i = 0; i < solution.Count(); i++)
                {
                    var n = toNestCoordinates(solution[i].ToArray(), NestConfig.clipperScale);
                    var sarea = GeometryUtil.polygonArea(n);
                    if (largestArea == null || largestArea > sarea)
                    {
                        clipperNfp = n;
                        largestArea = sarea;
                    }
                }

                for (var i = 0; i < clipperNfp.length; i++)
                {
                    clipperNfp[i].x += B[0].x;
                    clipperNfp[i].y += B[0].y;
                }
                nfp = new NFP[] { new NFP() { Points = clipperNfp.Points } };
            }

            if (nfp == null || nfp.Length == 0)
            {
                return null;
            }

            NFP nfps = nfp.First();
            if (nfps == null || nfps.Length == 0)
            {
                return null;
            }

            return nfps;
        }
        public static NFP rotatePolygon(NFP polygon, float degrees)
        {
            NFP rotated = new NFP();

            var angle = degrees * Math.PI / 180;
            List<Point> pp = new List<Point>();
            for (var i = 0; i < polygon.length; i++)
            {
                var x = polygon[i].x;
                var y = polygon[i].y;
                var x1 = (x * Math.Cos(angle) - y * Math.Sin(angle));
                var y1 = (x * Math.Sin(angle) + y * Math.Cos(angle));

                pp.Add(new Point(x1, y1));
            }
            rotated.Points = pp.ToArray();

            if (polygon.children != null && polygon.children.Count > 0)
            {
                rotated.children = new List<NFP>(); ;
                for (var j = 0; j < polygon.children.Count; j++)
                {
                    if (polygon.children[j] != null)
                    {
                        rotated.children.Add(rotatePolygon(polygon.children[j], degrees));
                    }
                }
            }

            return rotated;
        }
        public static NFP getHull(NFP polygon)
        {
            double[][] points = new double[polygon.length][];
            for (var i = 0; i < polygon.length; i++)
            {
                points[i] = (new double[] { polygon[i].x, polygon[i].y });
            }

            var hullpoints = polygonHull(points);

            if (hullpoints == null)
            {
                return polygon;
            }

            NFP hull = new NFP();
            for (int i = 0; i < hullpoints.Count(); i++)
            {
                hull.AddPoint(new Point(hullpoints[i][0], hullpoints[i][1]));
            }
            return hull;
        }
        public static double[][] polygonHull(double[][] points)
        {
            int n;
            n = points.Count();
            if ((n) < 3) return null;

            HullInfoPoint[] sortedPoints = new HullInfoPoint[n];
            double[][] flippedPoints = new double[n][];

            for (int i = 0; i < n; ++i) sortedPoints[i] = new HullInfoPoint { x = points[i][0], y = points[i][1], index = i };
            sortedPoints = sortedPoints.OrderBy(x => x.x).ThenBy(z => z.y).ToArray();

            for (int i = 0; i < n; ++i) flippedPoints[i] = new double[] { sortedPoints[i].x, -sortedPoints[i].y };

            var upperIndexes = computeUpperHullIndexes(sortedPoints.Select(z => new double[] { z.x, z.y, z.index }).ToArray());
            var lowerIndexes = computeUpperHullIndexes(flippedPoints);

            // Construct the hull polygon, removing possible duplicate endpoints.
            var skipLeft = lowerIndexes[0] == upperIndexes[0];
            var skipRight = lowerIndexes[lowerIndexes.Length - 1] == upperIndexes[upperIndexes.Length - 1];
            List<double[]> hull = new List<double[]>();

            // Add upper hull in right-to-l order.
            // Then add lower hull in left-to-right order.
            for (int i = upperIndexes.Length - 1; i >= 0; --i)
                hull.Add(points[sortedPoints[upperIndexes[i]].index]);
            //for (int i = +skipLeft; i < lowerIndexes.Length - skipRight; ++i) hull.push(points[sortedPoints[lowerIndexes[i]][2]]);
            for (int i = skipLeft ? 1 : 0; i < lowerIndexes.Length - (skipRight ? 1 : 0); ++i) hull.Add(points[sortedPoints[lowerIndexes[i]].index]);

            return hull.ToArray();
        }
        public static int[] computeUpperHullIndexes(double[][] points)
        {
            Dictionary<int, int> indexes = new Dictionary<int, int>
            {
                { 0, 0 },
                { 1, 1 }
            };
            var n = points.Count();
            var size = 2;

            for (var i = 2; i < n; ++i)
            {
                while (size > 1 && cross(points[indexes[size - 2]], points[indexes[size - 1]], points[i]) <= 0) --size;

                if (!indexes.ContainsKey(size))
                {
                    indexes.Add(size, -1);
                }
                indexes[size++] = i;
            }
            List<int> ret = new List<int>();
            for (int i = 0; i < size; i++)
            {
                ret.Add(indexes[i]);
            }
            return ret.ToArray();

            double cross(double[] a, double[] b, double[] c)
            {
                return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0]);
            }
        }
        public static NFP toNestCoordinates(IntPoint[] polygon, double scale)
        {
            var clone = new List<Point>();
            for (var i = 0; i < polygon.Count(); i++)
            {
                clone.Add(new Point(polygon[i].X / scale, polygon[i].Y / scale));
            }
            return new NFP() { Points = clone.ToArray() };
        }
        public static IntPoint[][] nfpToClipperCoordinates(NFP nfp, double clipperScale)
        {
            List<IntPoint[]> clipperNfp = new List<IntPoint[]>();

            if (nfp.children != null && nfp.children.Count > 0)
            {
                for (var j = 0; j < nfp.children.Count; j++)
                {
                    if (GeometryUtil.polygonArea(nfp.children[j]) < 0)
                    {
                        nfp.children[j].Reverse();
                    }
                    //var childNfp = SvgNest.toClipperCoordinates(nfp.children[j]);
                    var childNfp = Clipper.ScaleUpPaths(nfp.children[j], clipperScale);
                    clipperNfp.Add(childNfp);
                }
            }

            if (GeometryUtil.polygonArea(nfp) > 0)
            {
                nfp.Reverse();
            }


            //var outerNfp = SvgNest.toClipperCoordinates(nfp);

            // clipper js defines holes based on orientation

            var outerNfp = Clipper.ScaleUpPaths(nfp, clipperScale);

            //var cleaned = ClipperLib.Clipper.CleanPolygon(outerNfp, 0.00001*config.clipperScale);

            clipperNfp.Add(outerNfp);
            //var area = Math.abs(ClipperLib.Clipper.Area(cleaned));

            return clipperNfp.ToArray();
        }
        public static IntPoint[][] innerNfpToClipperCoordinates(NFP[] nfp, NestConfig config)
        {
            // inner nfps can be an array of nfps, outer nfps are always singular

            List<IntPoint[]> clipperNfp = new List<IntPoint[]>();
            for (var i = 0; i < nfp.Count(); i++)
            {
                var clip = nfpToClipperCoordinates(nfp[i], NestConfig.clipperScale);
                clipperNfp.AddRange(clip);
                //clipperNfp = clipperNfp.Concat(new[] { clip }).ToList();
            }

            return clipperNfp.ToArray();
        }
        public static NFP[] NewMinkowskiSum(NFP pattern, NFP path, int type, bool useChilds = false, bool takeOnlyBiggestArea = true)
        {
            var ac = Clipper.ScaleUpPaths(pattern, NestConfig.clipperScale);
            List<List<IntPoint>> solution = null;
            if (useChilds)
            {
                var bc = nfpToClipperCoordinates(path, NestConfig.clipperScale);
                for (var i = 0; i < bc.Length; i++)
                {
                    for (int j = 0; j < bc[i].Length; j++)
                    {
                        bc[i][j].X *= -1;
                        bc[i][j].Y *= -1;
                    }
                }

                solution = Clipper.MinkowskiSum(new List<IntPoint>(ac), new List<List<IntPoint>>(bc.Select(z => z.ToList())), true);
            }
            else
            {
                var bc = Clipper.ScaleUpPaths(path, NestConfig.clipperScale);
                for (var i = 0; i < bc.Length; i++)
                {
                    bc[i].X *= -1;
                    bc[i].Y *= -1;
                }
                solution = Clipper.MinkowskiSum(new List<IntPoint>(ac), new List<IntPoint>(bc), true);
            }
            NFP clipperNfp = null;

            double? largestArea = null;
            int largestIndex = -1;

            for (int i = 0; i < solution.Count(); i++)
            {
                var n = toNestCoordinates(solution[i].ToArray(), NestConfig.clipperScale);
                var sarea = Math.Abs(GeometryUtil.polygonArea(n));
                if (largestArea == null || largestArea < sarea)
                {
                    clipperNfp = n;
                    largestArea = sarea;
                    largestIndex = i;
                }
            }
            if (!takeOnlyBiggestArea)
            {
                for (int j = 0; j < solution.Count; j++)
                {
                    if (j == largestIndex) continue;
                    var n = toNestCoordinates(solution[j].ToArray(), NestConfig.clipperScale);
                    if (clipperNfp.children == null)
                        clipperNfp.children = new List<NFP>();
                    clipperNfp.children.Add(n);
                }
            }
            for (var i = 0; i < clipperNfp.Length; i++)
            {

                clipperNfp[i].x *= -1;
                clipperNfp[i].y *= -1;
                clipperNfp[i].x += pattern[0].x;
                clipperNfp[i].y += pattern[0].y;

            }
            if (clipperNfp.children != null)
                foreach (var nFP in clipperNfp.children)
                {
                    for (int j = 0; j < nFP.Length; j++)
                    {

                        nFP.Points[j].x *= -1;
                        nFP.Points[j].y *= -1;
                        nFP.Points[j].x += pattern[0].x;
                        nFP.Points[j].y += pattern[0].y;
                    }
                }
            var res = new[] { clipperNfp };
            return res;
        }
    }

    public class HullInfoPoint
    {
        public double x;
        public double y;
        public int index;
    }
    public class GeometryUtil
    {
        static double TOL = (float)Math.Pow(10, -9);
        public static PolygonBounds getPolygonBounds(NFP _polygon)
        {
            return getPolygonBounds(_polygon.Points);
        }
        public static PolygonBounds getPolygonBounds(Point[] polygon)
        {

            if (polygon == null || polygon.Count() < 3)
            {
                throw new ArgumentException("null");
            }

            var xmin = polygon[0].x;
            var xmax = polygon[0].x;
            var ymin = polygon[0].y;
            var ymax = polygon[0].y;

            for (var i = 1; i < polygon.Length; i++)
            {
                if (polygon[i].x > xmax)
                {
                    xmax = polygon[i].x;
                }
                else if (polygon[i].x < xmin)
                {
                    xmin = polygon[i].x;
                }

                if (polygon[i].y > ymax)
                {
                    ymax = polygon[i].y;
                }
                else if (polygon[i].y < ymin)
                {
                    ymin = polygon[i].y;
                }
            }

            var w = xmax - xmin;
            var h = ymax - ymin;
            //return new rectanglef(xmin, ymin, xmax - xmin, ymax - ymin);
            return new PolygonBounds(xmin, ymin, w, h);
        }
        public static bool _almostEqual(double a, double b, double? tolerance = null)
        {
            if (tolerance == null)
            {
                tolerance = TOL;
            }
            return Math.Abs(a - b) < tolerance;
        }
        public static bool _almostEqual(double? a, double? b, double? tolerance = null)
        {
            return _almostEqual(a.Value, b.Value, tolerance);
        }
        public static double polygonArea(NFP polygon)
        {
            double area = 0;
            int i, j;
            for (i = 0, j = polygon.Points.Length - 1; i < polygon.Points.Length; j = i++)
            {
                area += (polygon.Points[j].x + polygon.Points[i].x) * (polygon.Points[j].y
                    - polygon.Points[i].y);
            }
            return 0.5f * area;
        }
    }
    public class PolygonBounds
    {
        public double x;
        public double y;
        public double width;
        public double height;
        public PolygonBounds(double _x, double _y, double _w, double _h)
        {
            x = _x;
            y = _y;
            width = _w;
            height = _h;
        }
    }
    public class DbCacheKey
    {
        public int? A;
        public int? B;
        public float ARotation;
        public float BRotation;
        public NFP[] nfp;
        public int Type;
    }
    public class ClipCacheItem
    {
        public int index;
        public IntPoint[][] nfpp;
    }
    public class DbCache
    {
        public DbCache(WindowUnk w)
        {
            window = w;
        }
        public bool has(DbCacheKey obj)
        {
            lock (lockobj)
            {
                var key = getKey(obj);
                if (window.nfpCache.ContainsKey(key))
                {
                    return true;
                }
                return false;
            }
        }

        public WindowUnk window;
        public object lockobj = new object();

        string getKey(DbCacheKey obj)
        {
            var key = "A" + obj.A + "B" + obj.B + "Arot" + (int)Math.Round(obj.ARotation * 10000) + "Brot" + (int)Math.Round((obj.BRotation * 10000)) + ";" + obj.Type;
            return key;
        }
        internal void insert(DbCacheKey obj, bool inner = false)
        {
            var key = getKey(obj);
            lock (lockobj)
            {
                if (!window.nfpCache.ContainsKey(key))
                {
                    window.nfpCache.Add(key, Background.Clones(obj.nfp, inner).ToList());
                }
                else
                {
                    throw new Exception("trouble .cache allready has such key");
                }
            }
        }
        public NFP[] find(DbCacheKey obj, bool inner = false)
        {
            lock (lockobj)
            {
                var key = getKey(obj);
                //var key = "A" + obj.A + "B" + obj.B + "Arot" + (int)Math.Round(obj.ARotation) + "Brot" + (int)Math.Round((obj.BRotation));

                //console.log('key: ', key);
                if (window.nfpCache.ContainsKey(key))
                {
                    return Background.Clones(window.nfpCache[key].ToArray(), inner);
                }

                return null;
            }
        }

    }
    public class WindowUnk
    {
        public WindowUnk()
        {
            db = new DbCache(this);
        }
        public Dictionary<string, List<NFP>> nfpCache = new Dictionary<string, List<NFP>>();
        public DbCache db;
    }
}
