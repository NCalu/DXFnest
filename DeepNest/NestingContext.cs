using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DeepNestLib
{
    public class NestingContext
    {
        public Nest Nest { get; private set; }
        public int Iterations { get; private set; } = 0;

        private List<NestItem> Items = new List<NestItem>();

        public NestingContext()
        {
            Nest = new Nest();
            Iterations = 0;
        }

        public void AddSheet(NFP sheet, int qty)
        {
            NestItem sheetItem = new NestItem();
            sheetItem.Polygon = Nest.polygonOffsetDeepNest(sheet, -Nest.Config.sheetSpacing + 0.5 * Nest.Config.spacing).FirstOrDefault();
            List<NFP> children = new List<NFP>();
            if (sheet.children != null)
            {
                foreach (NFP child in sheet.children)
                {
                    children.Add(Nest.polygonOffsetDeepNest(child, Nest.Config.sheetSpacing - 0.5 * Nest.Config.spacing).FirstOrDefault());
                }
            }
            sheetItem.Polygon.children = children;
            sheetItem.IsSheet = true;
            sheetItem.Quanity = qty;
            Items.Add(sheetItem);
        }

        public void AddPart(NFP part, int qty, EnabledRotations rots, double minHrot)
        {
            NestItem partItem = new NestItem();
            partItem.Polygon = Nest.polygonOffsetDeepNest(part, 0.5 * Nest.Config.spacing).FirstOrDefault();
            List<NFP> children = new List<NFP>();
            if (part.children != null)
            {
                foreach (NFP child in part.children)
                {
                    children.Add(Nest.polygonOffsetDeepNest(child, -0.5 * Nest.Config.spacing).FirstOrDefault());
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
            Nest.launchWorkers(Items.ToArray(), token);
            Iterations++;
        }
    }
}
