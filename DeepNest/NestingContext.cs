using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DeepNestLib
{
    public class NestingContext
    {
        public Nest Nest { get; private set; }
        public int Iterations { get; private set; } = 0;

        private List<Item> Items = new List<Item>();

        public NestingContext()
        {
            Nest = new Nest();
            Iterations = 0;
        }

        public void AddSheet(NFP sheet, int qty)
        {
            Item sheetItem = new Item();
            sheetItem.Polygon = Nest.PolygonOffsetDeepNest(sheet, -Nest.Config.sheetSpacing + 0.5 * Nest.Config.spacing).FirstOrDefault();
            List<NFP> children = new List<NFP>();
            if (sheet.children != null)
            {
                foreach (NFP child in sheet.children)
                {
                    children.Add(Nest.PolygonOffsetDeepNest(child, 0.5 * Nest.Config.spacing).FirstOrDefault());
                    if (child.children != null)
                    {
                        List<NFP> subChildren = new List<NFP>();
                        foreach (NFP subChild in child.children)
                        {
                            subChildren.Add(Nest.PolygonOffsetDeepNest(subChild, -0.5 * Nest.Config.spacing).FirstOrDefault());
                        }
                        children.Last().children = subChildren;
                    }
                }
            }
            sheetItem.Polygon.children = children;
            sheetItem.IsSheet = true;
            sheetItem.Quanity = qty;
            Items.Add(sheetItem);
        }

        public void AddPart(NFP part, int qty, EnabledRotations rots, double minHrot)
        {
            Item partItem = new Item();
            partItem.Polygon = Nest.PolygonOffsetDeepNest(part, 0.5 * Nest.Config.spacing).FirstOrDefault();
            List<NFP> children = new List<NFP>();
            if (part.children != null)
            {
                foreach (NFP child in part.children)
                {
                    children.Add(Nest.PolygonOffsetDeepNest(child, -0.5 * Nest.Config.spacing).FirstOrDefault());
                }
            }
            partItem.Polygon.children = children;
            partItem.IsSheet = false;
            partItem.Quanity = qty;

            partItem.Rots = rots;

            partItem.RotMinHeight = (float)minHrot;

            Items.Add(partItem);
        }

        public void NestIterate(CancellationToken token)
        {
            Nest.LaunchWorkers(Items.ToArray(), token);
            Iterations++;
        }
    }
}
