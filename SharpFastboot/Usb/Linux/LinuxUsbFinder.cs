using System.Runtime.InteropServices;
using static SharpFastboot.Usb.Linux.LinuxUsbAPI;

namespace SharpFastboot.Usb.Linux
{
    public class LinuxUsbFinder
    {
        public static List<UsbDevice> FindDevice()
        {
            List<UsbDevice> devices = new List<UsbDevice>();
            const string base_path = "/dev/bus/usb";
            if (!Directory.Exists(base_path)) return devices;

            foreach (var bus_dir in Directory.GetDirectories(base_path))
            {
                foreach (var dev_path in Directory.GetFiles(bus_dir))
                {
                    int fd = open(dev_path, O_RDWR | O_CLOEXEC);
                    if (fd < 0)
                    {
                        fd = open(dev_path, 0 | O_CLOEXEC);
                        if (fd < 0) continue;
                    }

                    byte[] desc = new byte[1024];
                    IntPtr ptr = Marshal.AllocHGlobal(desc.Length);
                    try
                    {
                        int n = read(fd, ptr, (uint)desc.Length);
                        if (n < 18) { close(fd); continue; }
                        Marshal.Copy(ptr, desc, 0, n);

                        if (n < 18) { close(fd); continue; }
                        ushort idVendor = (ushort)(desc[8] | (desc[9] << 8));
                        ushort idProduct = (ushort)(desc[10] | (desc[11] << 8));
                        byte iSerialNumber = desc[14];

                        int pos = desc[0];
                        while (pos < n - 1)
                        {
                            int len = desc[pos];
                            if (len < 2 || pos + len > n) break;
                            byte type = desc[pos + 1];

                            if (type == 0x04) // Interface Descriptor
                            {
                                if (len < 9) { pos += len; continue; }
                                byte ifcClass = desc[pos + 5];
                                byte ifcSubClass = desc[pos + 6];
                                byte ifcProtocol = desc[pos + 7];
                                byte ifcId = desc[pos + 2];

                                if (ifcClass == 0xff && ifcSubClass == 0x42 && ifcProtocol == 0x03)
                                {
                                    byte numEpts = desc[pos + 4];
                                    byte epIn = 0, epOut = 0;
                                    int ept_pos = pos + len;
                                    int checked_epts = 0;

                                    while (ept_pos < n - 1 && checked_epts < numEpts)
                                    {
                                        int ept_len = desc[ept_pos];
                                        if (ept_len < 2 || ept_pos + ept_len > n) break;
                                        byte ept_type = desc[ept_pos + 1];

                                        if (ept_type == 0x05) // Endpoint Descriptor
                                        {
                                            if (ept_len >= 7)
                                            {
                                                byte addr = desc[ept_pos + 2];
                                                byte attr = desc[ept_pos + 3];
                                                if ((attr & 0x03) == 0x02) // Bulk
                                                {
                                                    if ((addr & 0x80) != 0) epIn = addr;
                                                    else epOut = addr;
                                                }
                                            }
                                            checked_epts++;
                                        }
                                        ept_pos += ept_len;
                                    }

                                    if (epIn != 0 && epOut != 0)
                                    {
                                        devices.Add(new LinuxUsbDevice
                                        {
                                            DevicePath = dev_path,
                                            ep_in = epIn,
                                            ep_out = epOut,
                                            InterfaceId = ifcId,
                                            iSerialNumber = iSerialNumber,
                                            UsbDeviceType = UsbDeviceType.Linux,
                                            SerialNumber = iSerialNumber == 0 ? null : "UNKNOWN"
                                        });
                                        close(fd);
                                        break;
                                    }
                                }
                            }
                            pos += len;
                        }
                    }
                    catch { }
                    finally
                    {
                        Marshal.FreeHGlobal(ptr);
                        if (fd >= 0) close(fd);
                        fd = -1;
                    }
                }
            }
            return devices;
        }
    }
}
