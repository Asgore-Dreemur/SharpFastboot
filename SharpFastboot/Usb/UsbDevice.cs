namespace SharpFastboot.Usb
{
    public abstract class UsbDevice : IDisposable
    {
        public required string DevicePath { get; set; }
        public string? SerialNumber { get; set; }
        public UsbDeviceType UsbDeviceType { get; set; }
        public abstract byte[] Read(int length);
        public abstract long Write(byte[] data, int length);
        public abstract int GetSerialNumber();
        public abstract int CreateHandle();
        public abstract void Reset();
        public abstract void Dispose();
    }

    public enum UsbDeviceType
    {
        WinLegacy = 0,
        WinUSB = 1,
        Linux = 2,
        LibUSB = 3,
        MacOS = 4
    }
}
