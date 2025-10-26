using Avalonia.Controls;
using SpiceSharp.Components;
using SpiceSharp.Simulations;
using SpiceSharp;
using System.Diagnostics;
using System.Linq;
using CustomComponents;
using HighComputationalDesign.Models;
using HighComputationalDesign.ElectricalComponents;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Input;

namespace HighComputationalDesign
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        HCPProject _Project;
        public HCPProject Project
        {
            get => _Project;
            set
            {
                _Project = value;
                if (XDesigner != null)
                {
                    XDesigner.Project = _Project;
                    XDesigner.SelectedObject = this.Project;
                }
                
                OnPropertyChanged(nameof(Project));
            }
        }

        private bool _Override = false;
        public bool Override { get => _Override; }
        
        public MainWindow()
        {
            InitializeComponent();
            //contextMenu = InitRightMenu();

            Project = new HCPProject();

            this.DataContext = this;

            //Project;

            //Spice2();
            //Spice();

            this.KeyDown += MainWindow_KeyDown;
            this.KeyUp += MainWindow_KeyUp;
        }

        private void MainWindow_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.F10)
            {
                MenuItem_ProjectStep_Click(this, new Avalonia.Interactivity.RoutedEventArgs());
            }
            else if (e.Key == Key.F5)
            {
                MenuItem_ProjectRun_Click(this, new Avalonia.Interactivity.RoutedEventArgs());
            }
            else if (e.Key == Key.LeftCtrl)
            {
                _Override = true;
            }
        }

        private void MainWindow_KeyUp(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.LeftCtrl)
            {
                _Override = false;
            }
        }

        public void Spice2()
        {
            // Clock a 20 kHz
            double ClockHz = 20_000;
            double period = 1.0 / ClockHz;   // 50 us
            double highTime = period / 2.0;  // 25 us per duty 50%

            var ClockPulse = new Pulse(0, 5, 0, 1e-9, 1e-9, highTime, period);

            // Costruisco il circuito con sorgente PULSE
            var ckt = new Circuit(
                new VoltageSource("Vclk", "in", "0",
                    ClockPulse),
                new Resistor("R1", "in", "out", 1.0e3),
                new Resistor("R2", "out", "0", 2.0e3),

                //new BehavioralVoltageSource("Vout", "out", "0", "V(in) < 2.5 ? 5 : 0")
                new ThresholdComponent("TH1", ["in"], ["out"], "0", string.Empty)
            );

            var ckt2 = new Circuit(
                new VoltageSource("Vclk", "in", "0",
                    ClockPulse),
                new Resistor("R1", "in", "out", 1.0e3),
                new Resistor("R2", "out", "0", 2.0e3)
            );

            // Simulazione transiente: 0 → 200 us, step 0.1 us
            var tran = new Transient("tran", 1e-7, 200e-6)
            {
                Repeat = false
            };

            var subckt = new SubcircuitDefinition(new Circuit(
                new Resistor("R1", "a", "b", 1e3),
                new Resistor("R2", "b", "c", 1e3)), "a", "c");
            var ckt5 = new Circuit(
                new VoltageSource("V1", "in", "0", 10.0),
                new Subcircuit("X1", subckt, "in", "out"),
                new Subcircuit("X2", subckt, "out", "0"));


            // Define the subcircuit
            var subckt3 = new SubcircuitDefinition(new Circuit(
                new Resistor("R1", "a", "b", 1e3),
                new Resistor("R2", "b", "0", 1e3)),
                "a", "b");

            // Define the circuit
            var ckt7 = new Circuit(
                new VoltageSource("V1", "in", "0", 5.0)//,
                                                       //new Subcircuit("X1", subckt3).Connect("in", "out"),

                );

            //while (true)
            {
                foreach (int exportType in tran.Run(ckt))
                {
                    double vin = tran.GetVoltage("in");
                    double vout = tran.GetVoltage("out");
                    Debug.WriteLine($"{tran.Time * 1e6:F2} us: Vin={vin:F2} V, Vout={vout:F2} V");
                }
            }
        }

        public void ExecuteCircuits(Transient tran, Circuit[] circuits)
        {
            // Creiamo un enumerator per ogni circuito
            var enumerators = circuits
                .Select(c => tran.Run(c).GetEnumerator())
                .ToList();

            bool hasMore;
            do
            {
                hasMore = false;

                foreach (var e in enumerators)
                {
                    if (e.MoveNext())
                    {
                        hasMore = true;

                        // Ogni yield della funzione viene eseguito qui
                        int result = e.Current;
                        double vin = tran.GetVoltage("in");
                        double vout = tran.GetVoltage("out");
                        Debug.WriteLine($"{tran.Time * 1e6:F2} us: Vin={vin:F2} V, Vout={vout:F2} V (yield={result})");
                    }
                }

            } while (hasMore);
        }

        private void MenuItemAddComponent_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            XDesigner.AddComponent(new DigitalComponent(XDesigner, $"Digital Component {Project.Components.Count}"));
        }

        private void MenuItemAddComponentClock_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            XDesigner.AddComponent(new Clock(XDesigner, $"Clock {Project.Components.Count}"));
        }

        private void MenuItemAddComponentMemory_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            XDesigner.AddComponent(new Memory(XDesigner, $"Memory {Project.Components.Count}"));
        }

        private void MenuItem_ProjectRun_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Project?.StartSimulation();
        }

        public void MenuItem_ProjectStep_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Project?.StepSimulation();
        }

        private void MenuItem_ProjectStop_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Project?.StopSimulation();
        }

        private void MenuItem_ProjectProperties_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            XDesigner.SelectedObject = Project;
        }

        /*public void Spice()
        {
            // Build the circuit
            var ckt = new Circuit(
                new VoltageSource("V1", "in", "0", 0.0),
                new SpiceSharp.Components.VoltageSources.FrequencyBehavior(
                new Resistor("R1", "in", "out", 1.0e3),
                new Resistor("R2", "out", "0", 2.0e3)
                );

            // Create a DC sweep and register to the event for exporting simulation data
            double max = 5.0;
            double hz = 20;
            //double step = 3
            var dc = new DC("dc", "V1", 0.0, 5.0, 5.0)
            {
                Repeat = false,
            };

            // Run the simulation
            while (true)
            {
                //dc.Rerun();

                foreach (int exportType in dc.Run(ckt))
                {
                    //var a = dc.Run(ckt);

                    var s = dc.Statistics;
                    //Debug.WriteLine(ckt["V1"].GetParameterSet<>());
                    Debug.WriteLine(dc.GetVoltage("in"));
                    Debug.WriteLine(dc.GetVoltage("out"));
                    Debug.WriteLine("");
                }
            }
        }*/

        private void MenuItem_LoadProject_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Project = HCPProject.Load(@"C:\Users\username\Desktop\a"); // TODO put a Avalonia FolderFileDialog

            XDesigner.eCanvas.Children.Clear();

            foreach (var component in Project?.Components)
            {
                component.DesignerWindow = XDesigner;
                XDesigner.eCanvas.Children.Add(component);
                component.UpdatePosition();
            }

            foreach (var s in Project?.SavedWirePaths)
            {
                if (s.Points is null || s.Points.Count == 0)
                {
                    var startAnchor = XDesigner.GetPinAnchor(s.DigitalComponentConnection.DigitalComponentA.GetLabelOPin_OutputPins(s.DigitalComponentConnection.NodeA), XDesigner.eCanvas);
                    var endAnchor = XDesigner.GetPinAnchor(s.DigitalComponentConnection.DigitalComponentB.GetLabelOPin_InputPins(s.DigitalComponentConnection.NodeB), XDesigner.eCanvas);
                    s.Points = XDesigner.FindPath(startAnchor, endAnchor, XDesigner.eCanvas.Bounds.Size, false);
                }

                s.Wire = XDesigner.BuildWire(s.Points);
                XDesigner.eCanvas.Children.Add(s.Wire);
                XDesigner.AddLineMenu(s.Wire);
            }
        }

        private void MenuItem_SaveProject_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Project?.Save();
        }


        // PropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}