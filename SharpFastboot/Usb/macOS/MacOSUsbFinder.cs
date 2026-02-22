using System.Text;
using static SharpFastboot.Usb.macOS.MacOSUsbAPI;

namespace SharpFastboot.Usb.macOS
{
    public class MacOSUsbFinder
    {
        public static List<UsbDevice> FindDevice()
        {
            List<UsbDevice> devices = new List<UsbDevice>();
            IntPtr matchingDict = IOServiceMatching("IOUSBDevice");
            if (matchingDict == IntPtr.Zero) return devices;

            int kr = IOServiceGetMatchingServices(IntPtr.Zero, matchingDict, out IntPtr iterator);
            if (kr != 0) return devices;

            IntPtr service;
            while ((service = IOIteratorNext(iterator)) != IntPtr.Zero)
            {
                StringBuilder sbPath = new StringBuilder(1024);
                IORegistryEntryGetPath(service, kIOServicePlane, sbPath);
                string devicePath = sbPath.ToString();

                IntPtr pluginInterface = IntPtr.Zero;
                int score = 0;
                kr = IOCreatePlugInInterfaceForService(service, kIOUSBDeviceUserClientTypeID, kIOCFPlugInInterfaceID, out pluginInterface, out score);
                if (kr != 0 || pluginInterface == IntPtr.Zero)
                {
                    IOObjectRelease(service);
                    continue;
                }

                try
                {
                    var queryInterface = GetDelegate<QueryInterfaceDelegate>(pluginInterface, Offset_Plugin_QueryInterface);
                    IntPtr deviceInterface = IntPtr.Zero;
                    if (queryInterface(pluginInterface, kIOUSBDeviceInterfaceID, out deviceInterface) != 0 || deviceInterface == IntPtr.Zero)
                        continue;

                    try
                    {
                        var getVendor = GetDelegate<USBGetDeviceVendorDelegate>(deviceInterface, Offset_USBGetDeviceVendor);
                        var getProduct = GetDelegate<USBGetDeviceProductDelegate>(deviceInterface, Offset_USBGetDeviceProduct);
                        var createIter = GetDelegate<USBDeviceCreateInterfaceIteratorDelegate>(deviceInterface, Offset_USBDeviceCreateInterfaceIterator);
                        var openDev = GetDelegate<USBDeviceOpenDelegate>(deviceInterface, Offset_USBDeviceOpen);
                        var closeDev = GetDelegate<USBDeviceCloseDelegate>(deviceInterface, Offset_USBDeviceClose);

                        ushort vid, pid;
                        getVendor(deviceInterface, out vid);
                        getProduct(deviceInterface, out pid);

                        IOUSBFindInterfaceRequest request = new IOUSBFindInterfaceRequest
                        {
                            bInterfaceClass = 0xff,
                            bInterfaceSubClass = 0x42,
                            bInterfaceProtocol = 0x03,
                            bAlternateSetting = kIOUSBFindInterfaceDontCare
                        };

                        IntPtr interfaceIter;
                        if (createIter(deviceInterface, ref request, out interfaceIter) == 0 && interfaceIter != IntPtr.Zero)
                        {
                            IntPtr ifcService;
                            while ((ifcService = IOIteratorNext(interfaceIter)) != IntPtr.Zero)
                            {
                                // Found potential fastboot interface
                                // Create plugin for interface to get more details
                                IntPtr ifcPlugin = IntPtr.Zero;
                                if (IOCreatePlugInInterfaceForService(ifcService, kIOUSBInterfaceUserClientTypeID, kIOCFPlugInInterfaceID, out ifcPlugin, out score) == 0 && ifcPlugin != IntPtr.Zero)
                                {
                                    try
                                    {
                                        var ifcQuery = GetDelegate<QueryInterfaceDelegate>(ifcPlugin, Offset_Plugin_QueryInterface);
                                        IntPtr ifcIntf = IntPtr.Zero;
                                        if (ifcQuery(ifcPlugin, kIOUSBInterfaceInterfaceID190, out ifcIntf) == 0 && ifcIntf != IntPtr.Zero)
                                        {
                                            try
                                            {
                                                var getNumEpts = GetDelegate<GetNumEndpointsDelegate>(ifcIntf, Offset_GetNumEndpoints);
                                                var getPipeProps = GetDelegate<GetPipePropertiesDelegate>(ifcIntf, Offset_GetPipeProperties);

                                                byte numEpts;
                                                getNumEpts(ifcIntf, out numEpts);
                                                byte bulkIn = 0, bulkOut = 0;

                                                for (byte i = 1; i <= numEpts; i++)
                                                {
                                                    byte direction, number, transferType, interval;
                                                    ushort maxPacketSize;
                                                    if (getPipeProps(ifcIntf, i, out direction, out number, out transferType, out maxPacketSize, out interval) == 0)
                                                    {
                                                        if (transferType == 0x02) // Bulk
                                                        {
                                                            if (direction == 1) bulkIn = i;
                                                            else bulkOut = i;
                                                        }
                                                    }
                                                }

                                                if (bulkIn != 0 && bulkOut != 0)
                                                {
                                                    devices.Add(new MacOSUsbDevice
                                                    {
                                                        DevicePath = devicePath,
                                                        bulkIn = bulkIn,
                                                        bulkOut = bulkOut,
                                                        UsbDeviceType = UsbDeviceType.MacOS
                                                    });
                                                }
                                            }
                                            finally { GetDelegate<ReleaseDelegate>(ifcIntf, Offset_IUnknown_Release)(ifcIntf); }
                                        }
                                    }
                                    finally { GetDelegate<ReleaseDelegate>(ifcPlugin, Offset_Plugin_Release)(ifcPlugin); }
                                }
                                IOObjectRelease(ifcService);
                            }
                            IOObjectRelease(interfaceIter);
                        }
                    }
                    finally
                    {
                        var devRelease = GetDelegate<ReleaseDelegate>(deviceInterface, Offset_IUnknown_Release);
                        devRelease(deviceInterface);
                    }
                }
                finally
                {
                    var pluginRelease = GetDelegate<ReleaseDelegate>(pluginInterface, Offset_Plugin_Release);
                    pluginRelease(pluginInterface);
                    IOObjectRelease(service);
                }
            }

            IOObjectRelease(iterator);
            return devices;
        }
    }
}
