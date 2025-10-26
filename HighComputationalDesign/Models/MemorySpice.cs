using HighComputationalDesign.ElectricalComponents;
using SpiceSharp;
using SpiceSharp.Algebra;
using SpiceSharp.Attributes;
using SpiceSharp.Behaviors;
using SpiceSharp.Components;
using SpiceSharp.ParameterSets;
using SpiceSharp.Simulations;
using System;
using System.Linq;

namespace HighComputationalDesign.Models
{
    public class MemorySpice : Component
    {
        public MemorySpiceParameters Parameters { get; }

        public event Action<int, bool> OnChangedOutputPinState;

        public MemorySpice(string name, MemoryComponent componentInfo, string[] input, string[] output, string ground)
            : base(name, 
                  (componentInfo.CellBits * 2) + componentInfo.AddressBits
                  + 3 /* Write, Read &  Enable */ 
                  + 1 /* Ground */
            )
        {
            Parameters = new MemorySpiceParameters()
            {
                AddressBits = componentInfo.AddressBits,
                CellBits = componentInfo.CellBits,
                GroundPin = ground
            };

            var pins = new string[(componentInfo.CellBits * 2) + componentInfo.AddressBits
                + 3 /* Write, Read &  Enable */ 
                + 1 /* Ground */
            ];

            int i = 0;
            int e = componentInfo.CellBits;
            for (; i < e; i++)
                pins[i] = input[i];

            e += componentInfo.AddressBits;
            for (; i < e; i++)
                pins[i] = input[i];
            
            pins[i] = input[i++];
            pins[i] = input[i++];
            pins[i] = input[i++];

            for (int c = 0; c < componentInfo.CellBits; c++, i++)
                pins[i] = output[c];
            
            pins[i++] = ground;

            Connect(pins);
        }

        public override void CreateBehaviors(ISimulation simulation)
        {
            var behaviors = new BehaviorContainer(Name);
            var context = new ComponentBindingContext(this, simulation, behaviors);
            behaviors.Add(new MemorySpiceBiasingBehavior(context, Parameters, OnChangedOutputPinState));
            simulation.EntityBehaviors.Add(behaviors);
        }
    }

    public class MemorySpiceParameters : ParameterSet, ICloneable<MemorySpiceParameters>
    {
        //public MemoryComponent ComponentInfo { get; set; }
        public int AddressBits { get; set; }
        public int CellBits { get; set; }
        public string GroundPin { get; set; }

        public MemorySpiceParameters Clone()
        {
            return new MemorySpiceParameters
            {
                AddressBits = this.AddressBits,
                CellBits = this.CellBits,
                GroundPin = this.GroundPin
            };
        }
    }

    [BehaviorFor(typeof(MemorySpice)), AddBehaviorIfNo(typeof(IBiasingBehavior))]
    public class MemorySpiceBiasingBehavior : Behavior, IBiasingBehavior
    {
        private readonly MemorySpiceParameters _parameters;
        private readonly int _groundNode;
        private IBiasingSimulationState _state;

        private readonly int[] InputDataNodes;
        private readonly int[] InputAddrNodes;
        private readonly int[] OutputDataNodes;
        private readonly int[] InputFlagsNodes;

        private readonly Element<double>[] OutputDataElementPins;
        private readonly Element<double>[] rhsDataElementPins;

        public byte[] GrezzaMemory;
        private event Action<int, bool> _OnChangedOutputPinState;

        public MemorySpiceBiasingBehavior(ComponentBindingContext context, MemorySpiceParameters parameters, Action<int, bool> onChangedOutputPinState) : base(context)
        {
            _parameters = parameters;
            
            _state = context.GetState<IBiasingSimulationState>();

            // Crea gli elementi della matrice            
            int i = 0;

            InputDataNodes = new int[parameters.CellBits];
            for (; i < parameters.CellBits; i++)
            {
                var _var = _state.GetSharedVariable(context.Nodes[i]);
                InputDataNodes[i] = _state.Map[_var];
            }

            InputAddrNodes = new int[parameters.AddressBits];
            for (int c = 0; c < parameters.AddressBits; c++, i++)
            {
                var _var = _state.GetSharedVariable(context.Nodes[i]);
                InputAddrNodes[c] = _state.Map[_var];
            }

            InputFlagsNodes = new int[3];
            for (int c = 0; c < 3; c++, i++)
            {
                var _var = _state.GetSharedVariable(context.Nodes[i]);
                InputFlagsNodes[c] = _state.Map[_var];
            }

            OutputDataNodes = new int[parameters.CellBits];
            OutputDataElementPins = new Element<double>[parameters.CellBits];
            rhsDataElementPins = new Element<double>[parameters.CellBits];
            for (int c = 0; c < parameters.CellBits; c++, i++)
            {
                var _var = _state.GetSharedVariable(context.Nodes[i]);
                OutputDataNodes[c] = _state.Map[_var];

                var outputNode = OutputDataNodes[c];
                OutputDataElementPins[c] = _state.Solver.GetElement(new MatrixLocation(outputNode, outputNode));
                rhsDataElementPins[c] = _state.Solver.GetElement(outputNode);
            }

            var groundVar = _state.GetSharedVariable(context.Nodes[i]); // Ottieni le variabili per i nodi
            _groundNode = _state.Map[groundVar]; // Ottieni gli indici dei nodi

            var cells = (int) Math.Pow(parameters.AddressBits, 2);
            GrezzaMemory = Enumerable.Repeat((byte) 0, cells).ToArray();

            _OnChangedOutputPinState = onChangedOutputPinState;
        }

        void IBiasingBehavior.Load()
        {
            if (!GetInputState(InputFlagsNodes[2])) // Is Not Enabled
            {
                for (int i = 0; i < this._parameters.CellBits; i++)
                    SetOutputPin(i, false);

                return;
            }

            int addr = GetAddress();
            byte _byte;

            /*if (GetInputState(InputFlagsNodes[1])) // Is Reading
            {
                _byte = GrezzaMemory[addr];                
            }
            else if (GetInputState(InputFlagsNodes[0])) // Is Writing
            {
                _byte = (byte)GetData();

                GrezzaMemory[addr] = _byte;
            }
            else
            {
                throw new ArgumentException("Inavlid Memory State, With Enabled Set Read | Write");
            }*/
            if (GetInputState(InputFlagsNodes[1])) // Is Reading
            {
                _byte = GrezzaMemory[addr];
            }
            else // Is Writing
            {
                _byte = (byte)GetData();

                GrezzaMemory[addr] = _byte;
            }

            var dataBits = IntToBits(_byte);

            for (int i = 0; i < this._parameters.CellBits; i++)
                SetOutputPin(i, dataBits[i] == 1);
        }

        public void SetOutputPin(int DataIndex, bool value)
        {
            this.OutputDataElementPins[DataIndex].Add(1e6);

            double val = value ? 1e6 * 5.0 : 0.0;
            this.rhsDataElementPins[DataIndex].Add(val);

            _OnChangedOutputPinState?.Invoke(DataIndex, value);
        }

        public bool GetInputState(int i)
        {
            return GetInputPinVoltage(i) >= 2.5;
        }

        public double GetInputPinVoltage(int i)
        {
            return _state.Solution[i] - _state.Solution[_groundNode];
        }

        public double GetOutputPinVoltage(int i)
        {
            return _state.Solution[i];
        }

        int BitsToInt(int[] bits)
        {
            int result = 0;
            for (int i = 0; i < bits.Length; i++)
            {
                result |= bits[i] << i;
            }
            return result;
        }

        int[] IntToBits(int value)
        {
            int[] bits = new int[this._parameters.CellBits];
            for (int i = 0; i < _parameters.CellBits; i++)
            {
                bits[i] = (value >> i) & 1;
            }
            return bits;
        }

        int GetAddress()
        {
            int result = 0;
            for (int i = 0; i < this._parameters.AddressBits; i++)
            {
                result |= (GetInputState(InputAddrNodes[i]) ? 1 : 0) << i;
            }
            return result;
        }

        int GetData()
        {
            int result = 0;
            for (int i = 0; i < this._parameters.CellBits; i++)
            {
                result |= (GetInputState(InputDataNodes[i]) ? 1 : 0) << i;
            }
            return result;
        }

        /*int[] IntToBits(int value)
        {
            int[] bits = new int[this._parameters.CellBits];
            for (int i = 0; i < _parameters.CellBits; i++)
            {
                bits[i] = (value >> i) & 1;
            }
            return bits;
        }*/
    }
}
