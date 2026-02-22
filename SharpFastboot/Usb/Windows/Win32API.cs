using System.Runtime.InteropServices;

namespace SharpFastboot.Usb.Windows
{
    public class Win32API
    {
        //File
        public static uint GENERIC_READ { get; } = 0x80000000;
        public static uint GENERIC_WRITE { get; } = 0x40000000;
        public static uint FILE_SHARE_READ { get; } = 0x00000001;
        public static uint FILE_SHARE_WRITE { get; } = 0x00000002;
        public static uint OPEN_EXISTING { get; } = 3;
        public static int INVALID_HANDLE_VALUE { get; } = -1;
        public static uint FILE_DEVICE_UNKNOWN { get; } = 0x00000022;
        public static uint METHOD_BUFFERED { get; } = 0;
        public static uint FILE_READ_ACCESS { get; } = 1;
        public static uint FILE_FLAG_OVERLAPPED { get; } = 0x40000000;

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern nint CreateFileW([MarshalAs(UnmanagedType.LPWStr)] string fileName, uint access,
                                             uint shareMode, nint securityAttributes,
                                             uint createDisposition, uint flagsAndAttributes,
                                             nint template);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern bool DeviceIoControl(nint device, uint code,
                                                  byte[] inBuffer, int inBufferSize,
                                                  byte[] outBuffer, int outBufferSize,
                                                  out int bytesReturned, nint overlapped);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern bool CloseHandle(nint handle);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern bool WriteFile(nint hFile, byte[] buffer, uint sizeToWrite, out ulong bytesWritten, nint overlapped);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern bool ReadFile(nint hFile, byte[] buffer, uint sizeToRead, out uint bytesRead, nint overlapped);

        //USB
        public struct GUID
        {
            public uint Data1;
            public ushort Data2;
            public ushort Data3;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] Data4;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SpDeviceInterfaceData
        {
            public uint cbSize;
            public GUID InterfaceClassGuid;
            public uint Flags;
            public nint Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct USBDeviceDescriptor
        {
            public byte bLength;
            public byte bDescriptorType;
            public ushort bcdUSB;
            public byte bDeviceClass;
            public byte bDeviceSubClass;
            public byte bDeviceProtocol;
            public byte bMaxPacketSize0;
            public ushort idVendor;
            public ushort idProduct;
            public ushort bcdDevice;
            public byte iManufacturer;
            public byte iProduct;
            public byte iSerialNumber;
            public byte bNumConfigurations;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct USBDeviceConfigDescriptor
        {
            public byte bLength;
            public byte bDescriptorType;
            public ushort wTotalLength;
            public byte bNumInterfaces;
            public byte bConfigurationValue;
            public byte iConfiguration;
            public byte bmAttributes;
            public byte MaxPower;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct USBDeviceInterfaceDescriptor
        {
            public byte bLength;
            public byte bDescriptorType;
            public byte bInterfaceNumber;
            public byte bAlternateSetting;
            public byte bNumEndpoints;
            public byte bInterfaceClass;
            public byte bInterfaceSubClass;
            public byte bInterfaceProtocol;
            public byte iInterface;
        }

        public static uint DIGCF_DEVICEINTERFACE { get; } = 0x00000010;

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern nint SetupDiGetClassDevsW(ref GUID guid, string? enumerator, int parent, uint flag);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern bool SetupDiEnumDeviceInterfaces(nint deviceInfoSet, nint deviceInfoData,
                                                              ref GUID interfaceClassGuid, uint index,
                                                              ref SpDeviceInterfaceData deviceInterfaceData);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        public static extern bool SetupDiGetDeviceInterfaceDetailW(nint deviceInfoSet, ref SpDeviceInterfaceData deviceInterfaceData,
                                                                   nint deviceInterfaceDetailData,
                                                                   uint detailSize,
                                                                   out uint requiredSize,
                                                                   nint deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiDestroyDeviceInfoList(nint deviceInfoSet);

        public static nint SimpleCreateHandle(string filePath, bool overlapped = false)
        {
            return CreateFileW(filePath,
                GENERIC_READ | GENERIC_WRITE,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                nint.Zero, OPEN_EXISTING,
                overlapped ? FILE_FLAG_OVERLAPPED : 0, nint.Zero);
        }

        //ErrorCode
        public static int ERROR_INSUFFICIENT_BUFFER { get; } = 122;
        public static int ERROR_NO_MORE_ITEMS { get; } = 259;
    }
}
