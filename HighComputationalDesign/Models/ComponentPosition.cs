using Avalonia;
using HighComputationalDesign.Utils;

namespace HighComputationalDesign.Models
{
    public class ComponentPosition
    {
        public Point Point;

        [JsonSave]
        public double X {  get => Point.X; set => Point = new Point(value, Point.Y); }

        [JsonSave]
        public double Y { get => Point.Y; set => Point = new Point(Point.X, value); }

        public ComponentPosition()
        {
            Point = new Point();
        }

        public ComponentPosition(double x, double y)
        {
            Point = new Point();
            
            X = x;
            Y = y;
        }
    }
}
