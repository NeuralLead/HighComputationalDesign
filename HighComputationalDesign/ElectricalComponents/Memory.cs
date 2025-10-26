using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using HighComputationalDesign.Models;
using HighComputationalDesign.Utils;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.CompilerServices;

namespace HighComputationalDesign.ElectricalComponents
{
    public class MemoryComponent : INotifyPropertyChanged
    {
        public event Action OnMemorySizeChanged;

        private int _CellBits = 32;
        [JsonSave]
        [Range(1, 8192)]
        public int CellBits { get => _CellBits; set { _CellBits = value; OnPropertyChanged(nameof(CellBits)); OnMemorySizeChanged?.Invoke(); } }

        private int _AddressBits = 32;
        [JsonSave]
        [Range(1, 8192)]
        public int AddressBits { get => _AddressBits; set { _AddressBits = value; OnPropertyChanged(nameof(AddressBits)); OnMemorySizeChanged?.Invoke(); } }


        // PropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class Memory : DigitalComponent
    {
        private MemoryComponent _MemoryComponentInfo = new MemoryComponent();
        
        [JsonSave]
        public MemoryComponent MemoryComponentInfo { get => _MemoryComponentInfo; 
            private set
            {
                if (_MemoryComponentInfo != null)
                    _MemoryComponentInfo.OnMemorySizeChanged -= MemoryComponentInfo_OnMemorySizeChanged;
                
                _MemoryComponentInfo = value;

                if (_MemoryComponentInfo != null)
                    _MemoryComponentInfo.OnMemorySizeChanged += MemoryComponentInfo_OnMemorySizeChanged;
            }
        }
        public override object? GetPropertiesObject { get => MemoryComponentInfo; }

        public Memory()
        {
            Init();
        }

        public Memory(Designer designerWindow, string componentName) : base(designerWindow, componentName)
        {
            Init();

            MemoryComponentInfo.CellBits = 4;
            MemoryComponentInfo.AddressBits = 4;
        }

        private void Init()
        {
            AreInputPinsEditable = false;
            AreOutputPinsEditable = false;
            IsCodeEditable = false;

            //OnDesignerWindowSet += Clock_OnDesignerWindowSet;
        }

        private void MemoryComponentInfo_OnMemorySizeChanged()
        {
            DigitalComponentInfo.PinsIn.Clear();
            DigitalComponentInfo.PinsOut.Clear();

            for (int i = 0; i < MemoryComponentInfo.CellBits; i++)
                DigitalComponentInfo.PinsIn.Add(new Pin($"iData{i}", i));

            for (int i = 0; i < MemoryComponentInfo.AddressBits; i++)
                DigitalComponentInfo.PinsIn.Add(new Pin($"iAddr{i}", i));

            for (int i = 0; i < MemoryComponentInfo.CellBits; i++)
                DigitalComponentInfo.PinsOut.Add(new Pin($"oData{i}", i));

            int u = MemoryComponentInfo.CellBits + MemoryComponentInfo.AddressBits + MemoryComponentInfo.CellBits;
            DigitalComponentInfo.PinsIn.Add(new Pin($"Read", u));
            DigitalComponentInfo.PinsIn.Add(new Pin($"Write", u+1));
            DigitalComponentInfo.PinsIn.Add(new Pin($"Enable", u+2));
        }

        public override void UpdateSpiceComponent()
        {
            if (SpiceComponent != null)
                (SpiceComponent as MemorySpice).OnChangedOutputPinState -= Memory_OnChangedOutputPinState;

            SpiceComponent = new MemorySpice(
                DigitalComponentInfo.DigitalComponentName, 
                MemoryComponentInfo, 
                DigitalComponentInfo.PinsIn.Select(x => x.Name).ToArray(),
                DigitalComponentInfo.PinsOut.Select(x => x.Name).ToArray(), 
                "0");

            (SpiceComponent as MemorySpice).OnChangedOutputPinState += Memory_OnChangedOutputPinState;
        }

        private void Memory_OnChangedOutputPinState(int x, bool s)
        {
            IImmutableSolidColorBrush brush = !s ? Brushes.Blue : Brushes.Red;

            Dispatcher.UIThread.Post(() =>
            {
                Label? labelO = GetLabelOPin_OutputPins(x);
                labelO.Foreground = brush;
                labelO.Background = brush;
            });

            Dispatcher.UIThread.Post(() =>
            {
                foreach (var a in DesignerWindow.Project.SavedWirePaths.Where(e => e.DigitalComponentConnection.DigitalComponentA == this && e.DigitalComponentConnection.NodeA == x))
                    a.Wire.Stroke = brush;
            });
        }
    }
}
