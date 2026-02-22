using LibUsbDotNet.LibUsb;
using LibUsbDotNet.Main;

namespace SharpFastboot.Usb.libusbdotnet
{
    public class LibUsbDevice : UsbDevice
    {
        private UsbContext? context;
        private IUsbDevice? usbDevice;
        private UsbEndpointReader? reader;
        private UsbEndpointWriter? writer;
        public ushort Vid { get; set; }
        public ushort Pid { get; set; }
        public byte BusNumber { get; set; }
        public byte DeviceAddress { get; set; }
        public byte InterfaceId { get; set; } = 0;

        public override int CreateHandle()
        {
            context = new UsbContext();
            var deviceList = context.List();
            var device = deviceList.OfType<LibUsbDotNet.LibUsb.UsbDevice>()
                                   .FirstOrDefault(d => d.BusNumber == BusNumber && d.Address == DeviceAddress);
            if (device == null)
            {
                context.Dispose();
                context = null;
                return -1;
            }

            usbDevice = device;
            usbDevice.Open();
            try
            {
                usbDevice.SetConfiguration(1);
            }
            catch { }

            try
            {
                usbDevice.ClaimInterface(InterfaceId);
            }
            catch
            {
                try
                {
                    (usbDevice as LibUsbDotNet.LibUsb.UsbDevice)?.DetachKernelDriver(InterfaceId);
                    usbDevice.ClaimInterface(InterfaceId);
                }
                catch { }
            }

            foreach (var config in usbDevice.Configs)
            {
                foreach (var ifc in config.Interfaces)
                {
                    if (ifc.Number != InterfaceId) continue;
                    foreach (var endpoint in ifc.Endpoints)
                    {
                        if ((endpoint.EndpointAddress & 0x80) != 0)
                        {
                            reader = usbDevice.OpenEndpointReader((ReadEndpointID)endpoint.EndpointAddress);
                            reader?.ReadFlush();
                        }
                        else
                        {
                            writer = usbDevice.OpenEndpointWriter((WriteEndpointID)endpoint.EndpointAddress);
                        }
                    }
                }
            }
            GetSerialNumber();
            return 0;
        }

        public override void Dispose()
        {
            if (usbDevice != null)
            {
                usbDevice.Close();
                usbDevice = null;
            }
            if (context != null)
            {
                context.Dispose();
                context = null;
            }
        }

        public override int GetSerialNumber()
        {
            if (usbDevice != null)
            {
                SerialNumber = usbDevice.Info.SerialNumber;
            }
            return 0;
        }

        public override void Reset()
        {
            if (usbDevice != null)
            {
                try
                {
                    usbDevice.ResetDevice();
                }
                catch { }
            }
        }

        public override byte[] Read(int length)
        {
            if (reader == null) return Array.Empty<byte>();

            const int maxLenToRead = 1048576;
            int lenRemaining = length;
            int count = 0;
            byte[] buffer = new byte[length];

            while (lenRemaining > 0)
            {
                int lenToRead = Math.Min(lenRemaining, maxLenToRead);
                int read_len;
                
                reader.Read(buffer, count, lenToRead, 5000, out read_len);

                if (read_len <= 0) break;
                
                count += read_len;
                lenRemaining -= read_len;

                if (read_len < lenToRead) break;
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
            if (writer == null) return -1;
            
            const int maxLenToSend = 1048576;
            int lenRemaining = length;
            int count = 0;

            if (length == 0)
            {
                int transferred;
                writer.Write(data, 0, 0, 5000, out transferred);
                return transferred;
            }

            while (lenRemaining > 0)
            {
                int lenToSend = Math.Min(lenRemaining, maxLenToSend);
                int transferred;
                writer.Write(data, count, lenToSend, 5000, out transferred);

                if (transferred <= 0) break;

                count += transferred;
                lenRemaining -= transferred;
                
                if (transferred < lenToSend) break;
            }

            return count > 0 ? count : (length == 0 ? 0 : -1);
        }
    }
}
