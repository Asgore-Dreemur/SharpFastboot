using LibUsbDotNet.LibUsb;

namespace SharpFastboot.Usb.libusbdotnet
{
    public class LibUsbFinder
    {
        public static List<UsbDevice> FindDevice()
        {
            List<UsbDevice> devices = new List<UsbDevice>();
            using (var context = new UsbContext())
            {
                var deviceList = context.List();

                foreach (var device in deviceList)
                {
                    bool isFastboot = false;
                    byte interfaceId = 0;
                    foreach (var config in device.Configs)
                    {
                        foreach (var ifc in config.Interfaces)
                        {
                            if ((int)ifc.Class == 0xff && (int)ifc.SubClass == 0x42 && (int)ifc.Protocol == 0x03)
                            {
                                isFastboot = true;
                                interfaceId = (byte)ifc.Number;
                                break;
                            }
                        }
                        if (isFastboot) break;
                    }

                    if (isFastboot)
                    {
                        var libUsbDevice = device as LibUsbDotNet.LibUsb.UsbDevice;
                        byte busNumber = libUsbDevice?.BusNumber ?? 0;
                        byte address = libUsbDevice?.Address ?? 0;

                        devices.Add(new LibUsbDevice
                        {
                            Vid = (ushort)device.VendorId,
                            Pid = (ushort)device.ProductId,
                            BusNumber = busNumber,
                            DeviceAddress = address,
                            InterfaceId = interfaceId,
                            DevicePath = $"Bus {busNumber} Device {address}: {device.VendorId:X4}:{device.ProductId:X4}",
                            UsbDeviceType = UsbDeviceType.LibUSB
                        });
                    }
                }
            }
            return devices;
        }
    }
}
