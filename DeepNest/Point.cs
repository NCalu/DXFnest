namespace DeepNestLib
{
    public class Point
    {
        public bool exact = true;
        public int id;
        public Point(double _x, double _y)
        {
            x = _x;
            y = _y;
        }
        internal Point(Point point)
        {
            this.exact = point.exact;
            this.id = point.id;
            this.marked = point.marked;
            this.x = point.x;
            this.y = point.y;
        }
        public bool marked;
        public double x;
        public double y;
        public Point Clone()
        {
            return new Point(this);
        }
    }
}

