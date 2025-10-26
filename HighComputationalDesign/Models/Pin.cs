using HighComputationalDesign.Utils;
using System.ComponentModel;

namespace HighComputationalDesign.Models
{
    public class Pin : INotifyPropertyChanged
    {
        private string _name;
        private int _index;

        public Pin(string v, int index)
        {
            _name = v;
            _index = index;
        }

        [JsonSave]
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                }
            }
        }

        [JsonSave]
        public int Index
        {
            get => _index;
            set
            {
                if (_index != value)
                {
                    _index = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Index)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
