using System.Runtime.InteropServices;
using static SharpFastboot.Usb.macOS.MacOSUsbAPI;

namespace SharpFastboot.Usb.macOS
{
    public class MacOSUsbDevice : UsbDevice
    {
        private IntPtr devicePtr;
        private IntPtr interfacePtr;
        public byte bulkIn { get; set; }
        public byte bulkOut { get; set; }

        public override int CreateHandle()
        {
            IntPtr service = IORegistryEntryFromPath(IntPtr.Zero, DevicePath + "\0");
            if (service == IntPtr.Zero) return -1;

            try
            {
                IntPtr pluginInterface = IntPtr.Zero;
                int score = 0;
                int kr = IOCreatePlugInInterfaceForService(service, kIOUSBDeviceUserClientTypeID, kIOCFPlugInInterfaceID, out pluginInterface, out score);
                if (kr != 0 || pluginInterface == IntPtr.Zero) return kr;

                try
                {
                    var queryInterface = GetDelegate<QueryInterfaceDelegate>(pluginInterface, Offset_Plugin_QueryInterface);
                    if (queryInterface(pluginInterface, kIOUSBDeviceInterfaceID, out devicePtr) != 0 || devicePtr == IntPtr.Zero) return -1;

                    try
                    {
                        var openDev = GetDelegate<USBDeviceOpenDelegate>(devicePtr, Offset_USBDeviceOpen);
                        var createIter = GetDelegate<USBDeviceCreateInterfaceIteratorDelegate>(devicePtr, Offset_USBDeviceCreateInterfaceIterator);
                        var setConf = GetDelegate<USBSetConfigurationDelegate>(devicePtr, Offset_USBSetConfiguration);
                        var getConf = GetDelegate<USBGetConfigurationDelegate>(devicePtr, Offset_USBGetConfiguration);

                        openDev(devicePtr);

                        byte currentConf;
                        if (getConf(devicePtr, out currentConf) == 0 && currentConf != 1)
                        {
                            setConf(devicePtr, 1);
                        }

                        IOUSBFindInterfaceRequest request = new IOUSBFindInterfaceRequest
                        {
                            bInterfaceClass = 0xff,
                            bInterfaceSubClass = 0x42,
                            bInterfaceProtocol = 0x03,
                            bAlternateSetting = kIOUSBFindInterfaceDontCare
                        };

                        IntPtr interfaceIter;
                        if (createIter(devicePtr, ref request, out interfaceIter) == 0 && interfaceIter != IntPtr.Zero)
                        {
                            IntPtr ifcService = IOIteratorNext(interfaceIter);
                            if (ifcService != IntPtr.Zero)
                            {
                                IntPtr ifcPlugin = IntPtr.Zero;
                                if (IOCreatePlugInInterfaceForService(ifcService, kIOUSBInterfaceUserClientTypeID, kIOCFPlugInInterfaceID, out ifcPlugin, out score) == 0 && ifcPlugin != IntPtr.Zero)
                                {
                                    try
                                    {
                                        var ifcQuery = GetDelegate<QueryInterfaceDelegate>(ifcPlugin, Offset_Plugin_QueryInterface);
                                        if (ifcQuery(ifcPlugin, kIOUSBInterfaceInterfaceID190, out interfacePtr) == 0 && interfacePtr != IntPtr.Zero)
                                        {
                                            var ifcOpen = GetDelegate<USBInterfaceOpenDelegate>(interfacePtr, Offset_USBInterfaceOpen);
                                            kr = ifcOpen(interfacePtr);
                                            if (kr == 0)
                                            {
                                                var ifcClear = GetDelegate<ClearPipeStallBothEndsDelegate>(interfacePtr, Offset_ClearPipeStallBothEnds);
                                                if (bulkIn != 0) ifcClear(interfacePtr, bulkIn);
                                                if (bulkOut != 0) ifcClear(interfacePtr, bulkOut);
                                            }
                                            else
                                            {
                                                GetDelegate<ReleaseDelegate>(interfacePtr, Offset_IUnknown_Release)(interfacePtr);
                                                interfacePtr = IntPtr.Zero;
                                            }
                                        }
                                    }
                                    finally { GetDelegate<ReleaseDelegate>(ifcPlugin, Offset_Plugin_Release)(ifcPlugin); }
                                }
                                IOObjectRelease(ifcService);
                            }
                            IOObjectRelease(interfaceIter);
                        }
                    }
                    catch { }
                }
                finally { GetDelegate<ReleaseDelegate>(pluginInterface, Offset_Plugin_Release)(pluginInterface); }
            }
            finally { IOObjectRelease(service); }

            return interfacePtr != IntPtr.Zero ? 0 : -1;
        }

        public override void Reset()
        {
            if (devicePtr != IntPtr.Zero)
            {
                GetDelegate<USBDeviceResetDelegate>(devicePtr, Offset_USBDeviceReset)(devicePtr);
            }
        }

        public override void Dispose()
        {
            if (interfacePtr != IntPtr.Zero)
            {
                GetDelegate<USBInterfaceCloseDelegate>(interfacePtr, Offset_USBInterfaceClose)(interfacePtr);
                GetDelegate<ReleaseDelegate>(interfacePtr, Offset_IUnknown_Release)(interfacePtr);
                interfacePtr = IntPtr.Zero;
            }
            if (devicePtr != IntPtr.Zero)
            {
                GetDelegate<USBDeviceCloseDelegate>(devicePtr, Offset_USBDeviceClose)(devicePtr);
                GetDelegate<ReleaseDelegate>(devicePtr, Offset_IUnknown_Release)(devicePtr);
                devicePtr = IntPtr.Zero;
            }
        }

        public override int GetSerialNumber()
        {
            if (devicePtr == IntPtr.Zero) return -1;

            byte serialIndex;
            var getIdx = GetDelegate<USBGetSerialNumberStringIndexDelegate>(devicePtr, Offset_USBGetSerialNumberStringIndex);
            if (getIdx(devicePtr, out serialIndex) != 0 || serialIndex == 0) return -1;

            IOUSBDevRequest req = new IOUSBDevRequest();
            byte[] buf = new byte[256];
            GCHandle handle = GCHandle.Alloc(buf, GCHandleType.Pinned);
            try
            {
                req.bmRequestType = 0x80;
                req.bRequest = 0x06;
                req.wValue = (ushort)((0x03 << 8) | serialIndex);
                req.wIndex = 0x0409;
                req.wLength = (ushort)buf.Length;
                req.pData = handle.AddrOfPinnedObject();

                var devReq = GetDelegate<DeviceRequestDelegate>(devicePtr, Offset_DeviceRequest);
                if (devReq(devicePtr, ref req) == 0 && req.wLenDone > 2)
                {
                    SerialNumber = System.Text.Encoding.Unicode.GetString(buf, 2, (int)req.wLenDone - 2);
                    return 0;
                }
                return -1;
            }
            finally { handle.Free(); }
        }

        public override byte[] Read(int length)
        {
            if (interfacePtr == IntPtr.Zero || bulkIn == 0) return Array.Empty<byte>();

            const int maxLenToRead = 1048576;
            int lenRemaining = length;
            int count = 0;
            byte[] buffer = new byte[length];
            var readPipe = GetDelegate<ReadPipeTODelegate>(interfacePtr, Offset_ReadPipeTO);

            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                while (lenRemaining > 0)
                {
                    int lenToRead = Math.Min(lenRemaining, maxLenToRead);
                    IntPtr ptr = new IntPtr(handle.AddrOfPinnedObject().ToInt64() + count);
                    uint size = (uint)lenToRead;

                    int kr = readPipe(interfacePtr, bulkIn, ptr, ref size, 5000, 5000);
                    if (kr != 0)
                    {
                        if (kr == kIOReturnNoDevice || kr == kIOReturnNotResponding || kr == kIOReturnAborted)
                        {
                            throw new IOException($"USB read failed with fatal error: 0x{kr:X}");
                        }
                        break;
                    }

                    count += (int)size;
                    lenRemaining -= (int)size;

                    if (size < lenToRead) break;
                }

                if (count < length)
                {
                    byte[] result = new byte[count];
                    Array.Copy(buffer, result, count);
                    return result;
                }
                return buffer;
            }
            finally
            {
                handle.Free();
            }
        }

        public override long Write(byte[] data, int length)
        {
            if (interfacePtr == IntPtr.Zero || bulkOut == 0) return -1;

            const int maxLenToSend = 1048576;
            int lenRemaining = length;
            int count = 0;
            var writePipe = GetDelegate<WritePipeTODelegate>(interfacePtr, Offset_WritePipeTO);

            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                while (lenRemaining > 0)
                {
                    int lenToSend = Math.Min(lenRemaining, maxLenToSend);
                    IntPtr ptr = new IntPtr(handle.AddrOfPinnedObject().ToInt64() + count);

                    int kr = writePipe(interfacePtr, bulkOut, ptr, (uint)lenToSend, 5000, 5000);
                    if (kr != 0)
                    {
                        if (kr == kIOReturnNoDevice || kr == kIOReturnNotResponding || kr == kIOReturnAborted)
                        {
                            throw new IOException($"USB write failed with fatal error: 0x{kr:X}");
                        }
                        break;
                    }

                    lenRemaining -= lenToSend;
                    count += lenToSend;
                }
                if (length == 0)
                {
                    writePipe(interfacePtr, bulkOut, IntPtr.Zero, 0, 5000, 5000);
                }
                return count > 0 ? count : (length == 0 ? 0 : -1);
            }
            finally
            {
                handle.Free();
            }
        }
    }
}
