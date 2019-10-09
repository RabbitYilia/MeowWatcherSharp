namespace MeowWatcherSharp
{
    public class ModemDevice
    {
        public int? Vid { get; set; }
        public int? Pid { get; set; }
        public int? Rev { get; set; }
        public int? MI { get; set; }
        public string ID { get; set; }
        public string FriendlyName { get; set; }
        public string PortName { get; set; }
        public string Description { get; set; }

        public override string ToString()
        {
            return $"{FriendlyName} [{PortName}]";
        }
    }
}
