using System.ComponentModel;
using System.Runtime.InteropServices;
using static SharpFastboot.Usb.Windows.Win32API;

namespace SharpFastboot.Usb.Windows
{
    public class WinUSBFinder
    {
        private static GUID AndroidUsbGUID =
            new GUID
            {
                Data1 = 0xf72fe0d4,
                Data2 = 0xcbcb,
                Data3 = 0x407d,
                Data4 = [0x88, 0x14, 0x9e, 0xd6, 0x73, 0xd0, 0xdd, 0x6b]
            };

        public static readonly uint IoGetDescriptorCode = ((FILE_DEVICE_UNKNOWN) << 16) | ((FILE_READ_ACCESS) << 14) | ((10) << 2) | (METHOD_BUFFERED);

        public static List<UsbDevice> FindDevice()
        {
            List<UsbDevice> devices = new List<UsbDevice>();
            IntPtr devInfo = SetupDiGetClassDevsW(ref AndroidUsbGUID, null, 0, DIGCF_DEVICEINTERFACE);
            if (devInfo.ToInt64() == -1)
                throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                uint index;
                for (index = 0; ; index++)
                {
                    SpDeviceInterfaceData interfaceData = new SpDeviceInterfaceData();
                    interfaceData.cbSize = (uint)Marshal.SizeOf<SpDeviceInterfaceData>();
                    if (SetupDiEnumDeviceInterfaces(devInfo, IntPtr.Zero, ref AndroidUsbGUID, index, ref interfaceData))
                    {
                        uint sizeResult = GetInterfaceDetailDataRequiredSize(devInfo, interfaceData);
                        IntPtr buffer = Marshal.AllocHGlobal((int)sizeResult);
                        Marshal.WriteInt32(buffer, IntPtr.Size == 8 ? 8 : 6);
                        if (!SetupDiGetDeviceInterfaceDetailW(devInfo, ref interfaceData,
                            buffer, sizeResult, out _, IntPtr.Zero))
                        {
                            Marshal.FreeHGlobal(buffer);
                            throw new Win32Exception(Marshal.GetLastWin32Error());
                        }
                        else
                        {
                            string? devicePath = Marshal.PtrToStringUni(buffer + 4);
                            Marshal.FreeHGlobal(buffer);
                            if (string.IsNullOrEmpty(devicePath))
                                continue;
                            bool? isLegacy = isLegacyDevice(devicePath);
                            if (!isLegacy.HasValue)
                                continue;

                            UsbDevice usb;
                            if (isLegacy.Value)
                            {
                                usb = new LegacyUsbDevice { DevicePath = devicePath };
                                usb.UsbDeviceType = UsbDeviceType.WinLegacy;
                            }
                            else
                            {
                                usb = new WinUSBDevice { DevicePath = devicePath };
                                usb.UsbDeviceType = UsbDeviceType.WinUSB;
                            }
                            if (usb.CreateHandle() == 0)
                                devices.Add(usb);
                            else
                                usb.Dispose();
                        }
                    }
                    else
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error == ERROR_NO_MORE_ITEMS) break;
                        throw new Win32Exception(error);
                    }
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(devInfo);
            }
            return devices;
        }

        private static uint GetInterfaceDetailDataRequiredSize(IntPtr devInfo, SpDeviceInterfaceData interfaceData)
        {
            uint requiredSize;
            if (!SetupDiGetDeviceInterfaceDetailW(devInfo, ref interfaceData, IntPtr.Zero, 0, out requiredSize, IntPtr.Zero))
            {
                int error = Marshal.GetLastWin32Error();
                if (error == ERROR_INSUFFICIENT_BUFFER)
                    return requiredSize;
                throw new Win32Exception(error);
            }
            throw new Win32Exception(ERROR_INSUFFICIENT_BUFFER);
        }

        private static bool? isLegacyDevice(string devicePath)
        {
            byte[] data = new byte[32];
            int bytes_get;
            IntPtr hUsb = SimpleCreateHandle(devicePath);
            if (hUsb == INVALID_HANDLE_VALUE)
                return null;
            bool ret = DeviceIoControl(hUsb, IoGetDescriptorCode, Array.Empty<byte>(), 0, data, 32, out bytes_get, IntPtr.Zero);
            CloseHandle(hUsb);
            return ret;
        }
    }
}
