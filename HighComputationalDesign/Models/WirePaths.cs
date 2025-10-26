using Avalonia;
using Avalonia.Controls.Shapes;
using HighComputationalDesign.Utils;
using System.Collections.Generic;

namespace HighComputationalDesign.Models
{
    public class WirePaths
    {
        [JsonSave]
        public DigitalComponentConnections DigitalComponentConnection { get; set; }

        public List<Point> Points { get; set; }

        [JsonSave]
        public ComponentPosition[] JPoints
        {
            get;
            set;
            //get => Points is null ? Array.Empty<ComponentPosition>() : Points.Select(x => new ComponentPosition(x.X, x.Y)).ToArray();
            //set => Points = value.Select(x => x.Point).ToList();
        }

        public Path Wire { get; set; }
    }
}
