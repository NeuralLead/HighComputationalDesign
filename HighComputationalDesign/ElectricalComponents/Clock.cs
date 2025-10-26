using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using HighComputationalDesign.Utils;
using SpiceSharp.Components;
using SpiceSharp.Simulations;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.CompilerServices;

namespace HighComputationalDesign.ElectricalComponents
{
    public class ClockComponent : INotifyPropertyChanged
    {
        [JsonSave]
        public bool AutoCalculate { get; set; } = true;

        private double _ClockHz = 20_000;

        [JsonSave]
        public double ClockHz {
            get => _ClockHz;
            set
            {
                _ClockHz = value;

                if (AutoCalculate)
                {
                    Period = 1.0 / ClockHz;
                    HighTime = Period * HighTimePercentage;

                    if (ClockHz < 1_000_000)
                        EdgeFraction = 0.1;    // sotto 1 MHz
                    else if (ClockHz < 1_000_000_000)
                        EdgeFraction = 0.05;   // da 1 MHz a 1 GHz
                    else
                        EdgeFraction = 0.01;   // sopra 1 GHz

                    RiseTime = HighTime * EdgeFraction;
                    FallTime = HighTime * EdgeFraction;
                }

                OnPropertyChanged(nameof(ClockHz));
                OnPropertyChanged(nameof(Clock));
                OnPropertyChanged(nameof(Period));
                OnPropertyChanged(nameof(HighTime));
                OnPropertyChanged(nameof(EdgeFraction));
                OnPropertyChanged(nameof(RiseTime));
                OnPropertyChanged(nameof(FallTime));
            }
        }

        public string Clock
        {
            get
            {
                if (ClockHz > 1_000_000_000)
                    return $"{(ClockHz / 1_000_000_000.0).ToString()} GHz";
                else if (ClockHz > 1_000_000)
                    return $"{(ClockHz / 1_000_000.0).ToString()} MHz";
                else if (ClockHz > 1_000)
                    return $"{(ClockHz / 1_000.0).ToString()} KHz";

                return $"{ClockHz} Hz";
            }
        }

        private double _HighTimePercentage = 0.5;
        [JsonSave]
        //[PropertyModels.ComponentModel.Trackable(0.0, 1.0)]
        [Range(0.0, 1.0)]
        public double HighTimePercentage
        {
            get => _HighTimePercentage;
            set
            {
                if (_HighTimePercentage > 1.0)
                    throw new ArgumentException("HighTime is a percentage from 0 to 1. Where 1 is 100%");
                if (_HighTimePercentage < 0.0)
                    throw new ArgumentException("HighTime is a percentage from 0 to 1. Where 0 is 0%");

                _HighTimePercentage = value;
                OnPropertyChanged(nameof(HighTimePercentage));
            }
        }

        private double _Period = 0.00005;

        [PropertyModels.ComponentModel.FloatPrecision(12)]
        [JsonSave]
        public double Period
        {
            get => _Period;
            set
            {
                if (AutoCalculate)
                {
                    _Period = 1.0 / ClockHz;
                    HighTime = _Period * HighTimePercentage;
                    OnPropertyChanged(nameof(HighTime));
                }
                else
                {
                    _Period = value;
                }
                
                OnPropertyChanged(nameof(Period));
            }
        }

        [PropertyModels.ComponentModel.FloatPrecision(12)]
        [JsonSave]
        public double HighTime { get; set; } = 0.000025;

        [Range(0.0, 1.0)]
        [JsonSave]
        public double EdgeFraction { get; set; } = 0.1;

        [PropertyModels.ComponentModel.FloatPrecision(12)]
        [JsonSave]
        public double RiseTime { get; set; } = 0.0000025; // 1e-9

        [PropertyModels.ComponentModel.FloatPrecision(12)]
        [JsonSave]
        public double FallTime { get; set; } = 0.0000025; // 1e-9

        // PropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class Clock : DigitalComponent, INotifyPropertyChanged
    {
        [JsonSave]
        public ClockComponent ClockComponent { get; private set; }
        public override object? GetPropertiesObject { get => ClockComponent; }


        public Clock()
        {
            Init();
        }

        public Clock(Designer designerWindow, string componentName): base(designerWindow, componentName)
        {
            Init();

            DigitalComponentInfo.PinsOut.Add(new Models.Pin($"Vclk", 0));
        }

        private void Init()
        {
            AreInputPinsEditable = false;
            AreOutputPinsEditable = false;
            IsCodeEditable = false;

            ClockComponent = new ClockComponent();

            OnDesignerWindowSet += Clock_OnDesignerWindowSet;
        }

        private void Clock_OnDesignerWindowSet()
        {
            DesignerWindow.Project.OnStepUpdated += OnStepUpdated;
        }

        public override void UpdateSpiceComponent()
        {
            // Clock a 20 kHz
            //double ClockHz = 20_000;
            //double period = 1.0 / ClockHz;   // 50 us
            //double highTime = period / 2.0;  // 25 us per duty 50%

            /*double highTime = ClockComponent.Period * ClockComponent.HighTimePercentage;  // 25 us per duty 50%
            
            double edgeFraction;

            if (ClockComponent.ClockHz < 1_000_000)
                edgeFraction = 0.1;    // sotto 1 MHz
            else if (ClockComponent.ClockHz < 1_000_000_000)
                edgeFraction = 0.05;   // da 1 MHz a 1 GHz
            else
                edgeFraction = 0.01;   // sopra 1 GHz

            double riseTime = highTime * edgeFraction; // 1e-9
            double fallTime = highTime * edgeFraction; // 1e-9*/

            var ClockPulse = new Pulse(0, 5, 0, ClockComponent.RiseTime, ClockComponent.FallTime, ClockComponent.HighTime, ClockComponent.Period);
            var ClockOutput = new VoltageSource(DigitalComponentInfo.DigitalComponentName, DigitalComponentInfo.PinsOut[0].Name, "0",
                    ClockPulse);

            SpiceComponent = ClockOutput;
        }

        public void OnStepUpdated(Transient tran)
        {
            int x = 0;
            bool s = tran.GetVoltage(DigitalComponentInfo.PinsOut[0].Name) >= 2.5;

            IImmutableSolidColorBrush brush = !s ? Brushes.Blue : Brushes.Red;

            Dispatcher.UIThread.Post(() =>
            {
                Label? labelO = GetLabelOPin_OutputPins(x);
                labelO.Foreground = brush;
                labelO.Background = brush;
            });

            Dispatcher.UIThread.Post(() =>
            {
                foreach(var a in DesignerWindow.Project.SavedWirePaths.Where(e=>e.DigitalComponentConnection.DigitalComponentA == this && e.DigitalComponentConnection.NodeA == 0))
                    a.Wire.Stroke = brush;
            });
        }
    }
}
