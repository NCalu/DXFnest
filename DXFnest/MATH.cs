using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NCnetic.Cam
{
    #region util
    public static class MATH
    {
        public static Vector2 GetArcCenter(Vector2 p0, Vector2 p1, Vector2 p2)
        {
            Vector2 mid01 = (p0 + p1) * 0.5f;
            Vector2 mid12 = (p1 + p2) * 0.5f;
            Vector2 d01 = p1 - p0;
            Vector2 d12 = p2 - p1;
            Vector2 perp01 = new Vector2(-d01.Y, d01.X);
            Vector2 perp12 = new Vector2(-d12.Y, d12.X);

            float det = perp01.X * (-perp12.Y) - perp01.Y * (-perp12.X);

            float dx = mid12.X - mid01.X;
            float dy = mid12.Y - mid01.Y;
            float t = (dx * (-perp12.Y) - dy * (-perp12.X)) / det;

            return mid01 + perp01 * t;
        }
        public static Vector3 ClosestFromPoint(Vector3 v0, Vector3 v1, Vector3 p)
        {
            Vector3 v01 = v1 - v0;
            Vector3 v0p = p - v0;

            float l = Vector3.Dot(v01, v01);
            if (l == 0)
            {
                return v0;
            }

            float t = Vector3.Dot(v0p, v01) / l;
            if (t < 0)
            {
                t = 0;
            }
            else if (t > 1)
            {
                t = 1;
            }

            return v0 + t * v01;
        }
        public static List<Vector2> GetConvexHull(List<Vector2> closed)
        {
            if (closed == null || closed.Count < 3)
                return new List<Vector2>(closed);

            List<Vector2> points = closed
                .OrderBy(p => p.X)
                .ThenBy(p => p.Y)
                .ToList();

            List<Vector2> lower = new List<Vector2>();
            foreach (Vector2 p in points)
            {
                while (lower.Count >= 2)
                {
                    Vector2 a = lower[lower.Count - 2];
                    Vector2 b = lower[lower.Count - 1];
                    if (Vector2.Cross(b - a, p - b) <= 0)
                        lower.RemoveAt(lower.Count - 1);
                    else
                        break;
                }
                lower.Add(p);
            }

            List<Vector2> upper = new List<Vector2>();
            for (int i = points.Count - 1; i >= 0; i--)
            {
                Vector2 p = points[i];
                while (upper.Count >= 2)
                {
                    Vector2 a = upper[upper.Count - 2];
                    Vector2 b = upper[upper.Count - 1];
                    if (Vector2.Cross(b - a, p - b) <= 0)
                        upper.RemoveAt(upper.Count - 1);
                    else
                        break;
                }
                upper.Add(p);
            }

            lower.RemoveAt(lower.Count - 1);
            upper.RemoveAt(upper.Count - 1);

            lower.AddRange(upper);
            return lower;
        }
    }
    #endregion

    #region triangulation
    public class Triangulation
    {
        public static List<int> Tessellate(IList<double> data, IList<int> holeIndices)
        {
            var hasHoles = holeIndices.Count > 0;
            var outerLen = hasHoles ? holeIndices[0] * 2 : data.Count;
            var outerNode = LinkedList(data, 0, outerLen, true);
            var triangles = new List<int>();

            if (outerNode == null)
            {
                return triangles;
            }

            if (hasHoles)
            {
                outerNode = EliminateHoles(data, holeIndices, outerNode);
                //if (outerNode == null)
                //{
                //    return triangles;
                //}
            }

            var minX = double.MaxValue;
            var minY = double.MaxValue;
            var maxX = double.MinValue;
            var maxY = double.MinValue;
            var invSize = 0.0;

            // if the shape is not too simple, we'll use z-order curve hash later; calculate polygon bbox
            if (data.Count > 80 * 2)
            {
                for (int i = 0; i < outerLen; i += 2)
                {
                    double x = data[i];
                    double y = data[i + 1];

                    if (x < minX)
                    {
                        minX = x;
                    }

                    if (y < minY)
                    {
                        minY = y;
                    }

                    if (x > maxX)
                    {
                        maxX = x;
                    }

                    if (y > maxY)
                    {
                        maxY = y;
                    }
                }

                // minX, minY and invSize are later used to transform coords into integers for z-order calculation
                invSize = Math.Max(maxX - minX, maxY - minY);
                invSize = invSize != 0 ? 1 / invSize : 0;
            }

            EarcutLinked(outerNode, triangles, minX, minY, invSize, 0);

            return triangles;
        }

        // Creates a circular doubly linked list from polygon points in the specified winding order.
        static Node LinkedList(IList<double> data, int start, int end, bool clockwise)
        {
            var last = default(Node);

            if (clockwise == (SignedArea(data, start, end) > 0))
            {
                for (int i = start; i < end; i += 2)
                {
                    last = InsertNode(i, data[i], data[i + 1], last);
                }
            }
            else
            {
                for (int i = end - 2; i >= start; i -= 2)
                {
                    last = InsertNode(i, data[i], data[i + 1], last);
                }
            }

            if (last != null && Equals(last, last.next))
            {
                RemoveNode(last);
                last = last.next;
            }

            return last;
        }

        // eliminate colinear or duplicate points
        static Node FilterPoints(Node start, Node end = null)
        {
            if (start == null)
            {
                return start;
            }

            if (end == null)
            {
                end = start;
            }

            var p = start;
            bool again;

            do
            {
                again = false;

                if (!p.steiner && (Equals(p, p.next) || Area(p.prev, p, p.next) == 0))
                {
                    RemoveNode(p);
                    p = end = p.prev;
                    if (p == p.next)
                    {
                        break;
                    }

                    again = true;

                }
                else
                {
                    p = p.next;
                }
            } while (again || p != end);

            return end;
        }

        // main ear slicing loop which triangulates a polygon (given as a linked list)
        static void EarcutLinked(Node ear, IList<int> triangles, double minX, double minY, double invSize, int pass = 0)
        {
            if (ear == null)
            {
                return;
            }

            // interlink polygon nodes in z-order
            if (pass == 0 && invSize != 0)
            {
                IndexCurve(ear, minX, minY, invSize);
            }

            var stop = ear;
            Node prev;
            Node next;

            // iterate through ears, slicing them one by one
            while (ear.prev != ear.next)
            {
                prev = ear.prev;
                next = ear.next;

                if (invSize != 0 ? IsEarHashed(ear, minX, minY, invSize) : IsEar(ear))
                {
                    // cut off the triangle
                    triangles.Add(prev.i / 2);
                    triangles.Add(ear.i / 2);
                    triangles.Add(next.i / 2);

                    RemoveNode(ear);

                    // skipping the next vertex leads to less sliver triangles
                    ear = next.next;
                    stop = next.next;

                    continue;
                }

                ear = next;

                // if we looped through the whole remaining polygon and can't find any more ears
                if (ear == stop)
                {
                    // try filtering points and slicing again
                    if (pass == 0)
                    {
                        EarcutLinked(FilterPoints(ear), triangles, minX, minY, invSize, 1);

                        // if this didn't work, try curing all small self-intersections locally
                    }
                    else if (pass == 1)
                    {
                        ear = CureLocalIntersections(ear, triangles);
                        EarcutLinked(ear, triangles, minX, minY, invSize, 2);

                        // as a last resort, try splitting the remaining polygon into two
                    }
                    else if (pass == 2)
                    {
                        SplitEarcut(ear, triangles, minX, minY, invSize);
                    }

                    break;
                }
            }
        }

        // check whether a polygon node forms a valid ear with adjacent nodes
        static bool IsEar(Node ear)
        {
            var a = ear.prev;
            var b = ear;
            var c = ear.next;

            if (Area(a, b, c) >= 0)
            {
                return false; // reflex, can't be an ear
            }

            // now make sure we don't have other points inside the potential ear
            var p = ear.next.next;

            while (p != ear.prev)
            {
                if (PointInTriangle(a.x, a.y, b.x, b.y, c.x, c.y, p.x, p.y) &&
                    Area(p.prev, p, p.next) >= 0)
                {
                    return false;
                }

                p = p.next;
            }

            return true;
        }

        static bool IsEarHashed(Node ear, double minX, double minY, double invSize)
        {
            var a = ear.prev;
            var b = ear;
            var c = ear.next;

            if (Area(a, b, c) >= 0)
            {
                return false; // reflex, can't be an ear
            }

            // triangle bbox; min & max are calculated like this for speed
            var minTX = a.x < b.x ? (a.x < c.x ? a.x : c.x) : (b.x < c.x ? b.x : c.x);
            var minTY = a.y < b.y ? (a.y < c.y ? a.y : c.y) : (b.y < c.y ? b.y : c.y);
            var maxTX = a.x > b.x ? (a.x > c.x ? a.x : c.x) : (b.x > c.x ? b.x : c.x);
            var maxTY = a.y > b.y ? (a.y > c.y ? a.y : c.y) : (b.y > c.y ? b.y : c.y);

            // z-order range for the current triangle bbox;
            var minZ = ZOrder(minTX, minTY, minX, minY, invSize);
            var maxZ = ZOrder(maxTX, maxTY, minX, minY, invSize);

            var p = ear.prevZ;
            var n = ear.nextZ;

            // look for points inside the triangle in both directions
            while (p != null && p.z >= minZ && n != null && n.z <= maxZ)
            {
                if (p != ear.prev && p != ear.next &&
                    PointInTriangle(a.x, a.y, b.x, b.y, c.x, c.y, p.x, p.y) &&
                    Area(p.prev, p, p.next) >= 0)
                {
                    return false;
                }

                p = p.prevZ;

                if (n != ear.prev && n != ear.next &&
                    PointInTriangle(a.x, a.y, b.x, b.y, c.x, c.y, n.x, n.y) &&
                    Area(n.prev, n, n.next) >= 0)
                {
                    return false;
                }

                n = n.nextZ;
            }

            // look for remaining points in decreasing z-order
            while (p != null && p.z >= minZ)
            {
                if (p != ear.prev && p != ear.next &&
                    PointInTriangle(a.x, a.y, b.x, b.y, c.x, c.y, p.x, p.y) &&
                    Area(p.prev, p, p.next) >= 0)
                {
                    return false;
                }

                p = p.prevZ;
            }

            // look for remaining points in increasing z-order
            while (n != null && n.z <= maxZ)
            {
                if (n != ear.prev && n != ear.next &&
                    PointInTriangle(a.x, a.y, b.x, b.y, c.x, c.y, n.x, n.y) &&
                    Area(n.prev, n, n.next) >= 0)
                {
                    return false;
                }

                n = n.nextZ;
            }

            return true;
        }

        // go through all polygon nodes and cure small local self-intersections
        static Node CureLocalIntersections(Node start, IList<int> triangles)
        {
            var p = start;
            do
            {
                var a = p.prev;
                var b = p.next.next;

                if (!Equals(a, b) && Intersects(a, p, p.next, b) && LocallyInside(a, b) && LocallyInside(b, a))
                {

                    triangles.Add(a.i / 2);
                    triangles.Add(p.i / 2);
                    triangles.Add(b.i / 2);

                    // remove two nodes involved
                    RemoveNode(p);
                    RemoveNode(p.next);

                    p = start = b;
                }
                p = p.next;
            } while (p != start);

            return p;
        }

        // try splitting polygon into two and triangulate them independently
        static void SplitEarcut(Node start, IList<int> triangles, double minX, double minY, double invSize)
        {
            // look for a valid diagonal that divides the polygon into two
            var a = start;
            do
            {
                var b = a.next.next;
                while (b != a.prev)
                {
                    if (a.i != b.i && IsValidDiagonal(a, b))
                    {
                        // split the polygon in two by the diagonal
                        var c = SplitPolygon(a, b);

                        // filter colinear points around the cuts
                        a = FilterPoints(a, a.next);
                        c = FilterPoints(c, c.next);

                        // run earcut on each half
                        EarcutLinked(a, triangles, minX, minY, invSize);
                        EarcutLinked(c, triangles, minX, minY, invSize);
                        return;
                    }
                    b = b.next;
                }
                a = a.next;
            } while (a != start);
        }

        // link every hole into the outer loop, producing a single-ring polygon without holes
        static Node EliminateHoles(IList<double> data, IList<int> holeIndices, Node outerNode)
        {
            var queue = new List<Node>();

            var len = holeIndices.Count;

            for (var i = 0; i < len; i++)
            {
                var start = holeIndices[i] * 2;
                var end = i < len - 1 ? holeIndices[i + 1] * 2 : data.Count;
                var list = LinkedList(data, start, end, false);
                if (list == null) return null;
                if (list == list.next)
                {
                    list.steiner = true;
                }

                queue.Add(GetLeftmost(list));
            }

            queue.Sort(CompareX);

            // process holes from left to right
            for (var i = 0; i < queue.Count; i++)
            {
                EliminateHole(queue[i], outerNode);
                outerNode = FilterPoints(outerNode, outerNode.next);
            }

            return outerNode;
        }

        static int CompareX(Node a, Node b)
        {
            return Math.Sign(a.x - b.x);
        }

        // find a bridge between vertices that connects hole with an outer ring and and link it
        static void EliminateHole(Node hole, Node outerNode)
        {
            outerNode = FindHoleBridge(hole, outerNode);
            if (outerNode != null)
            {
                var b = SplitPolygon(outerNode, hole);
                FilterPoints(b, b.next);
            }
        }

        // David Eberly's algorithm for finding a bridge between hole and outer polygon
        static Node FindHoleBridge(Node hole, Node outerNode)
        {
            var p = outerNode;
            var hx = hole.x;
            var hy = hole.y;
            var qx = double.MinValue;
            Node m = null;

            // find a segment intersected by a ray from the hole's leftmost point to the left;
            // segment's endpoint with lesser x will be potential connection point
            do
            {
                if (hy <= p.y && hy >= p.next.y && p.next.y != p.y)
                {
                    var x = p.x + (hy - p.y) * (p.next.x - p.x) / (p.next.y - p.y);
                    if (x <= hx && x > qx)
                    {
                        qx = x;
                        if (x == hx)
                        {
                            if (hy == p.y)
                            {
                                return p;
                            }

                            if (hy == p.next.y)
                            {
                                return p.next;
                            }
                        }
                        m = p.x < p.next.x ? p : p.next;
                    }
                }
                p = p.next;
            } while (p != outerNode);

            if (m == null)
            {
                return null;
            }

            if (hx == qx)
            {
                return m.prev; // hole touches outer segment; pick lower endpoint
            }

            // look for points inside the triangle of hole point, segment intersection and endpoint;
            // if there are no points found, we have a valid connection;
            // otherwise choose the point of the minimum angle with the ray as connection point

            var stop = m;
            var mx = m.x;
            var my = m.y;
            var tanMin = double.MaxValue;
            double tan;

            p = m.next;

            while (p != stop)
            {
                if (hx >= p.x && p.x >= mx && hx != p.x && PointInTriangle(hy < my ? hx : qx, hy, mx, my, hy < my ? qx : hx, hy, p.x, p.y))
                {

                    tan = Math.Abs(hy - p.y) / (hx - p.x); // tangential

                    if ((tan < tanMin || (tan == tanMin && p.x > m.x)) && LocallyInside(p, hole))
                    {
                        m = p;
                        tanMin = tan;
                    }
                }

                p = p.next;
            }

            return m;
        }

        // interlink polygon nodes in z-order
        static void IndexCurve(Node start, double minX, double minY, double invSize)
        {
            Node p = start;
            do
            {
                if (p.z == null)
                {
                    p.z = ZOrder(p.x, p.y, minX, minY, invSize);
                }

                p.prevZ = p.prev;
                p.nextZ = p.next;
                p = p.next;
            } while (p != start);

            p.prevZ.nextZ = null;
            p.prevZ = null;

            SortLinked(p);
        }

        // Simon Tatham's linked list merge sort algorithm
        static Node SortLinked(Node list)
        {
            int i;
            Node p;
            Node q;
            Node e;
            Node tail;
            int numMerges;
            int pSize;
            int qSize;
            int inSize = 1;

            do
            {
                p = list;
                list = null;
                tail = null;
                numMerges = 0;

                while (p != null)
                {
                    numMerges++;
                    q = p;
                    pSize = 0;
                    for (i = 0; i < inSize; i++)
                    {
                        pSize++;
                        q = q.nextZ;
                        if (q == null)
                        {
                            break;
                        }
                    }
                    qSize = inSize;

                    while (pSize > 0 || (qSize > 0 && q != null))
                    {

                        if (pSize != 0 && (qSize == 0 || q == null || p.z <= q.z))
                        {
                            e = p;
                            p = p.nextZ;
                            pSize--;
                        }
                        else
                        {
                            e = q;
                            q = q.nextZ;
                            qSize--;
                        }

                        if (tail != null)
                        {
                            tail.nextZ = e;
                        }
                        else
                        {
                            list = e;
                        }

                        e.prevZ = tail;
                        tail = e;
                    }

                    p = q;
                }

                tail.nextZ = null;
                inSize *= 2;

            } while (numMerges > 1);

            return list;
        }

        // z-order of a point given coords and inverse of the longer side of data bbox
        static int ZOrder(double x, double y, double minX, double minY, double invSize)
        {
            // coords are transformed into non-negative 15-bit integer range
            int intX = (int)(32767 * (x - minX) * invSize);
            int intY = (int)(32767 * (y - minY) * invSize);

            intX = (intX | (intX << 8)) & 0x00FF00FF;
            intX = (intX | (intX << 4)) & 0x0F0F0F0F;
            intX = (intX | (intX << 2)) & 0x33333333;
            intX = (intX | (intX << 1)) & 0x55555555;

            intY = (intY | (intY << 8)) & 0x00FF00FF;
            intY = (intY | (intY << 4)) & 0x0F0F0F0F;
            intY = (intY | (intY << 2)) & 0x33333333;
            intY = (intY | (intY << 1)) & 0x55555555;

            return intX | (intY << 1);
        }

        // find the leftmost node of a polygon ring
        static Node GetLeftmost(Node start)
        {
            Node p = start;
            Node leftmost = start;
            do
            {
                if (p.x < leftmost.x)
                {
                    leftmost = p;
                }

                p = p.next;
            } while (p != start);

            return leftmost;
        }

        // check if a point lies within a convex triangle
        static bool PointInTriangle(double ax, double ay, double bx, double by, double cx, double cy, double px, double py)
        {
            return (cx - px) * (ay - py) - (ax - px) * (cy - py) >= 0 &&
                   (ax - px) * (by - py) - (bx - px) * (ay - py) >= 0 &&
                   (bx - px) * (cy - py) - (cx - px) * (by - py) >= 0;
        }

        // check if a diagonal between two polygon nodes is valid (lies in polygon interior)
        static bool IsValidDiagonal(Node a, Node b)
        {
            return a.next.i != b.i && a.prev.i != b.i && !IntersectsPolygon(a, b) &&
                   LocallyInside(a, b) && LocallyInside(b, a) && MiddleInside(a, b);
        }

        // signed area of a triangle
        static double Area(Node p, Node q, Node r)
        {
            return (q.y - p.y) * (r.x - q.x) - (q.x - p.x) * (r.y - q.y);
        }

        // check if two points are equal
        static bool Equals(Node p1, Node p2)
        {
            return p1.x == p2.x && p1.y == p2.y;
        }

        // check if two segments intersect
        static bool Intersects(Node p1, Node q1, Node p2, Node q2)
        {
            if ((Equals(p1, q1) && Equals(p2, q2)) ||
                (Equals(p1, q2) && Equals(p2, q1)))
            {
                return true;
            }

            return Area(p1, q1, p2) > 0 != Area(p1, q1, q2) > 0 &&
                   Area(p2, q2, p1) > 0 != Area(p2, q2, q1) > 0;
        }

        // check if a polygon diagonal intersects any polygon segments
        static bool IntersectsPolygon(Node a, Node b)
        {
            Node p = a;
            do
            {
                if (p.i != a.i && p.next.i != a.i && p.i != b.i && p.next.i != b.i &&
                        Intersects(p, p.next, a, b))
                {
                    return true;
                }

                p = p.next;
            } while (p != a);

            return false;
        }

        // check if a polygon diagonal is locally inside the polygon
        static bool LocallyInside(Node a, Node b)
        {
            return Area(a.prev, a, a.next) < 0 ?
                Area(a, b, a.next) >= 0 && Area(a, a.prev, b) >= 0 :
                Area(a, b, a.prev) < 0 || Area(a, a.next, b) < 0;
        }

        // check if the middle point of a polygon diagonal is inside the polygon
        static bool MiddleInside(Node a, Node b)
        {
            var p = a;
            var inside = false;
            var px = (a.x + b.x) / 2;
            var py = (a.y + b.y) / 2;
            do
            {
                if (((p.y > py) != (p.next.y > py)) && p.next.y != p.y &&
                        (px < (p.next.x - p.x) * (py - p.y) / (p.next.y - p.y) + p.x))
                {
                    inside = !inside;
                }

                p = p.next;
            } while (p != a);

            return inside;
        }

        // link two polygon vertices with a bridge; if the vertices belong to the same ring, it splits polygon into two;
        // if one belongs to the outer ring and another to a hole, it merges it into a single ring
        static Node SplitPolygon(Node a, Node b)
        {
            var a2 = new Node(a.i, a.x, a.y);
            var b2 = new Node(b.i, b.x, b.y);
            var an = a.next;
            var bp = b.prev;

            a.next = b;
            b.prev = a;

            a2.next = an;
            an.prev = a2;

            b2.next = a2;
            a2.prev = b2;

            bp.next = b2;
            b2.prev = bp;

            return b2;
        }

        // create a node and optionally link it with previous one (in a circular doubly linked list)
        static Node InsertNode(int i, double x, double y, Node last)
        {
            var p = new Node(i, x, y);

            if (last == null)
            {
                p.prev = p;
                p.next = p;

            }
            else
            {
                p.next = last.next;
                p.prev = last;
                last.next.prev = p;
                last.next = p;
            }
            return p;
        }

        static void RemoveNode(Node p)
        {
            p.next.prev = p.prev;
            p.prev.next = p.next;

            if (p.prevZ != null)
            {
                p.prevZ.nextZ = p.nextZ;
            }

            if (p.nextZ != null)
            {
                p.nextZ.prevZ = p.prevZ;
            }
        }

        class Node
        {
            public int i;
            public double x;
            public double y;

            public int? z;

            public Node prev;
            public Node next;

            public Node prevZ;
            public Node nextZ;

            public bool steiner;

            public Node(int i, double x, double y)
            {
                // vertex index in coordinates array
                this.i = i;

                // vertex coordinates
                this.x = x;
                this.y = y;

                // previous and next vertex nodes in a polygon ring
                this.prev = null;
                this.next = null;

                // z-order curve value
                this.z = null;

                // previous and next nodes in z-order
                this.prevZ = null;
                this.nextZ = null;

                // indicates whether this is a steiner point
                this.steiner = false;
            }
        }

        static double SignedArea(IList<double> data, int start, int end)
        {
            var sum = default(double);

            for (int i = start, j = end - 2; i < end; i += 2)
            {
                sum += (data[j] - data[i]) * (data[i + 1] + data[j + 1]);
                j = i;
            }

            return sum;
        }

        // return a percentage difference between the polygon area and its triangulation area;
        // used to verify correctness of triangulation
        public static double Deviation(IList<double> data, IList<int> holeIndices, IList<int> triangles)
        {
            var hasHoles = holeIndices.Count > 0;
            var outerLen = hasHoles ? holeIndices[0] * 2 : data.Count;

            var polygonArea = Math.Abs(SignedArea(data, 0, outerLen));
            if (hasHoles)
            {
                var len = holeIndices.Count;

                for (var i = 0; i < len; i++)
                {
                    var start = holeIndices[i] * 2;
                    var end = i < len - 1 ? holeIndices[i + 1] * 2 : data.Count;
                    polygonArea -= Math.Abs(SignedArea(data, start, end));
                }
            }

            var trianglesArea = default(double);
            for (var i = 0; i < triangles.Count; i += 3)
            {
                var a = triangles[i] * 2;
                var b = triangles[i + 1] * 2;
                var c = triangles[i + 2] * 2;
                trianglesArea += Math.Abs(
                    (data[a] - data[c]) * (data[b + 1] - data[a + 1]) -
                    (data[a] - data[b]) * (data[c + 1] - data[a + 1]));
            }

            return polygonArea == 0 && trianglesArea == 0 ? 0 :
                Math.Abs((trianglesArea - polygonArea) / polygonArea);
        }
    }
    #endregion

    #region kdtree
    internal class KD
    {
        public enum AddDuplicateBehavior
        {
            Skip,
            Error,
            Update
        }

        public class DuplicateNodeError : Exception
        {
            public DuplicateNodeError()
                : base("Cannot Add Node With Duplicate Coordinates")
            {
            }
        }

        [Serializable]
        public class KdTree<TKey, TValue> : IKdTree<TKey, TValue>
        {
            public KdTree(int dimensions, KdTreeMath.ITypeMath<TKey> typeMath)
            {
                this.dimensions = dimensions;
                this.typeMath = typeMath;
                Count = 0;
            }

            public KdTree(int dimensions, KdTreeMath.ITypeMath<TKey> typeMath, AddDuplicateBehavior addDuplicateBehavior)
                : this(dimensions, typeMath)
            {
                AddDuplicateBehavior = addDuplicateBehavior;
            }

            private int dimensions;

            private KdTreeMath.ITypeMath<TKey> typeMath = null;

            private KdTreeNode<TKey, TValue> root = null;

            public AddDuplicateBehavior AddDuplicateBehavior { get; private set; }

            public bool Add(TKey[] point, TValue value)
            {
                var nodeToAdd = new KdTreeNode<TKey, TValue>(point, value);

                if (root == null)
                {
                    root = new KdTreeNode<TKey, TValue>(point, value);
                }
                else
                {
                    int dimension = -1;
                    KdTreeNode<TKey, TValue> parent = root;

                    do
                    {
                        dimension = (dimension + 1) % dimensions;

                        if (typeMath.AreEqual(point, parent.Point))
                        {
                            switch (AddDuplicateBehavior)
                            {
                                case AddDuplicateBehavior.Skip:
                                    return false;

                                case AddDuplicateBehavior.Error:
                                    throw new DuplicateNodeError();

                                case AddDuplicateBehavior.Update:
                                    parent.Value = value;
                                    return true;

                                default:
                                    throw new Exception("Unexpected AddDuplicateBehavior");
                            }
                        }

                        int compare = typeMath.Compare(point[dimension], parent.Point[dimension]);

                        if (parent[compare] == null)
                        {
                            parent[compare] = nodeToAdd;
                            break;
                        }
                        else
                        {
                            parent = parent[compare];
                        }
                    }
                    while (true);
                }

                Count++;
                return true;
            }

            private void ReaddChildNodes(KdTreeNode<TKey, TValue> removedNode)
            {
                if (removedNode.IsLeaf)
                    return;

                var nodesToReadd = new Queue<KdTreeNode<TKey, TValue>>();

                var nodesToReaddQueue = new Queue<KdTreeNode<TKey, TValue>>();

                if (removedNode.LeftChild != null)
                    nodesToReaddQueue.Enqueue(removedNode.LeftChild);

                if (removedNode.RightChild != null)
                    nodesToReaddQueue.Enqueue(removedNode.RightChild);

                while (nodesToReaddQueue.Count > 0)
                {
                    var nodeToReadd = nodesToReaddQueue.Dequeue();

                    nodesToReadd.Enqueue(nodeToReadd);

                    for (int side = -1; side <= 1; side += 2)
                    {
                        if (nodeToReadd[side] != null)
                        {
                            nodesToReaddQueue.Enqueue(nodeToReadd[side]);

                            nodeToReadd[side] = null;
                        }
                    }
                }

                while (nodesToReadd.Count > 0)
                {
                    var nodeToReadd = nodesToReadd.Dequeue();

                    Count--;
                    Add(nodeToReadd.Point, nodeToReadd.Value);
                }
            }

            public void RemoveAt(TKey[] point)
            {
                if (root == null)
                    return;

                KdTreeNode<TKey, TValue> node;

                if (typeMath.AreEqual(point, root.Point))
                {
                    node = root;
                    root = null;
                    Count--;
                    ReaddChildNodes(node);
                    return;
                }

                node = root;

                int dimension = -1;
                do
                {
                    dimension = (dimension + 1) % dimensions;

                    int compare = typeMath.Compare(point[dimension], node.Point[dimension]);

                    if (node[compare] == null)
                        return;

                    if (typeMath.AreEqual(point, node[compare].Point))
                    {
                        var nodeToRemove = node[compare];
                        node[compare] = null;
                        Count--;

                        ReaddChildNodes(nodeToRemove);
                    }
                    else
                        node = node[compare];
                }
                while (node != null);
            }

            public KdTreeNode<TKey, TValue>[] GetNearestNeighbours(TKey[] point, int count)
            {
                if (count > Count)
                    count = Count;

                if (count < 0)
                {
                    throw new ArgumentException("Number of neighbors cannot be negative");
                }

                if (count == 0)
                    return new KdTreeNode<TKey, TValue>[0];

                var neighbours = new KdTreeNode<TKey, TValue>[count];

                var nearestNeighbours = new NearestNeighbourList<KdTreeNode<TKey, TValue>, TKey>(count, typeMath);

                var rect = HyperRect<TKey>.Infinite(dimensions, typeMath);

                AddNearestNeighbours(root, point, rect, 0, nearestNeighbours, typeMath.MaxValue);

                count = nearestNeighbours.Count;

                var neighbourArray = new KdTreeNode<TKey, TValue>[count];

                for (var index = 0; index < count; index++)
                    neighbourArray[count - index - 1] = nearestNeighbours.RemoveFurtherest();

                return neighbourArray;
            }

            private void AddNearestNeighbours(
                KdTreeNode<TKey, TValue> node,
                TKey[] target,
                HyperRect<TKey> rect,
                int depth,
                NearestNeighbourList<KdTreeNode<TKey, TValue>, TKey> nearestNeighbours,
                TKey maxSearchRadiusSquared)
            {
                if (node == null)
                    return;

                int dimension = depth % dimensions;

                var leftRect = rect.Clone();
                leftRect.MaxPoint[dimension] = node.Point[dimension];

                var rightRect = rect.Clone();
                rightRect.MinPoint[dimension] = node.Point[dimension];

                int compare = typeMath.Compare(target[dimension], node.Point[dimension]);

                var nearerRect = compare <= 0 ? leftRect : rightRect;
                var furtherRect = compare <= 0 ? rightRect : leftRect;

                var nearerNode = compare <= 0 ? node.LeftChild : node.RightChild;
                var furtherNode = compare <= 0 ? node.RightChild : node.LeftChild;

                if (nearerNode != null)
                {
                    AddNearestNeighbours(
                        nearerNode,
                        target,
                        nearerRect,
                        depth + 1,
                        nearestNeighbours,
                        maxSearchRadiusSquared);
                }

                TKey distanceSquaredToTarget;

                TKey[] closestPointInFurtherRect = furtherRect.GetClosestPoint(target, typeMath);
                distanceSquaredToTarget = typeMath.DistanceSquaredBetweenPoints(closestPointInFurtherRect, target);

                if (typeMath.Compare(distanceSquaredToTarget, maxSearchRadiusSquared) <= 0)
                {
                    if (nearestNeighbours.IsCapacityReached)
                    {
                        if (typeMath.Compare(distanceSquaredToTarget, nearestNeighbours.GetFurtherestDistance()) < 0)
                            AddNearestNeighbours(
                                furtherNode,
                                target,
                                furtherRect,
                                depth + 1,
                                nearestNeighbours,
                                maxSearchRadiusSquared);
                    }
                    else
                    {
                        AddNearestNeighbours(
                            furtherNode,
                            target,
                            furtherRect,
                            depth + 1,
                            nearestNeighbours,
                            maxSearchRadiusSquared);
                    }
                }

                distanceSquaredToTarget = typeMath.DistanceSquaredBetweenPoints(node.Point, target);

                if (typeMath.Compare(distanceSquaredToTarget, maxSearchRadiusSquared) <= 0)
                    nearestNeighbours.Add(node, distanceSquaredToTarget);
            }

            public KdTreeNode<TKey, TValue>[] RadialSearch(TKey[] center, TKey radius)
            {
                var nearestNeighbours = new NearestNeighbourList<KdTreeNode<TKey, TValue>, TKey>(typeMath);
                return RadialSearch(center, radius, nearestNeighbours);
            }

            public KdTreeNode<TKey, TValue>[] RadialSearch(TKey[] center, TKey radius, int count)
            {
                var nearestNeighbours = new NearestNeighbourList<KdTreeNode<TKey, TValue>, TKey>(count, typeMath);
                return RadialSearch(center, radius, nearestNeighbours);
            }

            private KdTreeNode<TKey, TValue>[] RadialSearch(TKey[] center, TKey radius, NearestNeighbourList<KdTreeNode<TKey, TValue>, TKey> nearestNeighbours)
            {
                AddNearestNeighbours(
                    root,
                    center,
                    HyperRect<TKey>.Infinite(dimensions, typeMath),
                    0,
                    nearestNeighbours,
                    typeMath.Multiply(radius, radius));

                var count = nearestNeighbours.Count;

                var neighbourArray = new KdTreeNode<TKey, TValue>[count];

                for (var index = 0; index < count; index++)
                    neighbourArray[count - index - 1] = nearestNeighbours.RemoveFurtherest();

                return neighbourArray;
            }

            public int Count { get; private set; }

            public bool TryFindValueAt(TKey[] point, out TValue value)
            {
                var parent = root;
                int dimension = -1;
                do
                {
                    if (parent == null)
                    {
                        value = default(TValue);
                        return false;
                    }
                    else if (typeMath.AreEqual(point, parent.Point))
                    {
                        value = parent.Value;
                        return true;
                    }

                    dimension = (dimension + 1) % dimensions;
                    int compare = typeMath.Compare(point[dimension], parent.Point[dimension]);
                    parent = parent[compare];
                }
                while (true);
            }

            public TValue FindValueAt(TKey[] point)
            {
                if (TryFindValueAt(point, out TValue value))
                    return value;
                else
                    return default(TValue);
            }

            public bool TryFindValue(TValue value, out TKey[] point)
            {
                if (root == null)
                {
                    point = null;
                    return false;
                }

                var nodesToSearch = new Queue<KdTreeNode<TKey, TValue>>();

                nodesToSearch.Enqueue(root);

                while (nodesToSearch.Count > 0)
                {
                    var nodeToSearch = nodesToSearch.Dequeue();

                    if (nodeToSearch.Value.Equals(value))
                    {
                        point = nodeToSearch.Point;
                        return true;
                    }
                    else
                    {
                        for (int side = -1; side <= 1; side += 2)
                        {
                            var childNode = nodeToSearch[side];

                            if (childNode != null)
                                nodesToSearch.Enqueue(childNode);
                        }
                    }
                }

                point = null;
                return false;
            }

            public TKey[] FindValue(TValue value)
            {
                if (TryFindValue(value, out TKey[] point))
                    return point;
                else
                    return null;
            }

            private void AddNodeToStringBuilder(KdTreeNode<TKey, TValue> node, StringBuilder sb, int depth)
            {
                sb.AppendLine(node.ToString());

                for (var side = -1; side <= 1; side += 2)
                {
                    for (var index = 0; index <= depth; index++)
                        sb.Append("\t");

                    sb.Append(side == -1 ? "L " : "R ");

                    if (node[side] == null)
                        sb.AppendLine("");
                    else
                        AddNodeToStringBuilder(node[side], sb, depth + 1);
                }
            }

            public override string ToString()
            {
                if (root == null)
                    return "";

                var sb = new StringBuilder();
                AddNodeToStringBuilder(root, sb, 0);
                return sb.ToString();
            }

            private void AddNodesToList(KdTreeNode<TKey, TValue> node, List<KdTreeNode<TKey, TValue>> nodes)
            {
                if (node == null)
                    return;

                nodes.Add(node);

                for (var side = -1; side <= 1; side += 2)
                {
                    if (node[side] != null)
                    {
                        AddNodesToList(node[side], nodes);
                        node[side] = null;
                    }
                }
            }

            private void SortNodesArray(KdTreeNode<TKey, TValue>[] nodes, int byDimension, int fromIndex, int toIndex)
            {
                for (var index = fromIndex + 1; index <= toIndex; index++)
                {
                    var newIndex = index;

                    while (true)
                    {
                        var a = nodes[newIndex - 1];
                        var b = nodes[newIndex];
                        if (typeMath.Compare(b.Point[byDimension], a.Point[byDimension]) < 0)
                        {
                            nodes[newIndex - 1] = b;
                            nodes[newIndex] = a;
                        }
                        else
                            break;
                    }
                }
            }

            private void AddNodesBalanced(KdTreeNode<TKey, TValue>[] nodes, int byDimension, int fromIndex, int toIndex)
            {
                if (fromIndex == toIndex)
                {
                    Add(nodes[fromIndex].Point, nodes[fromIndex].Value);
                    nodes[fromIndex] = null;
                    return;
                }

                SortNodesArray(nodes, byDimension, fromIndex, toIndex);

                int midIndex = fromIndex + (int)System.Math.Round((toIndex + 1 - fromIndex) / 2f) - 1;

                Add(nodes[midIndex].Point, nodes[midIndex].Value);
                nodes[midIndex] = null;

                int nextDimension = (byDimension + 1) % dimensions;

                if (fromIndex < midIndex)
                    AddNodesBalanced(nodes, nextDimension, fromIndex, midIndex - 1);

                if (toIndex > midIndex)
                    AddNodesBalanced(nodes, nextDimension, midIndex + 1, toIndex);
            }

            public void Balance()
            {
                var nodeList = new List<KdTreeNode<TKey, TValue>>();
                AddNodesToList(root, nodeList);

                Clear();

                AddNodesBalanced(nodeList.ToArray(), 0, 0, nodeList.Count - 1);
            }

            private void RemoveChildNodes(KdTreeNode<TKey, TValue> node)
            {
                for (var side = -1; side <= 1; side += 2)
                {
                    if (node[side] != null)
                    {
                        RemoveChildNodes(node[side]);
                        node[side] = null;
                    }
                }
            }

            public void Clear()
            {
                if (root != null)
                    RemoveChildNodes(root);
            }

            public void SaveToFile(string filename)
            {
                BinaryFormatter formatter = new BinaryFormatter();
                using (FileStream stream = File.Create(filename))
                {
                    formatter.Serialize(stream, this);
                    stream.Flush();
                }
            }

            public static KdTree<TKey, TValue> LoadFromFile(string filename)
            {
                BinaryFormatter formatter = new BinaryFormatter();
                using (FileStream stream = File.Open(filename, FileMode.Open))
                {
                    return (KdTree<TKey, TValue>)formatter.Deserialize(stream);
                }

            }

            public IEnumerator<KdTreeNode<TKey, TValue>> GetEnumerator()
            {
                var left = new Stack<KdTreeNode<TKey, TValue>>();
                var right = new Stack<KdTreeNode<TKey, TValue>>();

                void addLeft(KdTreeNode<TKey, TValue> node)
                {
                    if (node.LeftChild != null)
                    {
                        left.Push(node.LeftChild);
                    }
                }

                void addRight(KdTreeNode<TKey, TValue> node)
                {
                    if (node.RightChild != null)
                    {
                        right.Push(node.RightChild);
                    }
                }

                if (root != null)
                {
                    yield return root;

                    addLeft(root);
                    addRight(root);

                    while (true)
                    {
                        if (left.Any())
                        {
                            var item = left.Pop();

                            addLeft(item);
                            addRight(item);

                            yield return item;
                        }
                        else if (right.Any())
                        {
                            var item = right.Pop();

                            addLeft(item);
                            addRight(item);

                            yield return item;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        [Serializable]
        public class KdTreeNode<TKey, TValue>
        {
            public KdTreeNode()
            {
            }

            public KdTreeNode(TKey[] point, TValue value)
            {
                Point = point;
                Value = value;
            }

            public TKey[] Point;
            public TValue Value = default(TValue);

            internal KdTreeNode<TKey, TValue> LeftChild = null;
            internal KdTreeNode<TKey, TValue> RightChild = null;

            internal KdTreeNode<TKey, TValue> this[int compare]
            {
                get
                {
                    if (compare <= 0)
                        return LeftChild;
                    else
                        return RightChild;
                }
                set
                {
                    if (compare <= 0)
                        LeftChild = value;
                    else
                        RightChild = value;
                }
            }

            public bool IsLeaf
            {
                get
                {
                    return (LeftChild == null) && (RightChild == null);
                }
            }

            public override string ToString()
            {
                var sb = new StringBuilder();

                for (var dimension = 0; dimension < Point.Length; dimension++)
                {
                    sb.Append(Point[dimension].ToString() + "\t");
                }

                if (Value == null)
                    sb.Append("null");
                else
                    sb.Append(Value.ToString());

                return sb.ToString();
            }
        }

        struct ItemPriority<TItem, TPriority>
        {
            public TItem Item;
            public TPriority Priority;
        }

        public class PriorityQueue<TItem, TPriority> : IPriorityQueue<TItem, TPriority>
        {
            public PriorityQueue(int capacity, KdTreeMath.ITypeMath<TPriority> priorityMath)
            {
                if (capacity <= 0)
                    throw new ArgumentException("Capacity must be greater than zero");

                this.capacity = capacity;
                queue = new ItemPriority<TItem, TPriority>[capacity];

                this.priorityMath = priorityMath;
            }

            public PriorityQueue(KdTreeMath.ITypeMath<TPriority> priorityMath)
            {
                this.capacity = 4;
                queue = new ItemPriority<TItem, TPriority>[capacity];

                this.priorityMath = priorityMath;
            }

            private KdTreeMath.ITypeMath<TPriority> priorityMath;

            private ItemPriority<TItem, TPriority>[] queue;

            private int capacity;

            private int count;
            public int Count { get { return count; } }

            private void ExpandCapacity()
            {
                capacity *= 2;

                var newQueue = new ItemPriority<TItem, TPriority>[capacity];

                Array.Copy(queue, newQueue, queue.Length);

                queue = newQueue;
            }

            public void Enqueue(TItem item, TPriority priority)
            {
                if (++count > capacity)
                    ExpandCapacity();

                int newItemIndex = count - 1;

                queue[newItemIndex] = new ItemPriority<TItem, TPriority> { Item = item, Priority = priority };

                ReorderItem(newItemIndex, -1);
            }

            public TItem Dequeue()
            {
                TItem item = queue[0].Item;

                queue[0].Item = default(TItem);
                queue[0].Priority = priorityMath.MinValue;

                ReorderItem(0, 1);

                count--;

                return item;
            }

            private void ReorderItem(int index, int direction)
            {
                if ((direction != -1) && (direction != 1))
                    throw new ArgumentException("Invalid Direction");

                var item = queue[index];

                int nextIndex = index + direction;

                while ((nextIndex >= 0) && (nextIndex < count))
                {
                    var next = queue[nextIndex];

                    int compare = priorityMath.Compare(item.Priority, next.Priority);

                    if (
                        ((direction == -1) && (compare > 0))
                        ||
                        ((direction == 1) && (compare < 0))
                        )
                    {
                        queue[index] = next;
                        queue[nextIndex] = item;

                        index += direction;
                        nextIndex += direction;
                    }
                    else
                        break;
                }
            }

            public TItem GetHighest()
            {
                if (count == 0)
                    throw new Exception("Queue is empty");
                else
                    return queue[0].Item;
            }

            public TPriority GetHighestPriority()
            {
                if (count == 0)
                    throw new Exception("Queue is empty");
                else
                    return queue[0].Priority;
            }
        }

        public interface INearestNeighbourList<TItem, TDistance>
        {
            bool Add(TItem item, TDistance distance);
            TItem GetFurtherest();
            TItem RemoveFurtherest();

            int MaxCapacity { get; }
            int Count { get; }
        }

        public class NearestNeighbourList<TItem, TDistance> : INearestNeighbourList<TItem, TDistance>
        {
            public NearestNeighbourList(int maxCapacity, KdTreeMath.ITypeMath<TDistance> distanceMath)
            {
                this.maxCapacity = maxCapacity;
                this.distanceMath = distanceMath;

                queue = new PriorityQueue<TItem, TDistance>(maxCapacity, distanceMath);
            }

            public NearestNeighbourList(KdTreeMath.ITypeMath<TDistance> distanceMath)
            {
                this.maxCapacity = int.MaxValue;
                this.distanceMath = distanceMath;

                queue = new PriorityQueue<TItem, TDistance>(distanceMath);
            }

            private PriorityQueue<TItem, TDistance> queue;

            private KdTreeMath.ITypeMath<TDistance> distanceMath;

            private int maxCapacity;
            public int MaxCapacity { get { return maxCapacity; } }

            public int Count { get { return queue.Count; } }

            public bool Add(TItem item, TDistance distance)
            {
                if (queue.Count >= maxCapacity)
                {
                    if (distanceMath.Compare(distance, queue.GetHighestPriority()) < 0)
                    {
                        queue.Dequeue();
                        queue.Enqueue(item, distance);
                        return true;
                    }
                    else
                        return false;
                }
                else
                {
                    queue.Enqueue(item, distance);
                    return true;
                }
            }

            public TItem GetFurtherest()
            {
                if (Count == 0)
                    throw new Exception("List is empty");
                else
                    return queue.GetHighest();
            }

            public TDistance GetFurtherestDistance()
            {
                if (Count == 0)
                    throw new Exception("List is empty");
                else
                    return queue.GetHighestPriority();
            }

            public TItem RemoveFurtherest()
            {
                return queue.Dequeue();
            }

            public bool IsCapacityReached
            {
                get { return Count == MaxCapacity; }
            }
        }

        public struct HyperRect<T>
        {
            private T[] minPoint;
            public T[] MinPoint
            {
                get
                {
                    return minPoint;
                }
                set
                {
                    minPoint = new T[value.Length];
                    value.CopyTo(minPoint, 0);
                }
            }

            private T[] maxPoint;
            public T[] MaxPoint
            {
                get
                {
                    return maxPoint;
                }
                set
                {
                    maxPoint = new T[value.Length];
                    value.CopyTo(maxPoint, 0);
                }
            }

            public static HyperRect<T> Infinite(int dimensions, KdTreeMath.ITypeMath<T> math)
            {
                var rect = new HyperRect<T>
                {
                    MinPoint = new T[dimensions],
                    MaxPoint = new T[dimensions]
                };

                for (var dimension = 0; dimension < dimensions; dimension++)
                {
                    rect.MinPoint[dimension] = math.NegativeInfinity;
                    rect.MaxPoint[dimension] = math.PositiveInfinity;
                }

                return rect;
            }

            public T[] GetClosestPoint(T[] toPoint, KdTreeMath.ITypeMath<T> math)
            {
                T[] closest = new T[toPoint.Length];

                for (var dimension = 0; dimension < toPoint.Length; dimension++)
                {
                    if (math.Compare(minPoint[dimension], toPoint[dimension]) > 0)
                    {
                        closest[dimension] = minPoint[dimension];
                    }
                    else if (math.Compare(maxPoint[dimension], toPoint[dimension]) < 0)
                    {
                        closest[dimension] = maxPoint[dimension];
                    }
                    else
                        closest[dimension] = toPoint[dimension];
                }

                return closest;
            }

            public HyperRect<T> Clone()
            {
                var rect = new HyperRect<T>
                {
                    MinPoint = MinPoint,
                    MaxPoint = MaxPoint
                };
                return rect;
            }
        }

        public interface IKdTree<TKey, TValue> : IEnumerable<KdTreeNode<TKey, TValue>>
        {
            bool Add(TKey[] point, TValue value);

            bool TryFindValueAt(TKey[] point, out TValue value);

            TValue FindValueAt(TKey[] point);

            bool TryFindValue(TValue value, out TKey[] point);

            TKey[] FindValue(TValue value);

            KdTreeNode<TKey, TValue>[] RadialSearch(TKey[] center, TKey radius, int count);

            void RemoveAt(TKey[] point);

            void Clear();

            KdTreeNode<TKey, TValue>[] GetNearestNeighbours(TKey[] point, int count = int.MaxValue);

            int Count { get; }
        }

        public interface IPriorityQueue<TItem, TPriority>
        {
            void Enqueue(TItem item, TPriority priority);

            TItem Dequeue();

            int Count { get; }
        }
    }
    internal class KdTreeMath
    {
        [Serializable]
        public abstract class TypeMath<T> : ITypeMath<T>
        {
            #region ITypeMath<T> members

            public abstract int Compare(T a, T b);

            public abstract bool AreEqual(T a, T b);

            public virtual bool AreEqual(T[] a, T[] b)
            {
                if (a.Length != b.Length)
                    return false;

                for (var index = 0; index < a.Length; index++)
                {
                    if (!AreEqual(a[index], b[index]))
                        return false;
                }

                return true;
            }

            public abstract T MinValue { get; }

            public abstract T MaxValue { get; }

            public T Min(T a, T b)
            {
                if (Compare(a, b) < 0)
                    return a;
                else
                    return b;
            }

            public T Max(T a, T b)
            {
                if (Compare(a, b) > 0)
                    return a;
                else
                    return b;
            }

            public abstract T Zero { get; }

            public abstract T NegativeInfinity { get; }

            public abstract T PositiveInfinity { get; }

            public abstract T Add(T a, T b);

            public abstract T Subtract(T a, T b);

            public abstract T Multiply(T a, T b);

            public abstract T DistanceSquaredBetweenPoints(T[] a, T[] b);

            #endregion
        }

        public interface ITypeMath<T>
        {

            int Compare(T a, T b);

            T MinValue { get; }

            T MaxValue { get; }

            T Min(T a, T b);

            T Max(T a, T b);

            bool AreEqual(T a, T b);

            bool AreEqual(T[] a, T[] b);

            T Add(T a, T b);

            T Subtract(T a, T b);

            T Multiply(T a, T b);

            T Zero { get; }

            T NegativeInfinity { get; }

            T PositiveInfinity { get; }

            T DistanceSquaredBetweenPoints(T[] a, T[] b);
        }

        public class GeoUtils
        {
            public static double Distance(double lat1, double lon1, double lat2, double lon2, char unit)
            {
                double theta = lon1 - lon2;
                double dist = System.Math.Sin(Deg2rad(lat1)) * System.Math.Sin(Deg2rad(lat2)) + System.Math.Cos(Deg2rad(lat1)) * System.Math.Cos(Deg2rad(lat2)) * System.Math.Cos(Deg2rad(theta));
                dist = System.Math.Acos(dist);
                dist = Rad2deg(dist);
                dist = dist * 60 * 1.1515;
                if (unit == 'K')
                {
                    dist = dist * 1.609344;
                }
                else if (unit == 'N')
                {
                    dist = dist * 0.8684;
                }
                return (dist);
            }

            private static double Deg2rad(double deg)
            {
                return (deg * System.Math.PI / 180.0);
            }

            private static double Rad2deg(double rad)
            {
                return (rad / System.Math.PI * 180.0);
            }

        }

        [Serializable]
        public class GeoMath : FloatMath
        {
            public override float DistanceSquaredBetweenPoints(float[] a, float[] b)
            {
                double dst = GeoUtils.Distance(a[0], a[1], b[0], b[1], 'K');
                return (float)(dst * dst);
            }
        }

        [Serializable]
        public class FloatMath : TypeMath<float>
        {
            public override int Compare(float a, float b)
            {
                return a.CompareTo(b);
            }

            public override bool AreEqual(float a, float b)
            {
                return a == b;
            }

            public override float MinValue
            {
                get { return float.MinValue; }
            }

            public override float MaxValue
            {
                get { return float.MaxValue; }
            }

            public override float Zero
            {
                get { return 0; }
            }

            public override float NegativeInfinity
            {
                get
                {
                    return (float)Math.Log(0);
                }
            }

            public override float PositiveInfinity
            {
                get
                {
                    return (float)(-Math.Log(0));
                }
            }

            public override float Add(float a, float b)
            {
                return a + b;
            }

            public override float Subtract(float a, float b)
            {
                return a - b;
            }

            public override float Multiply(float a, float b)
            {
                return a * b;
            }

            public override float DistanceSquaredBetweenPoints(float[] a, float[] b)
            {
                float distance = Zero;
                int dimensions = a.Length;

                for (var dimension = 0; dimension < dimensions; dimension++)
                {
                    float distOnThisAxis = Subtract(a[dimension], b[dimension]);
                    float distOnThisAxisSquared = Multiply(distOnThisAxis, distOnThisAxis);

                    distance = Add(distance, distOnThisAxisSquared);
                }

                return distance;
            }
        }

        [Serializable]
        public class DoubleMath : TypeMath<double>
        {
            public override int Compare(double a, double b)
            {
                return a.CompareTo(b);
            }

            public override bool AreEqual(double a, double b)
            {
                return a == b;
            }

            public override double MinValue
            {
                get { return double.MinValue; }
            }

            public override double MaxValue
            {
                get { return double.MaxValue; }
            }

            public override double Zero
            {
                get { return 0; }
            }

            public override double NegativeInfinity
            {
                get
                {
                    return Math.Log(0);
                }
            }

            public override double PositiveInfinity
            {
                get
                {
                    return -Math.Log(0);
                }
            }

            public override double Add(double a, double b)
            {
                return a + b;
            }

            public override double Subtract(double a, double b)
            {
                return a - b;
            }

            public override double Multiply(double a, double b)
            {
                return a * b;
            }

            public override double DistanceSquaredBetweenPoints(double[] a, double[] b)
            {
                double distance = Zero;
                int dimensions = a.Length;

                for (var dimension = 0; dimension < dimensions; dimension++)
                {
                    double distOnThisAxis = Subtract(a[dimension], b[dimension]);
                    double distOnThisAxisSquared = Multiply(distOnThisAxis, distOnThisAxis);

                    distance = Add(distance, distOnThisAxisSquared);
                }

                return distance;
            }
        }
    }
    #endregion

    #region Vector
    public struct Vector3
    {
        public float X;
        public float Y;
        public float Z;

        public Vector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static readonly Vector3 Zero = new Vector3(0, 0, 0);
        public static readonly Vector3 One = new Vector3(1, 1, 1);

        public float Length => (float)Math.Sqrt(X * X + Y * Y + Z * Z);

        public void Normalize()
        {
            float len = Length;
            if (len > 0)
            {
                X /= len;
                Y /= len;
                Z /= len;
            }
        }

        public float this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0: return X;
                    case 1: return Y;
                    case 2: return Z;
                    default: throw new IndexOutOfRangeException("Index must be 0, 1, or 2.");
                }
            }
            set
            {
                switch (index)
                {
                    case 0: X = value; break;
                    case 1: Y = value; break;
                    case 2: Z = value; break;
                    default: throw new IndexOutOfRangeException("Index must be 0, 1, or 2.");
                }
            }
        }

        public static Vector3 operator +(Vector3 a, Vector3 b)
            => new Vector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static Vector3 operator -(Vector3 a, Vector3 b)
            => new Vector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static Vector3 operator *(Vector3 a, float scalar)
            => new Vector3(a.X * scalar, a.Y * scalar, a.Z * scalar);

        public static Vector3 operator /(Vector3 a, float scalar)
            => new Vector3(a.X / scalar, a.Y / scalar, a.Z / scalar);

        public static Vector3 operator *(float scalar, Vector3 v)
            => new Vector3(v.X * scalar, v.Y * scalar, v.Z * scalar);

        public static float Dot(Vector3 a, Vector3 b)
            => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static Vector3 Cross(Vector3 a, Vector3 b)
            => new Vector3(
                a.Y * b.Z - a.Z * b.Y,
                a.Z * b.X - a.X * b.Z,
                a.X * b.Y - a.Y * b.X
            );

        public bool EqualsApprox(Vector3 other, float tolerance = 1e-6f)
        {
            return Math.Abs(X - other.X) < tolerance &&
                   Math.Abs(Y - other.Y) < tolerance &&
                   Math.Abs(Z - other.Z) < tolerance;
        }
    }
    public struct Vector2
    {
        public float X;
        public float Y;

        public Vector2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static readonly Vector2 Zero = new Vector2(0, 0);
        public static readonly Vector2 One = new Vector2(1, 1);

        public float Length => (float)Math.Sqrt(X * X + Y * Y);

        public void Normalize()
        {
            float len = Length;
            if (len > 0)
            {
                X /= len;
                Y /= len;
            }
        }

        public float this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0: return X;
                    case 1: return Y;
                    default: throw new IndexOutOfRangeException("Index must be 0 or 1.");
                }
            }
            set
            {
                switch (index)
                {
                    case 0: X = value; break;
                    case 1: Y = value; break;
                    default: throw new IndexOutOfRangeException("Index must be 0 or 1.");
                }
            }
        }

        public static Vector2 operator +(Vector2 a, Vector2 b)
            => new Vector2(a.X + b.X, a.Y + b.Y);

        public static Vector2 operator -(Vector2 a, Vector2 b)
            => new Vector2(a.X - b.X, a.Y - b.Y);

        public static Vector2 operator *(Vector2 a, float scalar)
            => new Vector2(a.X * scalar, a.Y * scalar);

        public static Vector2 operator /(Vector2 a, float scalar)
            => new Vector2(a.X / scalar, a.Y / scalar);

        public static Vector2 operator *(float scalar, Vector2 v)
            => new Vector2(v.X * scalar, v.Y * scalar);

        public static float Dot(Vector2 a, Vector2 b)
            => a.X * b.X + a.Y * b.Y;

        public static float Cross(Vector2 a, Vector2 b)
            => a.X * b.Y - a.Y * b.X;

        public bool EqualsApprox(Vector2 other, float tolerance = 1e-6f)
        {
            return Math.Abs(X - other.X) < tolerance &&
                   Math.Abs(Y - other.Y) < tolerance;
        }
    }
    #endregion
}
