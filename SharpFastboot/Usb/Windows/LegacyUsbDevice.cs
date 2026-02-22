using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using static SharpFastboot.Usb.Windows.Win32API;

namespace SharpFastboot.Usb.Windows
{
    public class LegacyUsbDevice : UsbDevice
    {
        public static readonly uint IoGetSerialCode = ((FILE_DEVICE_UNKNOWN) << 16) | ((FILE_READ_ACCESS) << 14) | ((16) << 2) | (METHOD_BUFFERED);
        public IntPtr DeviceHandle { get; private set; }

        public IntPtr ReadBulkHandle { get; private set; }

        public IntPtr WriteBulkHandle { get; private set; }

        public override int CreateHandle()
        {
            DeviceHandle = SimpleCreateHandle(DevicePath);
            ReadBulkHandle = SimpleCreateHandle(DevicePath + "\\BulkRead");
            WriteBulkHandle = SimpleCreateHandle(DevicePath + "\\BulkWrite");
            if (DeviceHandle == INVALID_HANDLE_VALUE ||
                ReadBulkHandle == INVALID_HANDLE_VALUE ||
                WriteBulkHandle == INVALID_HANDLE_VALUE)
                return Marshal.GetLastWin32Error();
            GetSerialNumber();
            return 0;
        }

        public override void Reset()
        {
            // Not supported in legacy driver
        }

        public override int GetSerialNumber()
        {
            byte[] serial = new byte[512];
            int bytes_get;
            if (DeviceIoControl(DeviceHandle, IoGetSerialCode, Array.Empty<byte>(), 0, serial, 512, out bytes_get, IntPtr.Zero))
            {
                SerialNumber = Encoding.UTF8.GetString(serial);
                return 0;
            }
            return Marshal.GetLastWin32Error();
        }

        public override byte[] Read(int length)
        {
            uint bytesRead;
            byte[] data = new byte[length];
            if (ReadBulkHandle == IntPtr.Zero)
                throw new Exception("Read handle is closed.");

            if (ReadFile(ReadBulkHandle, data, (uint)length, out bytesRead, IntPtr.Zero))
            {
                byte[] res = new byte[bytesRead];
                Array.Copy(data, res, bytesRead);
                return res;
            }
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        public override long Write(byte[] data, int length)
        {
            ulong bytesWrite = 0;
            if (WriteBulkHandle == IntPtr.Zero)
                throw new Exception("Write handle is closed.");

            if (WriteFile(WriteBulkHandle, data, (uint)length, out bytesWrite, IntPtr.Zero))
            {
                return (long)bytesWrite;
            }
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        public override void Dispose()
        {
            if (DeviceHandle != IntPtr.Zero && DeviceHandle != INVALID_HANDLE_VALUE)
            {
                CloseHandle(DeviceHandle);
                DeviceHandle = IntPtr.Zero;
            }
            if (ReadBulkHandle != IntPtr.Zero && ReadBulkHandle != INVALID_HANDLE_VALUE)
            {
                CloseHandle(ReadBulkHandle);
                ReadBulkHandle = IntPtr.Zero;
            }
            if (WriteBulkHandle != IntPtr.Zero && WriteBulkHandle != INVALID_HANDLE_VALUE)
            {
                CloseHandle(WriteBulkHandle);
                WriteBulkHandle = IntPtr.Zero;
            }
        }
    }
}
