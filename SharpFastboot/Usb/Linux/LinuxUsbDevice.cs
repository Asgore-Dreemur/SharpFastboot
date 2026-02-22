using System.Runtime.InteropServices;
using System.Text;
using static SharpFastboot.Usb.Linux.LinuxUsbAPI;

namespace SharpFastboot.Usb.Linux
{
    public class LinuxUsbDevice : UsbDevice
    {
        private int fd = -1;
        public byte ep_in { get; set; }
        public byte ep_out { get; set; }
        public int InterfaceId { get; set; }
        public byte iSerialNumber { get; set; }

        public override int CreateHandle()
        {
            fd = open(DevicePath, O_RDWR | O_CLOEXEC);
            if (fd < 0) return fd;
            int ifc = InterfaceId;
            int n = ioctl(fd, USBDEVFS_CLAIMINTERFACE, ref ifc);
            if (n != 0)
            {
                ioctl(fd, USBDEVFS_DISCONNECT, ref ifc);
                n = ioctl(fd, USBDEVFS_CLAIMINTERFACE, ref ifc);
            }
            if (n != 0)
            {
                close(fd);
                fd = -1;
                return n;
            }
            GetSerialNumber();
            return 0;
        }

        public override void Reset()
        {
            if (fd >= 0)
            {
                ioctl(fd, USBDEVFS_RESET, IntPtr.Zero);
            }
        }

        public override void Dispose()
        {
            if (fd >= 0)
            {
                int ifc = InterfaceId;
                ioctl(fd, USBDEVFS_RELEASEINTERFACE, ref ifc);
                close(fd);
                fd = -1;
            }
        }

        public override int GetSerialNumber()
        {
            if (iSerialNumber == 0) return -1;

            usbdevfs_ctrltransfer ctrl = new usbdevfs_ctrltransfer();
            byte[] descriptor = new byte[256];
            GCHandle handle = GCHandle.Alloc(descriptor, GCHandleType.Pinned);
            try
            {
                uint ctrlCode = (IntPtr.Size == 8) ? USBDEVFS_CONTROL_X86_64 : USBDEVFS_CONTROL_X86;

                ctrl.bRequestType = 0x80;
                ctrl.bRequest = 0x06;
                ctrl.wValue = (ushort)(0x03 << 8);
                ctrl.wIndex = 0;
                ctrl.wLength = (ushort)descriptor.Length;
                ctrl.data = handle.AddrOfPinnedObject();
                ctrl.timeout = 1000;

                int n = ioctl(fd, ctrlCode, ref ctrl);
                int languageCount = 0;
                ushort[] languages = new ushort[128];
                if (n > 2)
                {
                    languageCount = (n - 2) / 2;
                    for (int i = 0; i < languageCount; i++)
                    {
                        languages[i] = (ushort)(descriptor[2 + i * 2] | (descriptor[3 + i * 2] << 8));
                    }
                }
                else
                {
                    languages[0] = 0x0409;
                    languageCount = 1;
                }

                for (int i = 0; i < languageCount; i++)
                {
                    ctrl.bRequestType = 0x80;
                    ctrl.bRequest = 0x06;
                    ctrl.wValue = (ushort)((0x03 << 8) | iSerialNumber);
                    ctrl.wIndex = languages[i];
                    ctrl.wLength = (ushort)descriptor.Length;
                    ctrl.data = handle.AddrOfPinnedObject();
                    ctrl.timeout = 1000;

                    n = ioctl(fd, ctrlCode, ref ctrl);
                    if (n > 2)
                    {
                        SerialNumber = Encoding.Unicode.GetString(descriptor, 2, n - 2);
                        return 0;
                    }
                }
                return -1;
            }
            finally
            {
                handle.Free();
            }
        }

        public override byte[] Read(int length)
        {
            const uint MAX_USBFS_BULK_SIZE = 16384;
            const int MAX_RETRIES = 5;
            byte[] buffer = new byte[length];
            int count = 0;

            while (count < length)
            {
                int xfer = (length - count > (int)MAX_USBFS_BULK_SIZE) ? (int)MAX_USBFS_BULK_SIZE : (length - count);
                GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                try
                {
                    usbdevfs_bulktransfer bulk = new usbdevfs_bulktransfer
                    {
                        ep = ep_in,
                        len = (uint)xfer,
                        timeout = 5000,
                        data = new IntPtr(handle.AddrOfPinnedObject().ToInt64() + count)
                    };

                    uint bulkCode = (IntPtr.Size == 8) ? USBDEVFS_BULK_X86_64 : USBDEVFS_BULK_X86;
                    int n = -1;
                    int retry = 0;
                    do
                    {
                        n = ioctl(fd, bulkCode, ref bulk);
                        if (n < 0)
                        {
                            int err = Marshal.GetLastWin32Error();
                            if (err == EINTR || err == EAGAIN) continue;
                            if (err == ETIMEDOUT) break;
                            if (err == ENODEV || err == ESHUTDOWN || err == EPROTO)
                            {
                                throw new IOException($"USB read failed with fatal error: {err}");
                            }

                            if (++retry > MAX_RETRIES) break;
                            Thread.Sleep(500);
                        }
                    } while (n < 0);

                    if (n < 0) break;
                    count += n;
                    if (n < xfer) break;
                }
                finally
                {
                    handle.Free();
                }
            }
            if (count < length)
            {
                byte[] result = new byte[count];
                Array.Copy(buffer, result, count);
                return result;
            }
            return buffer;
        }

        public override long Write(byte[] data, int length)
        {
            const uint MAX_USBFS_BULK_SIZE = 16384;
            const int MAX_RETRIES = 5;
            int count = 0;

            if (length == 0)
            {
                GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
                try
                {
                    usbdevfs_bulktransfer bulk = new usbdevfs_bulktransfer
                    {
                        ep = ep_out,
                        len = 0,
                        timeout = 0,
                        data = handle.AddrOfPinnedObject()
                    };

                    uint bulkCode = (IntPtr.Size == 8) ? USBDEVFS_BULK_X86_64 : USBDEVFS_BULK_X86;
                    int n = ioctl(fd, bulkCode, ref bulk);
                    return n == 0 ? 0 : -1;
                }
                finally { handle.Free(); }
            }

            while (count < length)
            {
                int xfer = (length - count > (int)MAX_USBFS_BULK_SIZE) ? (int)MAX_USBFS_BULK_SIZE : (length - count);
                GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
                try
                {
                    usbdevfs_bulktransfer bulk = new usbdevfs_bulktransfer
                    {
                        ep = ep_out,
                        len = (uint)xfer,
                        timeout = 5000,
                        data = new IntPtr(handle.AddrOfPinnedObject().ToInt64() + count)
                    };

                    uint bulkCode = (IntPtr.Size == 8) ? USBDEVFS_BULK_X86_64 : USBDEVFS_BULK_X86;
                    int n = -1;
                    int retry = 0;
                    do
                    {
                        n = ioctl(fd, bulkCode, ref bulk);
                        if (n < 0)
                        {
                            int err = Marshal.GetLastWin32Error();
                            if (err == EINTR || err == EAGAIN) continue;
                            if (err == ETIMEDOUT) break;
                            if (err == ENODEV || err == ESHUTDOWN || err == EPROTO)
                            {
                                throw new IOException($"USB write failed with fatal error: {err}");
                            }

                            if (++retry > MAX_RETRIES) break;
                            Thread.Sleep(500);
                        }
                    } while (n < 0);

                    if (n < 0) return (count > 0) ? count : -1;
                    count += n;
                    if (n < (int)xfer) break;
                }
                finally
                {
                    handle.Free();
                }
            }
            return count;
        }
    }
}
