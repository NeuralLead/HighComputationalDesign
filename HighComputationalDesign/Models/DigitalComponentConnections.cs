using HighComputationalDesign.Utils;

namespace HighComputationalDesign.Models
{
    public class DigitalComponentConnections
    {
        [JsonSave]
        public DigitalComponent DigitalComponentA { get; set; }
        
        [JsonSave]
        public int NodeA { get; set; }

        
        [JsonSave]
        public DigitalComponent DigitalComponentB { get; set; }
        
        [JsonSave]
        public int NodeB { get; set; }

        public DigitalComponentConnections(DigitalComponent _digitalComponentA, int startPin, DigitalComponent _digitalComponentB, int targetPin)
        {
            DigitalComponentA = _digitalComponentA;
            NodeA = startPin;
            
            DigitalComponentB = _digitalComponentB;
            NodeB = targetPin;
        }
    }
}
