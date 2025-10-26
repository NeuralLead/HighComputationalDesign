using SpiceSharp.Simulations;
using SpiceSharp;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System;
using Avalonia.Threading;
using System.IO;
using HighComputationalDesign.Utils;
using System.ComponentModel.DataAnnotations;

namespace HighComputationalDesign.Models
{
    public class HCPProject: INotifyPropertyChanged
    {
        [JsonSave]
        public string Name { get; set; } = "New Project";
        
        [JsonSave]
        public string ProjectPath { get; set; } = string.Empty;

        [JsonSave]
        public ObservableCollection<DigitalComponent> Components { get; set; } = new ObservableCollection<DigitalComponent>();

        // Lista per memorizzare tutte le linee salvate
        [JsonSave]
        public List<WirePaths> SavedWirePaths { get; set; } = new List<WirePaths>();

        [JsonSave]
        [PropertyModels.ComponentModel.FloatPrecision(12)]
        public double TransientStep { get; set; } = 1e-7;

        [JsonSave]
        [PropertyModels.ComponentModel.FloatPrecision(12)]
        public double TransientFinal { get; set; } = 200e-6;

        private int _SimulationStepDelay = 1;
        [JsonSave]
        [Range(0, int.MaxValue)]
        public int SimulationStepDelay { get => _SimulationStepDelay; set { _SimulationStepDelay = value < 1 ? 1 : value; } }

        private int runned = 0;

        private bool IsPaused = false;
        public static ManualResetEvent StepByStepEvent = new ManualResetEvent(false);
        private bool IsStopped = true;
        private bool IsLoopEnabled = false;

        public Transient Tran { get; private set; } = new Transient("Null");
        public string TransientTime { get => Tran is null || runned <= 0 ? $"Time 0" : $"Time {Tran.Time}"; }


        public event Action<Transient> OnStepUpdated;

        public void Save()
        {
            if (string.IsNullOrEmpty(ProjectPath))
                throw new Exception("Inavlid ProjectPath directory");

            if(!Directory.Exists(ProjectPath))
                Directory.CreateDirectory(ProjectPath);

            string projectFile = Path.Combine(ProjectPath, $"Project.hcd");
            
            SavedWirePaths.ForEach(s => s.JPoints = s.Points.Select(x => new ComponentPosition(x.X, x.Y)).ToArray()); // TODO ugly method ( find a better method to save autonomously right values, Avalonia Point haven't Properties)

            File.WriteAllText(projectFile, GlobalJsonConverter.Serialize(this));

            SavedWirePaths.ForEach(x => x.JPoints = Array.Empty<ComponentPosition>()); // TODO ugly method ( find a better method to save autonomously right values, Avalonia Point haven't Properties)
        }

        public static HCPProject? Load(string projectPath)
        {
            string projectFile = Path.Combine(projectPath, $"Project.hcd");

            if (!File.Exists(projectFile))
                throw new Exception("Project.hcd file dosen't Exists");

            string projectFileContent = File.ReadAllText(projectFile);

            var p = GlobalJsonConverter.Deserialize<HCPProject>(projectFileContent);

            foreach (var s in p.SavedWirePaths)
            {
                var dA = s.DigitalComponentConnection.DigitalComponentA?.DigitalComponentInfo.DigitalComponentName;
                var dB = s.DigitalComponentConnection.DigitalComponentB?.DigitalComponentInfo.DigitalComponentName;

                s.DigitalComponentConnection.DigitalComponentA = p.Components?.FirstOrDefault(x => x.DigitalComponentInfo.DigitalComponentName == dA);
                s.DigitalComponentConnection.DigitalComponentB = p.Components?.FirstOrDefault(x => x.DigitalComponentInfo.DigitalComponentName == dB);

                // TODO ugly method ( find a better method to load autonomously right values, Avalonia Point haven't Properties)
                if (s.JPoints != null)
                    s.Points = s.JPoints.Select(x => x.Point).ToList();
                s.JPoints = Array.Empty<ComponentPosition>();
            }

            return p;
        }

        object CHeck_Lock = new object();

        private void StartProject()
        {
            lock (CHeck_Lock)
            {
                if (!IsStopped)
                    return;
            }

            Save();

            foreach (var co in Components)
                co.UpdateSpiceComponent();

            var components = Components.Where(x => x.SpiceComponent != null).Select(x => x.SpiceComponent).ToArray();

            if (components.Length == 0) throw new System.Exception("No components");

            // Costruisco il circuito con sorgente PULSE
            var ckt = new Circuit(
                components

            /*ClockOutput,*/
            /*new Resistor("R1", "in", "out", 1.0e3),
            new Resistor("R2", "out", "0", 2.0e3),

            //new BehavioralVoltageSource("Vout", "out", "0", "V(in) < 2.5 ? 5 : 0")
            new ThresholdComponent("TH1", ["in"], ["out"], "0")*/
            );

            // Simulazione transiente: 0 → 200 us, step 0.1 us
            Tran = new Transient("tran", TransientStep, TransientFinal)
            {
                Repeat = false
            };

            new Thread(() =>
            {
                IsStopped = false;

                while (!IsStopped)
                {
                    runned++;

                    foreach (int exportType in Tran.Run(ckt))
                    {
                        /*double vin = Tran.GetVoltage("in");
                        *///double vout = Tran.GetVoltage("out");
                          //Debug.WriteLine($"{Tran.Time * 1e6:F2} us: Vin={vin:F2} V, Vout={vout:F2} V");

                        Dispatcher.UIThread.Post(() =>
                        {
                            OnPropertyChanged(nameof(TransientTime));
                        });
                        //foreach (var cop in Components) cop.OnStepUpdated(Tran);
                        OnStepUpdated?.Invoke(Tran);

                        Thread.Sleep(_SimulationStepDelay);

                        if (IsPaused)
                        {
                            StepByStepEvent.WaitOne();
                            StepByStepEvent.Reset();
                        }

                        if (IsStopped) break;
                    }

                    if (!IsLoopEnabled)
                        lock(CHeck_Lock)
                            IsStopped = true;
                }

            }).Start();
        }

        public void StartSimulation()
        {
            IsPaused = false;
            StepByStepEvent.Set();

            StartProject();
        }

        public void StepSimulation()
        {
            IsPaused = true;
            StepByStepEvent.Set();

            StartProject();
        }

        public void StopSimulation()
        {
            IsPaused = false;
            lock(CHeck_Lock)
                IsStopped = true;

            StepByStepEvent.Set();
        }

        // PropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
