using System.ComponentModel;
using System.Runtime.InteropServices;
using static SharpFastboot.Usb.Windows.Win32API;
using static SharpFastboot.Usb.Windows.WinUSBAPI;

namespace SharpFastboot.Usb.Windows
{
    public class WinUSBDevice : UsbDevice
    {
        private byte InterfaceNum;
        private byte ReadBulkID, WriteBulkID;
        private byte ReadBulkIndex, WriteBulkIndex;
        private IntPtr WinUSBHandle;
        private IntPtr FileHandle;
        private USBDeviceDescriptor USBDeviceDescriptor;
        private USBDeviceConfigDescriptor USBDeviceConfigDescriptor;
        private USBDeviceInterfaceDescriptor USBDeviceInterfaceDescriptor;

        public override int CreateHandle()
        {
            IntPtr hUsb = SimpleCreateHandle(DevicePath, true);
            uint bytesTransfered;
            if (hUsb == INVALID_HANDLE_VALUE)
                return Marshal.GetLastWin32Error();
            FileHandle = hUsb;
            if (!WinUsb_Initialize(hUsb, out WinUSBHandle))
                return Marshal.GetLastWin32Error();
            if (!WinUsb_GetCurrentAlternateSetting(WinUSBHandle, out InterfaceNum))
                return Marshal.GetLastWin32Error();
            IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(USBDeviceDescriptor));
            if (!WinUsb_GetDescriptor(WinUSBHandle, USB_DEVICE_DESCRIPTOR_TYPE, 0, 0, ptr, (uint)Marshal.SizeOf(USBDeviceDescriptor), out bytesTransfered))
                return Marshal.GetLastWin32Error();
            USBDeviceDescriptor = Marshal.PtrToStructure<USBDeviceDescriptor>(ptr);
            Marshal.FreeHGlobal(ptr);
            ptr = Marshal.AllocHGlobal(Marshal.SizeOf(USBDeviceConfigDescriptor));
            if (!WinUsb_GetDescriptor(WinUSBHandle, USB_CONFIGURATION_DESCRIPTOR_TYPE, 0, 0, ptr, (uint)Marshal.SizeOf(USBDeviceConfigDescriptor), out bytesTransfered))
                return Marshal.GetLastWin32Error();
            USBDeviceConfigDescriptor = Marshal.PtrToStructure<USBDeviceConfigDescriptor>(ptr);
            Marshal.FreeHGlobal(ptr);
            if (!WinUsb_QueryInterfaceSettings(WinUSBHandle, InterfaceNum, out USBDeviceInterfaceDescriptor))
                return Marshal.GetLastWin32Error();

            for (byte endpoint = 0; endpoint < USBDeviceInterfaceDescriptor.bNumEndpoints; endpoint++)
            {
                WinUSBPipeInfo pipeInfo;
                if (!WinUsb_QueryPipe(WinUSBHandle, InterfaceNum, endpoint, out pipeInfo))
                    return Marshal.GetLastWin32Error();
                if (pipeInfo.PipeType == WinUSBPipeType.UsbdPipeTypeBulk)
                {
                    if ((pipeInfo.PipeID & USB_ENDPOINT_DIRECTION_MASK) != 0)
                    {
                        if (ReadBulkID == 0)
                        {
                            ReadBulkID = pipeInfo.PipeID;
                            ReadBulkIndex = endpoint;
                        }
                    }
                    else
                    {
                        if (WriteBulkID == 0)
                        {
                            WriteBulkID = pipeInfo.PipeID;
                            WriteBulkIndex = endpoint;
                        }
                    }
                }
            }
            GetSerialNumber();

            byte bTrue = 1;
            byte bFalse = 0;
            uint timeout = 5000;
            WinUsb_SetPipePolicy(WinUSBHandle, ReadBulkID, AUTO_CLEAR_STALL, 1, ref bTrue);
            WinUsb_SetPipePolicy(WinUSBHandle, WriteBulkID, AUTO_CLEAR_STALL, 1, ref bTrue);
            WinUsb_SetPipePolicy(WinUSBHandle, ReadBulkID, PIPE_TRANSFER_TIMEOUT, 4, ref timeout);
            WinUsb_SetPipePolicy(WinUSBHandle, WriteBulkID, PIPE_TRANSFER_TIMEOUT, 4, ref timeout);
            WinUsb_SetPipePolicy(WinUSBHandle, WriteBulkID, SHORT_PACKET_TERMINATE, 1, ref bFalse);
            return 0;
        }

        public override void Reset()
        {
            if (WinUSBHandle != IntPtr.Zero)
            {
                if (ReadBulkID != 0) WinUsb_ResetPipe(WinUSBHandle, ReadBulkID);
                if (WriteBulkID != 0) WinUsb_ResetPipe(WinUSBHandle, WriteBulkID);
            }
        }

        public override int GetSerialNumber()
        {
            uint bytes_get;
            uint descriptorSize = 64;
            IntPtr ptr = Marshal.AllocHGlobal((int)descriptorSize);
            while (!WinUsb_GetDescriptor(WinUSBHandle, USB_STRING_DESCRIPTOR_TYPE,
                USBDeviceDescriptor.iSerialNumber, 0x0409,
                ptr, descriptorSize, out bytes_get))
            {
                if ((uint)Marshal.GetLastWin32Error() != (uint)ERROR_INSUFFICIENT_BUFFER)
                    return Marshal.GetLastWin32Error();
                descriptorSize *= 2;
                Marshal.FreeHGlobal(ptr);
                ptr = Marshal.AllocHGlobal((int)descriptorSize);
            }
            SerialNumber = Marshal.PtrToStringUni(ptr + 2, (int)(bytes_get - 2) / 2);
            Marshal.FreeHGlobal(ptr);
            return 0;
        }

        public override byte[] Read(int length)
        {
            byte[] data = new byte[length];
            ulong bytesTransfered;
            if (WinUsb_ReadPipe(WinUSBHandle, ReadBulkID, data, (ulong)length, out bytesTransfered, IntPtr.Zero))
            {
                byte[] realData = new byte[bytesTransfered];
                Array.Copy(data, realData, (int)bytesTransfered);
                return realData;
            }
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        public override long Write(byte[] data, int length)
        {
            ulong bytesWrite = 0;
            if (WinUSBHandle == IntPtr.Zero)
                throw new Exception("Device handle is closed.");

            if (WinUsb_WritePipe(WinUSBHandle, WriteBulkID, data, (uint)length, out bytesWrite, IntPtr.Zero))
                return (long)bytesWrite;
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        public override void Dispose()
        {
            if (WinUSBHandle != IntPtr.Zero)
            {
                WinUsb_Free(WinUSBHandle);
                WinUSBHandle = IntPtr.Zero;
            }
            if (FileHandle != IntPtr.Zero && FileHandle != INVALID_HANDLE_VALUE)
            {
                CloseHandle(FileHandle);
                FileHandle = IntPtr.Zero;
            }
        }
    }
}
