using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MeowWatcherSharp
{
    class Program
    {
        static void Main(string[] args)
        {
            var Config = new Settings("conf.json");
            var Start = (DateTime.Now.ToUniversalTime().Ticks - 621355968000000000) / 10000;
            while (true)
            {
                var Now = (DateTime.Now.ToUniversalTime().Ticks - 621355968000000000) / 10000;
                if (Now - Start < 1000)
                {
                    Thread.Sleep((int)(1000-Now + Start));
                }
                foreach (var DeviceName in Config.GetSpareDevices())
                {
                    Console.WriteLine($"Try to Start {DeviceName}");
                    Config.Devices[DeviceName].DetectPort();
                    Config.Devices[DeviceName].StartMonitor();
                }
                foreach (var DeviceName in Config.GetLiveDevices())
                {
                    try
                    {
                        Modem.ReceiveSMS(Config.Devices[DeviceName]);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        Config.Devices[DeviceName].StopMonitor();
                    }
                }
                Start = Now;
            }
        }
    }
}