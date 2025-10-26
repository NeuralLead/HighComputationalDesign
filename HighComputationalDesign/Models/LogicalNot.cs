using SpiceSharp;
using SpiceSharp.Components;
using SpiceSharp.ParameterSets;
using SpiceSharp.Attributes;
using SpiceSharp.Behaviors;
using SpiceSharp.Simulations;
using SpiceSharp.Algebra;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.IO;

namespace CustomComponents
{
    /// <summary>
    /// Componente con logica hyper programmabile
    /// </summary>
    public class ThresholdComponent : Component
    {
        public ThresholdParameters Parameters { get; }
        private string SourceCodeFile = string.Empty;

        public event Action<int, bool> OnChangedOutputPinState;

        /// <summary>
        /// Crea un nuovo componente di soglia
        /// </summary>
        /// <param name="name">Nome del componente</param>
        /// <param name="input">Nodi di input</param>
        /// <param name="output">Nodi di output</param>
        /// <param name="ground">Nodo di riferimento (ground)</param>
        public ThresholdComponent(string name, string[] input, string[] output, string ground, string sourceCodeFile)
            : base(name, input.Length + output.Length + 1 /* Ground */)
        {
            Parameters = new ThresholdParameters()
            {
                InputPins = input,
                OutputPins = output,
                GroundPin = ground
            };
            
            var pins = new string[input.Length + output.Length + 1/* Ground */];
            
            input.CopyTo(pins, 0);
            output.CopyTo(pins, input.Length);
            pins[input.Length + output.Length] = ground;
            
            Connect(pins);

            SourceCodeFile = sourceCodeFile;
        }

        public override void CreateBehaviors(ISimulation simulation)
        {
            var behaviors = new BehaviorContainer(Name);
            var context = new ComponentBindingContext(this, simulation, behaviors);
            behaviors.Add(new ThresholdBiasingBehavior(context, Parameters, OnChangedOutputPinState, SourceCodeFile));
            simulation.EntityBehaviors.Add(behaviors);
        }
    }

    /// <summary>
    /// Parametri del componente di soglia
    /// </summary>
    [GeneratedParameters]
    public partial class ThresholdParameters : ParameterSet, ICloneable<ThresholdParameters>
    {
        [ParameterName("threshold"), ParameterInfo("Tensione di soglia")]
        public double Threshold { get; set; } = 2.5;

        [ParameterName("vhigh"), ParameterInfo("Tensione output alto")]
        public double VHigh { get; set; } = 5.0;

        [ParameterName("vlow"), ParameterInfo("Tensione output basso")]
        public double VLow { get; set; } = 0.0;

        [ParameterName("smoothness"), ParameterInfo("Parametro di smoothing per convergenza")]
        public double Smoothness { get; set; } = 0.01;

        [ParameterName("inputpins"), ParameterInfo("Pins di input da ricordare")]
        public string[] InputPins { get; set; } = Array.Empty<string>();

        [ParameterName("outputpins"), ParameterInfo("Pins di output da ricordare")]
        public string[] OutputPins { get; set; } = Array.Empty<string>();

        [ParameterName("groundpin"), ParameterInfo("Ground pin da ricordare")]
        public string GroundPin { get; set; } = string.Empty;

        public ThresholdParameters Clone()
        {
            return new ThresholdParameters
            {
                Threshold = this.Threshold,
                VHigh = this.VHigh,
                VLow = this.VLow,
                Smoothness = this.Smoothness,
                InputPins = this.InputPins.ToArray(),
                OutputPins = this.OutputPins.ToArray(),
                GroundPin = this.GroundPin
            };
        }
    }

    /// <summary>
    /// Behavior per analisi DC e transiente
    /// </summary>
    [BehaviorFor(typeof(ThresholdComponent)), AddBehaviorIfNo(typeof(IBiasingBehavior))]
    public class ThresholdBiasingBehavior : Behavior, IBiasingBehavior
    {
        private readonly ThresholdParameters _parameters;
        private readonly int _groundNode;
        private IBiasingSimulationState _state;

        private readonly int[] InputNodes;
        private readonly int[] OutputNodes;
        private readonly Element<double>[] OutputElementPins;
        private readonly Element<double>[] rhsElementPins;

        private event Action<int, bool> _OnChangedOutputPinState;
        private readonly string SourceCodeFile;

        public ThresholdBiasingBehavior(ComponentBindingContext context, ThresholdParameters parames, Action<int, bool> onChangedOutputPinState, string sourceCodeFile  )
            : base(context)
        {
            // Accedi al componente tramite reflection o passa i parametri direttamente
            //context.TryGetParameterSet(out _parameters);
            _parameters = parames;
            _OnChangedOutputPinState = onChangedOutputPinState;

            _state = context.GetState<IBiasingSimulationState>();

            // Ottieni le variabili per i nodi
            var groundVar = _state.GetSharedVariable(context.Nodes[parames.InputPins.Length + parames.OutputPins.Length]);

            // Ottieni gli indici dei nodi
            _groundNode = _state.Map[groundVar];

            // Crea gli elementi della matrice            

            InputNodes = new int[parames.InputPins.Length];
            for (int i = 0; i < parames.InputPins.Length; i++)
            {
                var _var = _state.GetSharedVariable(context.Nodes[i]);
                InputNodes[i] = _state.Map[_var];
            }

            OutputNodes = new int[parames.OutputPins.Length];
            OutputElementPins = new Element<double>[parames.OutputPins.Length];
            rhsElementPins = new Element<double>[parames.OutputPins.Length];

            for (int i = 0; i < parames.OutputPins.Length; i++)
            {
                var _var = _state.GetSharedVariable(context.Nodes[parames.InputPins.Length + i]);
                OutputNodes[i] = _state.Map[_var];

                var outputNode = OutputNodes[i];
                OutputElementPins[i] = _state.Solver.GetElement(new MatrixLocation(outputNode, outputNode));
                rhsElementPins[i] = _state.Solver.GetElement(outputNode);

                //_OnChangedOutputPinState?.Invoke(i, false);
            }

            SourceCodeFile = sourceCodeFile;
            Init();
        }

        public void SetOutputPin(int pinIndex, bool value)
        {
            OutputElementPins[pinIndex].Add(1e6);

            double val = value ? 1e6 * _parameters.VHigh : _parameters.VLow;
            rhsElementPins[pinIndex].Add(val);

            _OnChangedOutputPinState?.Invoke(pinIndex, value);
        }

        public void SetOutputPin(string pinName, bool value)
        {
            int pinIndex = Array.IndexOf(this._parameters.OutputPins, pinName);
            SetOutputPin(pinIndex, value);
        }

        public bool GetOutputPin(int pinIndex)
        {
            return GetOutputPinVoltage(pinIndex) >= this._parameters.Threshold;
        }

        public bool GetOutputPin(string pinName)
        {
            int pinIndex = Array.IndexOf(this._parameters.InputPins, pinName);
            return GetOutputPin(pinIndex);
        }

        public bool GetInputPin(int pinIndex)
        {
            return GetInputPinVoltage(pinIndex) >= this._parameters.Threshold;
        }

        public bool GetInputPin(string pinName)
        {
            int pinIndex = Array.IndexOf(this._parameters.InputPins, pinName);
            return GetInputPin(pinIndex);
        }

        public double GetInputPinVoltage(int pinIndex)
        {
            return _state.Solution[InputNodes[pinIndex]] - _state.Solution[_groundNode];
        }

        public double GetInputPinVoltage(string pinName)
        {
            int pinIndex = Array.IndexOf(this._parameters.InputPins, pinName);
            return GetInputPinVoltage(pinIndex);
        }

        public double GetOutputPinVoltage(int pinIndex)
        {
            return _state.Solution[OutputNodes[pinIndex]]/* - _state.Solution[_groundNode]*/;
        }

        public double GetOutputPinVoltage(string pinName)
        {
            int pinIndex = Array.IndexOf(this._parameters.OutputPins, pinName);
            return GetOutputPinVoltage(pinIndex);
        }

        public bool[] IntToBits(int value, int bitsCounter)
        {
            bool[] bits = new bool[bitsCounter];
            for (int i = 0; i < bitsCounter; i++)
                bits[i] = ((value >> i) & 1) == 1;
            return bits;
        }

        public void LogLine(object? v)
        {
            Debug.WriteLine(v);
        }

        //Globals globals;
        //Action onUpdate;
        Microsoft.CodeAnalysis.Scripting.ScriptRunner<object> runner;

        /*string codice = @"
                using CustomComponents;

public static class MyHCP
{
    public static void F(ThresholdBiasingBehavior self)
    {
        self.LogLine(self.GetInputPinVoltage(0));
    }
}


        bool v = Self.GetInputPin(0);
        Self.SetOutputPin(0, !v);
        Self.SetOutputPin(1, v);
        
        //MyHCP.F(Self);

        //Self.LogLine(Self.GetInputPinVoltage(0));
    ";*/

        /// <summary>
        /// Init C# Script (PreCompile OnFly)
        /// </summary>
        void Init()
        {
            /*globals = new Globals
            {
                 Self = this,
            };*/

            var sOpts = Microsoft.CodeAnalysis.Scripting.ScriptOptions.Default
                    .WithReferences(typeof(object).Assembly, typeof(CustomComponents.ThresholdBiasingBehavior).Assembly)
                    .WithImports("System", "System.Collections.Generic", "System.Diagnostics");

            // Compila una sola volta
            var codice = File.ReadAllText(SourceCodeFile);
            //var script = CSharpScript.Create(codice, sOpts, typeof(Globals));
            //script.Compile();  // JIT iniziale

            //runner = script.CreateDelegate(); // runner pronto da richiamare

            // 2 Nd
            //var state = CSharpScript.RunAsync(codice, sOpts, globals).GetAwaiter().GetResult();
            //onUpdate = (Action)state.Variables.First(v => v.Name == "OnUpdate").Value;

            Self = new object[] { this };

            // Compilazione una sola volta
            var script = CSharpScript.Create(codice, sOpts/*, typeof(Globals)*/);
            script.Compile();
            runner = script.CreateDelegate();

            // esegui una volta per caricare la classe ScriptState
            runner().GetAwaiter().GetResult();

            // Ora estrai l’assembly in memoria
            Assembly assembly;
            using (var ms = new MemoryStream())
            {
                var emitResult = script.GetCompilation().Emit(ms);
                if (!emitResult.Success)
                    throw new Exception("Errore di compilazione script.");

                ms.Seek(0, SeekOrigin.Begin);
                assembly = Assembly.Load(ms.ToArray());
            }

            // Cerca la classe e il metodo
            var type = assembly.GetType("Submission#0+ScriptState")
                       ?? assembly.GetType("ScriptState");

            _onUpdate = type.GetMethod("OnUpdate", BindingFlags.Public | BindingFlags.Static);
        }
        MethodInfo? _onUpdate;

        void IBiasingBehavior.Load()
        {
            // Avvia Script
            //runner(globals).GetAwaiter().GetResult();
            //onUpdate();

            // Invocazione veloce: chiama ScriptState.OnUpdate(Self)
            /*var type = script.GetCompilation().Assembly.GetType("Submission#0+ScriptState")
                       ?? script.GetCompilation().Assembly.GetType("ScriptState");

            var method = type.GetMethod("OnUpdate", BindingFlags.Public | BindingFlags.Static);
            method.Invoke(null, new object[] { globals.Self });*/

            _onUpdate.Invoke(null, Self);
        }

        private object[] Self;
    }

    /*public class Globals
    {
        //public Element<double>[] OutputElementPins;
        //public Element<double>[] rhsElementPins;

        public ThresholdBiasingBehavior Self;
    }*/
}