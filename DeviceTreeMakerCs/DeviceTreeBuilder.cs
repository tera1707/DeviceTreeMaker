using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace DeviceTreeMakerCs
{
    internal sealed class DeviceTreeBuilder : IDisposable
    {
        private IntPtr _hDevInfo;
        private Dictionary<uint, NativeMethods.SP_DEVINFO_DATA>? _devInfoMap;
        private bool _disposed;

        public DeviceTreeBuilder()
        {
            _hDevInfo = NativeMethods.SetupDiGetClassDevs(IntPtr.Zero, null, IntPtr.Zero,
                NativeMethods.DIGCF_ALLCLASSES | NativeMethods.DIGCF_PRESENT);

            if (_hDevInfo == IntPtr.Zero || _hDevInfo.ToInt64() == -1)
            {
                throw new InvalidOperationException("SetupDiGetClassDevs failed.");
            }
        }

        public DeviceNode Build()
        {
            EnsureNotDisposed();

            _devInfoMap ??= BuildDevInfoMap(_hDevInfo);

            if (NativeMethods.CM_Locate_DevNode(out var rootDevInst, null, 0) != NativeMethods.CR_SUCCESS)
            {
                throw new InvalidOperationException("ルートデバイスの取得に失敗しました。");
            }

            return BuildTree(rootDevInst, 0);
        }

        private Dictionary<uint, NativeMethods.SP_DEVINFO_DATA> BuildDevInfoMap(IntPtr hDevInfo)
        {
            var map = new Dictionary<uint, NativeMethods.SP_DEVINFO_DATA>();
            uint index = 0;
            while (true)
            {
                var data = new NativeMethods.SP_DEVINFO_DATA { cbSize = Marshal.SizeOf<NativeMethods.SP_DEVINFO_DATA>() };
                if (!NativeMethods.SetupDiEnumDeviceInfo(hDevInfo, index, ref data))
                    break;
                map[data.DevInst] = data;
                index++;
            }
            return map;
        }

        private DeviceNode BuildTree(uint devInst, int depth)
        {
            string instanceId = GetDeviceId(devInst);
            string displayName = _devInfoMap!.TryGetValue(devInst, out var data)
                ? GetDeviceFriendlyName(_hDevInfo, data)
                : "(FriendlyName取得不可)";

            uint? status = null;
            uint? problem = null;
            if (NativeMethods.CM_Get_DevNode_Status(out var s, out var p, devInst, 0) == NativeMethods.CR_SUCCESS)
            {
                status = s;
                problem = p;
            }

            Guid? classGuid = _devInfoMap.TryGetValue(devInst, out var d) ? d.ClassGuid : null;

            IReadOnlyList<string> hardwareIds = Array.Empty<string>();
            if (_devInfoMap.TryGetValue(devInst, out var di))
            {
                hardwareIds = GetHardwareIds(_hDevInfo, di);
            }

            var node = new DeviceNode(instanceId, displayName, depth,
                                       devInstRaw: devInst,
                                       classGuid: classGuid,
                                       status: status,
                                       problemCode: problem,
                                       hardwareIds: hardwareIds);

            if (NativeMethods.CM_Get_Child(out var child, devInst, 0) == NativeMethods.CR_SUCCESS)
            {
                var current = child;
                do
                {
                    node.Children.Add(BuildTree(current, depth + 1));
                } while (NativeMethods.CM_Get_Sibling(out current, current, 0) == NativeMethods.CR_SUCCESS);
            }
            return node;
        }

        private static string GetDeviceFriendlyName(IntPtr hDevInfo, NativeMethods.SP_DEVINFO_DATA data)
        {
            byte[] buffer = new byte[512];
            uint regType;
            uint required;

            if (NativeMethods.SetupDiGetDeviceRegistryProperty(hDevInfo, ref data, NativeMethods.SPDRP_FRIENDLYNAME, out regType, buffer, (uint)buffer.Length, out required))
            {
                return BytesToUnicodeString(buffer);
            }
            if (NativeMethods.SetupDiGetDeviceRegistryProperty(hDevInfo, ref data, NativeMethods.SPDRP_DEVICEDESC, out regType, buffer, (uint)buffer.Length, out required))
            {
                return BytesToUnicodeString(buffer);
            }
            return "(取得不可)";
        }

        private static IReadOnlyList<string> GetHardwareIds(IntPtr hDevInfo, NativeMethods.SP_DEVINFO_DATA data)
        {
            // First call to get required size
            uint regType;
            uint required;
            byte[] empty = Array.Empty<byte>();
            NativeMethods.SetupDiGetDeviceRegistryProperty(hDevInfo, ref data, NativeMethods.SPDRP_HARDWAREID, out regType, empty, 0, out required);

            if (required == 0)
            {
                return Array.Empty<string>();
            }

            var buffer = new byte[required];
            if (!NativeMethods.SetupDiGetDeviceRegistryProperty(hDevInfo, ref data, NativeMethods.SPDRP_HARDWAREID, out regType, buffer, (uint)buffer.Length, out required))
            {
                return Array.Empty<string>();
            }

            return ParseRegMultiSz(buffer);
        }

        private static IReadOnlyList<string> ParseRegMultiSz(byte[] buffer)
        {
            // REG_MULTI_SZ (UTF-16LE) -> list of strings, terminated by double null
            string s = Encoding.Unicode.GetString(buffer);
            // Remove trailing nulls
            s = s.TrimEnd('\0');
            if (s.Length == 0) return Array.Empty<string>();
            var parts = s.Split('\0');
            var list = new List<string>(parts.Length);
            foreach (var p in parts)
            {
                if (!string.IsNullOrEmpty(p)) list.Add(p);
            }
            return list;
        }

        private static string BytesToUnicodeString(byte[] bytes)
        {
            string s = Encoding.Unicode.GetString(bytes);
            int nullIndex = s.IndexOf('\0');
            return nullIndex >= 0 ? s[..nullIndex] : s;
        }

        private static string GetDeviceId(uint devInst)
        {
            var sb = new StringBuilder(NativeMethods.MAX_DEVICE_ID_LEN);
            return NativeMethods.CM_Get_Device_ID(devInst, sb, sb.Capacity, 0) == NativeMethods.CR_SUCCESS ? sb.ToString() : "(取得失敗)";
        }

        private void EnsureNotDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DeviceTreeBuilder));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_hDevInfo != IntPtr.Zero && _hDevInfo.ToInt64() != -1)
            {
                NativeMethods.SetupDiDestroyDeviceInfoList(_hDevInfo);
            }
            _hDevInfo = IntPtr.Zero;
        }
    }
}
