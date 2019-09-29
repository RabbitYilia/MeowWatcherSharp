using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeowWatcherSharp
{
    class Settings
    {
        public class DevicesItem
        {
            public Settings Parent;
            public class PushAddrsItem
            {
                public string URL { get; set; }
                public string Body { get; set; }
            }
            public string Name = "";
            public string IMEI = "";
            public string PhoneNumber = "";
            public string ModemPortName = "";
            public string DiagnosePortName = "";
            public string VoicePortName = "";
            public SerialPort ModemPort { get; set; }
            public SerialPort DiagnosePort { get; set; }
            public SerialPort VoicePort { get; set; }
            public bool Monitoring = false;

            public List<PushAddrsItem> PushAddrs = new List<PushAddrsItem>();
            public void DetectPort()
            {
                if (this.IMEI != "")
                {
                    Modem.DetectModemByIMEI(this);
                    return;
                }
            }
            public bool StartMonitor()
            {
                if (this.Monitoring == false)
                {
                    try
                    {
                        if (ModemPortName != "")
                        {
                            this.ModemPort = new SerialPort(ModemPortName);
                            this.ModemPort.Open();
                            Modem.InitDevice(this);
                        }
                        if (DiagnosePortName != "")
                        {
                            this.DiagnosePort = new SerialPort(DiagnosePortName);
                            this.DiagnosePort.Open();
                        }
                        if (VoicePortName != "")
                        {
                            this.VoicePort = new SerialPort(VoicePortName);
                            this.VoicePort.Open();
                        }
                        if (ModemPort != null && DiagnosePort != null && VoicePort != null)
                        {
                            this.Monitoring = true;
                            Console.WriteLine($"{this.Name} starts being monitoring");
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        StopMonitor();
                        return false;
                    }
                }
                StopMonitor();
                return false;
            }
            public void StopMonitor()
            {
                if (ModemPort != null || DiagnosePort != null || VoicePort != null)
                {
                    Console.WriteLine($"{this.Name} stops being monitoring");
                }
                if (ModemPort != null)
                {
                    if (ModemPort.IsOpen)
                    {
                        try
                        {
                            ModemPort.Close();
                        }
                        catch (Exception ex)
                        {
                            ModemPort.Dispose();
                        }
                    }
                    ModemPort.Dispose();
                    ModemPort = null;
                }
                if (DiagnosePort != null)
                {
                    if (DiagnosePort.IsOpen)
                    {
                        try
                        {
                            DiagnosePort.Close();
                        }
                        catch (Exception ex)
                        {
                            DiagnosePort.Dispose();
                        }
                    }
                    DiagnosePort.Dispose();
                    DiagnosePort = null;
                }
                if (VoicePort != null)
                {
                    if (VoicePort.IsOpen)
                    {
                        try
                        {
                            VoicePort.Close();
                        }
                        catch (Exception ex)
                        {
                            VoicePort.Dispose();
                        }
                    }
                    VoicePort.Dispose();
                    VoicePort = null;
                }
                this.Monitoring = false;
            }
            public void PostMessage(string Tittle,string Message)
            {
                foreach (var PostConf in this.PushAddrs)
                {
                    var Attrs = PostConf.Body.Replace("{.Tittle}", System.Web.HttpUtility.UrlEncode(Tittle)).Replace("{.Content}", System.Web.HttpUtility.UrlEncode(Message));
                    Parent.Networking.Post(PostConf.URL, Attrs);
                }
            }
        }
        public string Proxy { get; set; }
        public Network Networking { get; set; }
        public Dictionary<string, DevicesItem> Devices = new Dictionary<string, DevicesItem>();
        public Settings(string Path)
        {
            System.IO.StreamReader File = System.IO.File.OpenText(Path);
            JsonTextReader Reader = new JsonTextReader(File);
            JObject Data = (JObject)JToken.ReadFrom(Reader);
            this.Proxy = Data["Proxy"].ToString();
            foreach (JObject Device in Data["Devices"])
            {
                var ThisDevice = new DevicesItem
                {
                    Parent = this,
                    IMEI = Device["IMEI"].ToString(),
                    Name = Device["Name"].ToString()
                };
                foreach (JObject PushAddr in Device["PushAddrs"])
                {
                    ThisDevice.PushAddrs.Add(new DevicesItem.PushAddrsItem
                    {
                        Body = PushAddr["Body"].ToString(),
                        URL = PushAddr["URL"].ToString()
                    });
                }
                this.Devices.Add(Device["Name"].ToString(), ThisDevice);
            }
            this.Networking = new Network(this.Proxy);
        }
        public List<String> GetSpareDevices()
        {
            var ReturnValue = new List<String>();
            foreach (var Device in this.Devices)
            {
                if (Device.Value.Monitoring == false)
                {
                    ReturnValue.Add(Device.Key);
                }
            }
            Console.WriteLine("Current Offline: "+String.Join(", ", ReturnValue.ToArray()));
            return ReturnValue;
        }
        public List<String> GetLiveDevices()
        {
            var ReturnValue = new List<String>();
            foreach (var Device in this.Devices)
            {
                if (Device.Value.Monitoring == true)
                {
                    ReturnValue.Add(Device.Key);
                }
            }
            Console.WriteLine("Current Online: " + String.Join(", ", ReturnValue.ToArray()));
            return ReturnValue;
        }
    }
}

