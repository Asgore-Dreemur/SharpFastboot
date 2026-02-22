using System.Runtime.InteropServices;

namespace SharpFastboot.Usb.Windows
{
    public class WinUSBAPI
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct WinUSBPipeInfo
        {
            public WinUSBPipeType PipeType;
            public byte PipeID;
            public ushort MaximumPacketSize;
            public byte Interval;
        }

        public enum WinUSBPipeType
        {
            UsbdPipeTypeControl,
            UsbdPipeTypeIsochronous,
            UsbdPipeTypeBulk,
            UsbdPipeTypeInterrupt
        }

        public static readonly byte USB_DEVICE_DESCRIPTOR_TYPE = 0x01;
        public static readonly byte USB_CONFIGURATION_DESCRIPTOR_TYPE = 0x02;
        public static readonly byte USB_ENDPOINT_DIRECTION_MASK = 0x80;
        public static readonly byte USB_STRING_DESCRIPTOR_TYPE = 0x03;

        [DllImport("Winusb.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern bool WinUsb_Initialize(IntPtr DeviceHandle, out IntPtr InterfaceHandle);

        [DllImport("Winusb.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern bool WinUsb_Free(IntPtr InterfaceHandle);

        [DllImport("Winusb.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern bool WinUsb_GetCurrentAlternateSetting(IntPtr InterfaceHandle, out byte InterfaceNum);

        [DllImport("Winusb.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern bool WinUsb_GetDescriptor(IntPtr DeviceHandle, byte DescriptorType, byte index, ushort LangID,
            IntPtr buffer, uint bufferLen, out uint lengthTransfered);

        [DllImport("Winusb.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern bool WinUsb_QueryInterfaceSettings(IntPtr DeviceHandle, byte interfaceNum, out Win32API.USBDeviceInterfaceDescriptor descriptor);

        [DllImport("Winusb.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern bool WinUsb_QueryPipe(IntPtr DeviceHandle, byte interfaceNum, byte pipeIndex, out WinUSBPipeInfo info);

        [DllImport("Winusb.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern bool WinUsb_WritePipe(IntPtr DeviceHandle, byte pipeID, byte[] buffer,
            ulong bufferLen, out ulong bytesTransfered, IntPtr overlapp);

        [DllImport("Winusb.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern bool WinUsb_ReadPipe(IntPtr DeviceHandle, byte pipeID, byte[] buffer,
            ulong bufferLen, out ulong bytesTransfered, IntPtr overlapp);

        [DllImport("Winusb.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern bool WinUsb_ResetPipe(IntPtr InterfaceHandle, byte PipeID);

        [DllImport("Winusb.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern bool WinUsb_SetPipePolicy(IntPtr InterfaceHandle, byte PipeID, uint PolicyType, uint ValueLength, ref uint Value);

        [DllImport("Winusb.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern bool WinUsb_SetPipePolicy(IntPtr InterfaceHandle, byte PipeID, uint PolicyType, uint ValueLength, ref byte Value);

        // Pipe Policies
        public const uint SHORT_PACKET_TERMINATE = 0x01;
        public const uint AUTO_CLEAR_STALL = 0x02;
        public const uint PIPE_TRANSFER_TIMEOUT = 0x03;
        public const uint IGNORE_SHORT_PACKETS = 0x04;
        public const uint ALLOW_PARTIAL_READS = 0x05;
        public const uint AUTO_FLUSH = 0x06;
        public const uint RAW_IO = 0x07;

    }
}
