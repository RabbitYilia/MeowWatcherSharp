using System;
using System.Collections.Generic;
using System.ComponentModel;

using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace MeowWatcherSharp
{
    public static class UsbSerialDeviceEnumerator
    {
        public static IEnumerable<ModemDevice> EnumerateModemDevices( bool presentOnly = true)
        {
            var Type = "Modem";
            uint n = 0;
            Guid gid = new Guid();
            NativeMethods.SetupDiClassGuidsFromNameW(Type, ref gid, 0, ref n);
            NativeMethods.SetupDiClassGuidsFromNameW(Type, ref gid, n, ref n);

            var ModemhDevInfoSet = NativeMethods.SetupDiGetClassDevs(
                ref gid,
                null,
                IntPtr.Zero,
                presentOnly ? NativeMethods.DiGetClassFlags.DIGCF_PRESENT : 0);

            if (ModemhDevInfoSet.ToInt64() == NativeMethods.INVALID_HANDLE_VALUE)
            {
                yield break;
            }

            var SerialhDevInfoSet = NativeMethods.SetupDiGetClassDevs(
                ref NativeMethods.GUID_DEVINTERFACE_SERENUM_BUS_ENUMERATOR,
                null,
                IntPtr.Zero,
                presentOnly ? NativeMethods.DiGetClassFlags.DIGCF_PRESENT : 0);

            if (SerialhDevInfoSet.ToInt64() == NativeMethods.INVALID_HANDLE_VALUE)
            {
                yield break;
            }

            try
            {
                var ModemdevInfoData = new NativeMethods.DevInfoData { CbSize = (uint)Marshal.SizeOf<NativeMethods.DevInfoData>() };
                var SerialdevInfoData = new NativeMethods.DevInfoData { CbSize = (uint)Marshal.SizeOf<NativeMethods.DevInfoData>() };

                for (uint i = 0; NativeMethods.SetupDiEnumDeviceInfo(ModemhDevInfoSet, i, ref ModemdevInfoData); i++)
                {
                    var Modemid = GetDeviceIds(ModemhDevInfoSet, ModemdevInfoData);
                    var device = new ModemDevice
                    {
                        ModemPortName = GetPortName(ModemhDevInfoSet, ModemdevInfoData),
                        FriendlyName = GetFriendlyName(ModemhDevInfoSet, ModemdevInfoData),
                        Description = GetDescription(ModemhDevInfoSet, ModemdevInfoData),
                        ID = Modemid["ID"],
                        Vid = Modemid.ContainsKey("VID") ? (int?)int.Parse(Modemid["VID"], System.Globalization.NumberStyles.HexNumber) : null,
                        Pid = Modemid.ContainsKey("PID") ? (int?)int.Parse(Modemid["PID"], System.Globalization.NumberStyles.HexNumber) : null,
                        Rev = Modemid.ContainsKey("REV") ? (int?)int.Parse(Modemid["REV"], System.Globalization.NumberStyles.HexNumber) : null,
                        MI = Modemid.ContainsKey("MI") ? (int?)int.Parse(Modemid["MI"], System.Globalization.NumberStyles.HexNumber) : null,
                    };
                    for (uint j = 0; NativeMethods.SetupDiEnumDeviceInfo(SerialhDevInfoSet, j, ref SerialdevInfoData); j++)
                    {
                        var Serialid = GetDeviceIds(SerialhDevInfoSet, SerialdevInfoData);
                        if (Modemid["ID"] == Serialid["ID"])
                        {
                            var PortName = GetPortName(SerialhDevInfoSet, SerialdevInfoData);
                            var MI = Modemid.ContainsKey("MI") ? (int?)int.Parse(Serialid["MI"], System.Globalization.NumberStyles.HexNumber) : null;
                            switch (MI)
                            {
                                case 1:device.VoicePortName= PortName; break;
                                case 2: device.DiagnosePortName=PortName; break;
                            }
                        }
                    }
                    yield return device;
                }

                if (Marshal.GetLastWin32Error() != NativeMethods.NO_ERROR &&
                    Marshal.GetLastWin32Error() != NativeMethods.ERROR_NO_MORE_ITEMS)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to enumerate USB serial devices. Error: [{Marshal.GetLastWin32Error()}] HR: [{Marshal.GetHRForLastWin32Error()}]");
                }
            }
            finally
            {
                NativeMethods.SetupDiDestroyDeviceInfoList(ModemhDevInfoSet);
                NativeMethods.SetupDiDestroyDeviceInfoList(SerialhDevInfoSet);
            }
        }

        private static string GetPortName(IntPtr hDevInfoSet, NativeMethods.DevInfoData devInfoData)
        {
            var hRegKey = NativeMethods.SetupDiOpenDevRegKey(
                hDevInfoSet,
                ref devInfoData,
                NativeMethods.DeviceInfoPropertyScope.DICS_FLAG_GLOBAL,
                0,
                NativeMethods.DeviceInfoRegistryKeyType.DIREG_DEV,
                NativeMethods.RegistrySpecificAccessRights.KEY_QUERY_VALUE);

            if (hRegKey == IntPtr.Zero) return string.Empty;

            var safeHandle = new SafeRegistryHandle(hRegKey, true);

            var key = RegistryKey.FromHandle(safeHandle);
            return key.GetValue(@"PortName") as string;
        }

        private static string GetFriendlyName(IntPtr hDevInfoSet, NativeMethods.DevInfoData devInfoData)
        {
            var buffer = new StringBuilder(256);
            var length = (uint)buffer.Capacity;
            NativeMethods.SetupDiGetDeviceRegistryProperty(hDevInfoSet, ref devInfoData, NativeMethods.DeviceInfoRegistryProperty.SPDRP_FRIENDLYNAME, out _, buffer, length, out _);

            return buffer.ToString();
        }

        private static string GetDescription(IntPtr hDevInfoSet, NativeMethods.DevInfoData devInfoData)
        {
            var buffer = new StringBuilder(256);
            var length = (uint)buffer.Capacity;
            NativeMethods.SetupDiGetDeviceRegistryProperty(hDevInfoSet, ref devInfoData, NativeMethods.DeviceInfoRegistryProperty.SPDRP_DEVICEDESC, out _, buffer, length, out _);

            return buffer.ToString();
        }


        private static Dictionary<string, string> GetDeviceIds(IntPtr hDevInfoSet, NativeMethods.DevInfoData devInfoData)
        {
            var buffer = new StringBuilder(256);
            var length = (uint)buffer.Capacity;
            NativeMethods.SetupDiGetDeviceRegistryProperty(hDevInfoSet, ref devInfoData, NativeMethods.DeviceInfoRegistryProperty.SPDRP_HARDWAREID, out _, buffer, length, out _);
            var bufferID = new StringBuilder(256);
            var lengthID = (uint)buffer.Capacity;
            NativeMethods.SetupDiGetDeviceInstanceIdW(hDevInfoSet, ref devInfoData, bufferID, lengthID, out _);
            
            var result = new Dictionary<string, string>();

            var regex = new Regex(@"(?<Enum>[^\\]*)\\((?<ID>[^&]+)&?)+"); //Matches 'USB\VID_123&PID_456&REV_001' or 'root\GenericDevice'
            var regexID = new Regex(@"\\[A-Za-z0-9]&(?<ID>[A-Fa-f0-9]*)&"); //Matches 'USB\VID_123&PID_456&REV_001' or 'root\GenericDevice'

            var match = regex.Match(buffer.ToString());
            var matchID = regexID.Match(bufferID.ToString());
            if (!match.Success || !match.Groups["ID"].Success) return result; //empty result. The ID group should always match if the match succeeded. But testing here for completeness.
            result.Add("ID", matchID.Groups[1].ToString());
            foreach (var id in match.Groups["ID"].Captures)
            {
                var splitIndex = id.ToString().IndexOf('_');
                if (splitIndex < 0)
                {
                    if (id.ToString().Contains("PID")) result.Add("PID", id.ToString().Replace("PID", ""));
                    if (id.ToString().Contains("VID")) result.Add("VID", id.ToString().Replace("VID", ""));
                    if (id.ToString().Contains("REV")) result.Add("REV", id.ToString().Replace("REV", ""));
                }
                else { result.Add(id.ToString().Substring(0, splitIndex), id.ToString().Substring(splitIndex + 1)); }
            } 

            return result;
        }
    }
}
