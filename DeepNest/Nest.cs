using ClipperLib;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DeepNestLib
{
    public class Nest
    {
        GeneticAlgorithm ga;
        public Background background = new Background();
        public SheetPlacement nests = null;
        public static NestConfig Config = new NestConfig();
        public Nest() { }
        public static NFP[] polygonOffsetDeepNest(NFP polygon, double offset)
        {
            if (offset == 0) return new[] { polygon };
            List<IntPoint> p = ToClipper(polygon).ToList();

            ClipperOffset co = new ClipperOffset(3, 0.01 * offset * NestConfig.clipperScale);
            co.AddPath(p, JoinType.jtSquare, EndType.etClosedPolygon);
            List<List<IntPoint>> newpaths = new List<List<IntPoint>>();
            co.Execute(ref newpaths, offset * NestConfig.clipperScale);

            List<NFP> result = new List<NFP>();
            for (var i = 0; i < newpaths.Count; i++)
            {
                result.Add(clipperToNFP(newpaths[i]));
            }

            return result.ToArray();
        }
        public static IntPoint[] ToClipper(NFP polygon)
        {
            var d = Clipper.ScaleUpPaths(polygon, NestConfig.clipperScale);
            return d.ToArray();
        }
        public static NFP clipperToNFP(IList<IntPoint> polygon)
        {
            List<Point> ret = new List<Point>();

            for (var i = 0; i < polygon.Count; i++)
            {
                ret.Add(new Point(polygon[i].X / NestConfig.clipperScale, polygon[i].Y / NestConfig.clipperScale));
            }

            return new NFP() { Points = ret.ToArray() };
        }
        public static NFP CloneTree(NFP tree)
        {
            if (tree == null) return null;
            NFP newtree = new NFP();
            foreach (var t in tree.Points)
            {
                newtree.AddPoint(new Point(t.x, t.y) { exact = t.exact });
            }

            if (tree.children != null && tree.children.Count > 0)
            {
                newtree.children = new List<NFP>();
                foreach (var c in tree.children)
                {
                    newtree.children.Add(CloneTree(c));
                }

            }

            return newtree;
        }
        public void ResponseProcessor(SheetPlacement payload)
        {
            if (ga == null)
            {
                return;
            }

            ga.Population[payload.index].processing = null;
            ga.Population[payload.index].fitness = payload.Fitness;

            if (this.nests == null || this.nests.Fitness > payload.Fitness)
            {
                this.nests = payload;
            }
        }
        public void launchWorkers(NestItem[] parts, CancellationToken token)
        {
            background.ResponseAction = ResponseProcessor;

            if (ga == null)
            {
                ga = new GeneticAlgorithm(parts, Config);
            }

            var finished = true;
            for (int i = 0; i < ga.Population.Count; i++)
            {
                if (ga.Population[i].fitness == null)
                {
                    finished = false;
                    break;
                }
            }

            if (finished)
            {
                ga.NewGeneration(Config);
            }

            var running = ga.Population.Where((p) => { return p.processing != null; }).Count();

            List<NFP> sheets = new List<NFP>();
            List<int> sheetids = new List<int>();
            List<int> sheetsources = new List<int>();
            List<List<NFP>> sheetchildren = new List<List<NFP>>();
            int sid = 0;

            for (int i = 0; i < parts.Count(); i++)
            {
                if (parts[i].IsSheet)
                {
                    NFP nfp = parts[i].Polygon;
                    for (int j = 0; j < parts[i].Quanity; j++)
                    {
                        NFP cln = CloneTree(nfp);
                        cln.Id = sid;
                        cln.source = nfp.source;

                        sheets.Add(cln);
                        sheetids.Add(sid);
                        sheetsources.Add(i);
                        sheetchildren.Add(nfp.children);
                        sid++;
                    }
                }
            }

            for (int i = 0; i < ga.Population.Count; i++)
            {
                if (running < 1 && ga.Population[i].processing == null && ga.Population[i].fitness == null)
                {
                    ga.Population[i].processing = true;

                    // hash values on arrays don't make it across ipc, store them in an array and reassemble on the other side....
                    List<int> ids = new List<int>();
                    List<int> sources = new List<int>();
                    List<List<NFP>> children = new List<List<NFP>>();

                    for (int j = 0; j < ga.Population[i].placements.Count; j++)
                    {
                        var pid = ga.Population[i].placements[j].Id;
                        var source = ga.Population[i].placements[j].source;
                        var child = ga.Population[i].placements[j].children;
                        ids.Add(pid);
                        sources.Add(source.Value);
                        children.Add(child);
                    }

                    DataInfo data = new DataInfo()
                    {
                        index = i,
                        sheets = sheets,
                        sheetids = sheetids.ToArray(),
                        sheetsources = sheetsources.ToArray(),
                        sheetchildren = sheetchildren,
                        individual = ga.Population[i],
                        config = Config,
                        ids = ids.ToArray(),
                        sources = sources.ToArray(),
                        children = children

                    };

                    background.BackgroundStart(data, token);
                    running++;
                }
            }
        }
    }

    public class DataInfo
    {
        public int index;
        public List<NFP> sheets;
        public int[] sheetids;
        public int[] sheetsources;
        public List<List<NFP>> sheetchildren;
        public PopulationItem individual;
        public NestConfig config;
        public int[] ids;
        public int[] sources;
        public List<List<NFP>> children;
    }
    public class PolygonTreeItem
    {
        public NFP Polygon;
        public PolygonTreeItem Parent;
        public List<PolygonTreeItem> Childs = new List<PolygonTreeItem>();
    }
    public enum PlacementTypeEnum
    {
        BOX, GRAVITY, SQUEEZE
    }
    public class PopulationItem
    {
        public object processing = null;

        public double? fitness;

        public float[] Rotation;
        public List<NFP> placements;
    }
    public class SheetPlacementItem
    {
        public int sheetId;
        public int sheetSource;

        public List<PlacementItem> sheetplacements = new List<PlacementItem>();
        //public List<PlacementItem> placements = new List<PlacementItem>();
    }
    public class PlacementItem
    {
        //public List<List<IntPoint>> nfp;
        public int id;
        public NFP hull;
        public NFP hullsheet;

        public float rotation;
        public double x;
        public double y;
        public int source;
    }
    public class SheetPlacement
    {
        public double? Fitness;

        public float[] Rotation;
        public List<SheetPlacementItem>[] placements;

        public NFP[] paths;
        public double area;
        internal int index;
    }
    public enum EnabledRotations { NONE, BY_90, BY_180, ANY, PAVE_0_90 }
    public class NestItem
    {
        public NFP Polygon;
        public int Quanity;
        public bool IsSheet;
        public EnabledRotations Rots = EnabledRotations.NONE;
        public float RotMinHeight = 0;
    }
}

