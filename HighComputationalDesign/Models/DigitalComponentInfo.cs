using HighComputationalDesign.Utils;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HighComputationalDesign.Models
{
    public class DigitalComponentInfo : INotifyPropertyChanged
    {
        [JsonSave]
        [BrowsableAttribute(false)]
        public ComponentPosition ComponentPosition { get; set; } = new ComponentPosition();
        //public Point ComponentSize { get => new Point(Bounds.Width, Bounds.Height); set { Width = value.X; Height = value.Y; } }

        private string _DigitalComponentName = "New digital component";

        [JsonSave]
        public string DigitalComponentName { get => _DigitalComponentName; set { _DigitalComponentName = value; OnPropertyChanged(nameof(DigitalComponentName)); } }

        [JsonSave]
        public string LogicSourceFile { get; set; } = string.Empty;

        [BrowsableAttribute(false)]
        [JsonSave]
        public ObservableCollection<Pin> PinsIn { get; set; } = new ObservableCollection<Pin>();
        
        [BrowsableAttribute(false)]
        [JsonSave]
        public ObservableCollection<Pin> PinsOut { get; set; } = new ObservableCollection<Pin>();


        // PropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
