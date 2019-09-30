using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GsmComm.PduConverter;
using GsmComm.PduConverter.SmartMessaging;
using NAudio.Wave;
using NAudio.Lame;
using NAudio.Wave.SampleProviders;

namespace MeowWatcherSharp
{
    static class Modem
    {
        public static void InitDevice(Settings.DevicesItem Device)
        {
            string Data;
            string RegexStr;
            try
            {
                //Enable Feedback
                Data = RunCommandWithFeedBack(Device.ModemPort, "ATE");
                if (!Data.Contains("OK")) { throw new Exception($"Init Failure:{Data}"); }
                //Reset
                Data = RunCommandWithFeedBack(Device.ModemPort, "AT&F");
                if (!Data.Contains("OK")) { throw new Exception($"Init Failure:{Data}"); }
                Data = RunCommandWithFeedBack(Device.ModemPort, "ATZ");
                if (!Data.Contains("OK")) { throw new Exception($"Init Failure:{Data}"); }
                Data = RunCommandWithFeedBack(Device.ModemPort, "ATE1");
                if (!Data.Contains("OK")) { throw new Exception($"Init Failure:{Data}"); }
                //Set Work Mode
                Data = RunCommandWithFeedBack(Device.ModemPort, "AT^SYSCFG=2,2,3FFFFFFF,2,4");
                if (!Data.Contains("OK")) { throw new Exception($"Init Failure:{Data}"); }
                Data = RunCommandWithFeedBack(Device.ModemPort, "AT^U2DIAG=0");
                if (!Data.Contains("OK")) { throw new Exception($"Init Failure:{Data}"); }
                //Get IMEI
                Data = RunCommandWithFeedBack(Device.ModemPort, "AT+CGSN");
                if (!Data.Contains("OK")) { throw new Exception($"Init Failure:{Data}"); }
                if (Device.IMEI == "") { Device.IMEI = Regex.Match(Data, "\\r\\n[0-9A-Za-z]*\\r\\n").ToString().Replace("\r\n", ""); }
                //Get PhoneNumber
                Data = RunCommandWithFeedBack(Device.ModemPort, "AT+CNUM");
                //if (!Data.Contains("OK")) { throw new Exception($"Init Failure:{Data}"); }
                RegexStr = "\\r\\n\\+CNUM: [\\\",\\+0-9]*\\r\\n";
                Device.PhoneNumber = Regex.Match(Data, RegexStr).ToString().Replace("\r\n", "");
                if (Device.PhoneNumber.Contains("+CNUM: "))
                {
                    Device.PhoneNumber = Device.PhoneNumber.Replace("\"", "").Replace("+CNUM: ", "").Split(',')[1];
                }
                //Set SMS Charset
                Data = RunCommandWithFeedBack(Device.ModemPort, "AT+CSCS=?");
                if (!Data.Contains("OK")) { throw new Exception($"Init Failure:{Data}"); }
                if (Data.Contains("UCS2"))
                {
                    Data = RunCommandWithFeedBack(Device.ModemPort, "AT+CSCS=\"UCS2\"");
                    if (!Data.Contains("OK")) { throw new Exception($"Init Failure:{Data}"); }
                }
                //Set SMS Mode
                Data = RunCommandWithFeedBack(Device.ModemPort, "AT+CMGF=?");
                if (!Data.Contains("OK")) { throw new Exception($"Init Failure:{Data}"); }
                RegexStr = "\\r\\n\\+CMGF: \\([0-9-]*\\)\\r\\n";
                var Status = Regex.Match(Data, RegexStr).ToString().Replace("\r\n", "");
                if (Status.Contains("0"))
                {
                    Data = RunCommandWithFeedBack(Device.ModemPort, "AT+CMGF=0");
                    if (!Data.Contains("OK")) { throw new Exception($"Init Failure:{Data}"); }
                }
                //Set SMS Position
                Data = RunCommandWithFeedBack(Device.ModemPort, "AT+CPMS= \"ME\",\"ME\",\"ME\"");
                if (!Data.Contains("OK")) { throw new Exception($"Init Failure:{Data}"); }
                //Set Feedback Info
                Data = RunCommandWithFeedBack(Device.ModemPort, "AT+CRC=1");
                if (!Data.Contains("OK")) { throw new Exception($"Init Failure:{Data}"); }
                Data = RunCommandWithFeedBack(Device.ModemPort, "AT+CLIP=1");
                if (!Data.Contains("OK")) { throw new Exception($"Init Failure:{Data}"); }
                Data = RunCommandWithFeedBack(Device.ModemPort, "AT^CURC=1");
                if (!Data.Contains("OK")) { throw new Exception($"Init Failure:{Data}"); }
            }
            catch (Exception ex)
            {
                throw new Exception($"I/O Exception: {ex.Message}");
            }
        }
        public static void ReceiveSMS(Settings.DevicesItem Device)
        {
            var Data = RunCommandWithFeedBack(Device.ModemPort, "AT+CPMS= \"ME\",\"ME\",\"ME\"");
            if (!Data.Contains("OK")) { throw new Exception($"Init Failure:{Data}"); }
            Data = RunCommandWithFeedBack(Device.ModemPort, "AT+CPMS?");
            if (!Data.Contains("OK")) { throw new Exception($"Init Failure:{Data}"); }
            var RegexStr = "\\r\\n\\+CPMS: [\\\"A-Z,0-9]*\\r\\n";
            Data = Regex.Match(Data, RegexStr).ToString().Replace("\r\n", "").Replace("+CPMS: ", "").Replace("\"", "");
            var SMSNum = Int32.Parse(Data.Split(',')[1]);
            if (SMSNum == 0) return;
            for (var SMSCount = 0; SMSNum >= SMSCount; SMSCount++)
            {
                Data = RunCommandWithFeedBack(Device.ModemPort, "AT+CMGR=" + SMSCount.ToString());
                if (!Data.Contains("OK")) { throw new Exception("Recv Failure"); }
                RegexStr = $"\r\n([0-9A-F]*)\r\n";
                Data = Regex.Match(Data, RegexStr).ToString().Replace("\r\n", "");
                if (Data == "")
                {
                    continue;
                }
                GsmComm.PduConverter.IncomingSmsPdu PDU = IncomingSmsPdu.Decode(Data, true);
                var SendTime = "GMT" + PDU.GetTimestamp().ToDateTime().ToString("z yyyy-MM-dd HH:mm:ss");
                var ReceiveTime = "GMT" + DateTime.Now.ToString("z yyyy-MM-dd HH:mm:ss");
                var SMSC = PDU.SmscAddress;
                var Msg = PDU as SmsDeliverPdu;
                var From = Msg.OriginatingAddress;
                var To = Device.Name + "@" + Device.PhoneNumber;
                var Tittle = From + "->" + To;
                Data = "From:" + From + "\r\n" + "To:" + To + "\r\n" + "Send:" + SendTime + "\r\n" + "Received:" + ReceiveTime + "\r\n" + "SMSC:" + SMSC + "\r\n" + PDU.UserDataText;
                Console.WriteLine($"{Device.Name}-SMS Received:{Tittle}");
                Console.WriteLine($"SMS:{Data}");
                try
                {
                    Device.PostMessage(Tittle, Data);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Push Exception: {ex.Message}");
                }
                Data = RunCommandWithFeedBack(Device.ModemPort, "AT+CMGD=" + SMSCount.ToString());
            }
        }
        public static void ReceiveStatus(Settings.DevicesItem Device)
        {
            if (Device.DiagnosePort != null)
            {
                if (Device.DiagnosePort.IsOpen)
                {
                    var Data = Device.DiagnosePort.ReadExisting().Replace("\r\n\r\n", "\r\n").TrimStart('\r').TrimStart('\n').TrimEnd('\n').TrimEnd('\r');
                    if (Data == "") { return; }
                    var Status = Data.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                    var Start = (DateTime.Now.ToUniversalTime().Ticks - 621355968000000000) / 10000;
                    foreach (var Response in Status)
                    {
                        if (Response.Contains("^RSSI: "))
                        {
                            Device.Signal = Data.Split(':')[1];
                        }
                        else if (Response.Contains("+CRING: "))
                        {
                            Console.WriteLine($"{Device.Name}-IncomingCall");
                            //Refuse Call
                            //var Feedback = RunCommandWithFeedBack(Device.ModemPort, "AT+CHUP");
                            //Console.WriteLine(Feedback);
                            if (Device.Busy == false)
                            {
                                Device.Busy = true;
                                AnswerCall(Device);
                                Device.Busy = false;
                            }
                        }
                        else if (Response.Contains("+CLIP: "))
                        {
                            var Args = Response.Replace("+CLIP: ", "").Replace("\"", "").Split(',');
                            Console.WriteLine($"{Device.Name}-IncomingNumber-{Args[0]}");
                        }
                        else if (Response.Contains("^CEND:"))
                        {
                            var Args = Response.Replace("^CEND:", "").Replace("\"", "").Split(',');
                            Console.WriteLine($"{Device.Name}-IncomingCallEnd-{Args[0]}");
                        }
                        else
                        {
                            Console.WriteLine($"{Device.Name}-{Response}");
                        }
                    }
                }
            }
        }
        public static void AnswerCall(Settings.DevicesItem Device)
        {
            // 32000khz 16bit mono 
            var WAVReader = new WaveFileReader("1.wav");
            //var resampler = new WdlResamplingSampleProvider(WAVReader.ToSampleProvider(), 8000);
            //var monoSource = resampler.ToMono().ToWaveProvider16();
            byte[] bytesOutput = new byte[1280];
            var Data = RunCommandWithFeedBack(Device.ModemPort, "ATA");
            Console.WriteLine(Data);
            Data = RunCommandWithFeedBack(Device.ModemPort, "AT^DDSETEX=2");
            Console.WriteLine(Data);
            var Start = (DateTime.Now.ToUniversalTime().Ticks - 621355968000000000) / 10000;
            var WaveFormat = new WaveFormat(8000, 16, 1);
            var WAVWriter = new WaveFileWriter($"{Device.Name}-{Start}.wav", WaveFormat);
            //var bufferedWaveProvider = new BufferedWaveProvider(WaveFormat);
            //bufferedWaveProvider.BufferLength = 1024000;
            //bufferedWaveProvider.DiscardOnBufferOverflow = true;
            //WaveOut waveout = new WaveOut();
            //waveout.Init(bufferedWaveProvider);
            byte[] ReadByteBuffer = new byte[16000];
            while (((DateTime.Now.ToUniversalTime().Ticks - 621355968000000000) / 10000) - Start < 50000)
            {
                int bytesRead = WAVReader.Read(bytesOutput, 0, bytesOutput.Length);
                if (bytesRead != 0)
                {
                    Device.VoicePort.Write(bytesOutput, 0, bytesRead);
                }
                //var step = 1024;
                //using (var ms = new MemoryStream())
                //{
                //    int sourcePos = 0;
                //    while (sourcePos < rawSource.Length)
                //    {
                //        var remain = (int)rawSource.Length - sourcePos;
                //        var thisStepLength = remain > step ? step : remain;
                //        byte[] stepBuffer;
                //        stepBuffer = new byte[thisStepLength];
                //        rawSource.Read(stepBuffer, sourcePos, thisStepLength);
                //        sourcePos += thisStepLength;

                //        Console.WriteLine($"Readed {sourcePos}/{(int)rawSource.Length}");
                //        Device.VoicePort.Write(stepBuffer, 0, stepBuffer.Length);
                //        Console.WriteLine($"Sended {sourcePos}/{(int)rawSource.Length}");
                //    }
                //}
                //try
                //{
                //    int intLength = (int)rawSource.Length;
                //    byte[] byteBuffer = new byte[intLength];
                //    rawSource.Read(byteBuffer, 0, intLength);
                //    Device.VoicePort.Write(byteBuffer, 0,1020);
                //} catch (Exception ex)
                //{

                //}
                //var ReadData = Device.VoicePort.ReadExisting();
                //if (ReadData == "") continue;
                //byte[] BufferBytes = System.Text.Encoding.Default.GetBytes(ReadData);


                var ReadLen = Device.VoicePort.Read(ReadByteBuffer, 0, 16000);
                //bufferedWaveProvider.AddSamples(ReadByteBuffer, 0, ReadLen);
                WAVWriter.Write(ReadByteBuffer, 0, ReadLen);
                WAVWriter.Flush();
                //if (waveout.PlaybackState != PlaybackState.Playing)
                //{
                //    waveout.Play();
                //}
            }
            WAVWriter.Close();
            Data = RunCommandWithFeedBack(Device.ModemPort, "AT+CHUP");
            Console.WriteLine(Data);
            Data = RunCommandWithFeedBack(Device.ModemPort, "ATH");
            Console.WriteLine(Data);
        }
        public static void DetectModemByIMEI(Settings.DevicesItem Device)
        {
            Device.ModemPortName = "";
            Device.DiagnosePortName = "";
            Device.VoicePortName = "";
            var searcher = new ManagementObjectSearcher("root\\cimv2", "SELECT * FROM Win32_POTSModem");
            var collection = searcher.Get();
            foreach (var device in collection)
            {
                try
                {
                    var PortName = device.GetPropertyValue("AttachedTo").ToString();
                    var ComPort = new SerialPort(PortName);
                    Console.WriteLine($"Detect {PortName}");
                    ComPort.WriteTimeout = 200;
                    ComPort.ReadTimeout = 200;
                    ComPort.Open();
                    Console.WriteLine($"Opened {PortName}");
                    var Data = RunCommandWithFeedBack(ComPort, "ATE");
                    if (Data.Contains("OK"))
                    {
                        //Modem Port
                        var DeviceID = device.GetPropertyValue("DeviceID").ToString().Split('\\')[2].Split('&')[1];
                        var PortIMEI = Regex.Match(RunCommandWithFeedBack(ComPort, "AT+CGSN"), "\\r\\n[A=Za-z0-9]*\\r\\n").ToString().Replace("\r\n", "");
                        if (PortIMEI == Device.IMEI)
                        {
                            Device.ModemPortName = PortName;
                            Device.DiagnosePortName = Modem.DetectDiagnosePort(DeviceID);
                            Device.VoicePortName = Modem.DetectVoicePort(DeviceID);
                            ComPort.Close();
                            Console.WriteLine($"{Device.Name}-ModemPort Works on {Device.ModemPortName}");
                            Console.WriteLine($"{Device.Name}-DiagnosePort Works on {Device.DiagnosePortName}");
                            Console.WriteLine($"{Device.Name}-Voice Works on {Device.VoicePortName}");
                            return;
                        }
                    }
                    ComPort.Close();
                }
                catch
                {
                    continue;
                }
            }
        }

        static string DetectDiagnosePort(string SerialID)
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\cimv2", "SELECT * FROM Win32_PnPEntity WHERE ClassGuid=\"{4d36e978-e325-11ce-bfc1-08002be10318}\"");
            var collection = searcher.Get();
            foreach (var device in collection)
            {
                var DeviceID = device.GetPropertyValue("DeviceID").ToString().Split('\\')[2].Split('&')[1];
                var Name = device.GetPropertyValue("Name").ToString();
                if (SerialID == DeviceID)
                {
                    if (Name.Contains("PC UI Interface"))
                    {
                        var PortName = Regex.Match(device.GetPropertyValue("Name").ToString(), "(COM[0-9]*)");
                        return PortName.ToString();
                    }

                }
            }
            return "";
        }
        static string DetectVoicePort(string SerialID)
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\cimv2", "SELECT * FROM Win32_PnPEntity WHERE ClassGuid=\"{4d36e978-e325-11ce-bfc1-08002be10318}\"");
            var collection = searcher.Get();
            foreach (var device in collection)
            {
                var DeviceID = device.GetPropertyValue("DeviceID").ToString().Split('\\')[2].Split('&')[1];
                var Name = device.GetPropertyValue("Name").ToString();
                if (SerialID == DeviceID)
                {
                    if (Name.Contains("Application Interface"))
                    {
                        var PortName = Regex.Match(device.GetPropertyValue("Name").ToString(), "(COM[0-9]*)");
                        return PortName.ToString();
                    }
                }
            }
            return "";
        }
        static string RunCommandWithFeedBack(SerialPort ComPort, String Command)
        {
            var Result = "";
            ComPort.Write(Command + "\r\n");
            var Start = (DateTime.Now.ToUniversalTime().Ticks - 621355968000000000) / 10000;
            while (((DateTime.Now.ToUniversalTime().Ticks - 621355968000000000) / 10000) - Start < 500)
            {
                var Feedback = ComPort.ReadExisting().Replace("\r\r\n", "\r\n").Replace("\r\n\r\n", "\r\n");
                Thread.Sleep(100);
                if (Feedback == "") continue;
                if (Result != Result + Feedback)
                {
                    Result = Result + Feedback;
                }
                else
                {
                    break;
                }
            }
            Console.WriteLine(Result);
            return Result.Replace("\r\r\n", "\r\n").Replace("\r\n\r\n", "\r\n");
        }
    }
}
