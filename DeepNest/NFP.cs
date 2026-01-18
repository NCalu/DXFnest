using System.Collections.Generic;
using System.Linq;

namespace DeepNestLib
{
    public class NFP
    {
        public NFP()
        {
            Points = new Point[] { };
        }
        public void AddPoint(Point point)
        {
            var list = Points.ToList();
            list.Add(point);
            Points = list.ToArray();
        }

        public void Reverse()
        {
            Points = Points.Reverse().ToArray();
        }

        public double x { get; set; }
        public double y { get; set; }

        public double WidthCalculated
        {
            get
            {
                var maxx = Points.Max(z => z.x);
                var minx = Points.Min(z => z.x);

                return maxx - minx;
            }
        }

        public double HeightCalculated
        {
            get
            {
                var maxy = Points.Max(z => z.y);
                var miny = Points.Min(z => z.y);
                return maxy - miny;
            }
        }

        public Point this[int ind]
        {
            get
            {
                return Points[ind];
            }
        }

        public List<NFP> children;

        public int Length
        {
            get
            {
                return Points.Length;
            }
        }

        public int length
        {
            get
            {
                return Points.Length;
            }
        }

        public int Id;

        public double? offsetx;
        public double? offsety;
        public int? source = null;
        public float Rotation;

        public Point[] Points;

        internal void push(Point pt)
        {
            List<Point> points = new List<Point>();
            if (Points == null)
            {
                Points = new Point[] { };
            }
            points.AddRange(Points);
            points.Add(pt);
            Points = points.ToArray();

        }
    }
}
