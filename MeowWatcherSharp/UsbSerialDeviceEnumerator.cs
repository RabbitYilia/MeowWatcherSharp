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

            var hDevInfoSet = NativeMethods.SetupDiGetClassDevs(
                ref gid,
                null,
                IntPtr.Zero,
                presentOnly ? NativeMethods.DiGetClassFlags.DIGCF_PRESENT : 0);

            if (hDevInfoSet.ToInt64() == NativeMethods.INVALID_HANDLE_VALUE)
            {
                yield break;
            }

            try
            {
                var devInfoData = new NativeMethods.DevInfoData { CbSize = (uint)Marshal.SizeOf<NativeMethods.DevInfoData>() };

                for (uint i = 0; NativeMethods.SetupDiEnumDeviceInfo(hDevInfoSet, i, ref devInfoData); i++)
                {
                    var id = GetDeviceIds(hDevInfoSet, devInfoData);

                    var device = new ModemDevice
                    {
                        PortName = GetPortName(hDevInfoSet, devInfoData),
                        FriendlyName = GetFriendlyName(hDevInfoSet, devInfoData),
                        Description = GetDescription(hDevInfoSet, devInfoData),
                        ID = id["ID"],
                        Vid = id.ContainsKey("VID") ? (int?)int.Parse(id["VID"], System.Globalization.NumberStyles.HexNumber) : null,
                        Pid = id.ContainsKey("PID") ? (int?)int.Parse(id["PID"], System.Globalization.NumberStyles.HexNumber) : null,
                        Rev = id.ContainsKey("REV") ? (int?)int.Parse(id["REV"], System.Globalization.NumberStyles.HexNumber) : null,
                        MI = id.ContainsKey("MI") ? (int?)int.Parse(id["MI"], System.Globalization.NumberStyles.HexNumber) : null,
                    };
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
                NativeMethods.SetupDiDestroyDeviceInfoList(hDevInfoSet);
            }
        }

        public static IEnumerable<ModemDevice> EnumerateSerialDevices(bool presentOnly = true)
        {
            var hDevInfoSet = NativeMethods.SetupDiGetClassDevs(
                ref NativeMethods.GUID_DEVINTERFACE_SERENUM_BUS_ENUMERATOR,
                null,
                IntPtr.Zero,
                presentOnly ? NativeMethods.DiGetClassFlags.DIGCF_PRESENT : 0);
           
            if (hDevInfoSet.ToInt64() == NativeMethods.INVALID_HANDLE_VALUE)
            {
                yield break;
            }

            try
            {
                var devInfoData = new NativeMethods.DevInfoData {CbSize = (uint)Marshal.SizeOf<NativeMethods.DevInfoData>()};

                for (uint i = 0; NativeMethods.SetupDiEnumDeviceInfo(hDevInfoSet, i, ref devInfoData); i++)
                {
                    var id = GetDeviceIds(hDevInfoSet, devInfoData);

                    var device = new ModemDevice
                    {
                        PortName = GetPortName(hDevInfoSet, devInfoData),
                        FriendlyName = GetFriendlyName(hDevInfoSet, devInfoData),
                        Description = GetDescription(hDevInfoSet, devInfoData),
                        ID = id["ID"],
                        Vid = id.ContainsKey("VID") ? (int?)int.Parse(id["VID"], System.Globalization.NumberStyles.HexNumber) : null,
                        Pid = id.ContainsKey("PID") ? (int?)int.Parse(id["PID"], System.Globalization.NumberStyles.HexNumber) : null,
                        Rev = id.ContainsKey("REV") ? (int?)int.Parse(id["REV"], System.Globalization.NumberStyles.HexNumber) : null,
                        MI = id.ContainsKey("MI") ? (int?)int.Parse(id["MI"], System.Globalization.NumberStyles.HexNumber) : null,
                    };

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
                NativeMethods.SetupDiDestroyDeviceInfoList(hDevInfoSet);
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
