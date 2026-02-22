using System.Runtime.InteropServices;
using System.Text;

namespace SharpFastboot.Usb.macOS
{
    public static class MacOSUsbAPI
    {
        public const string IOKit = "/System/Library/Frameworks/IOKit.framework/IOKit";
        public const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

        [DllImport(IOKit)]
        public static extern IntPtr IOServiceMatching(string name);

        [DllImport(IOKit)]
        public static extern int IOServiceGetMatchingServices(IntPtr masterPort, IntPtr matching, out IntPtr iterator);

        [DllImport(IOKit)]
        public static extern IntPtr IOIteratorNext(IntPtr iterator);

        [DllImport(IOKit)]
        public static extern void IOObjectRelease(IntPtr obj);

        [DllImport(IOKit)]
        public static extern IntPtr IORegistryEntryFromPath(IntPtr masterPort, string path);

        [DllImport(IOKit)]
        public static extern int IORegistryEntryGetPath(IntPtr entry, string plane, StringBuilder path);

        public const string kIODeviceTreePlane = "IODeviceTree";
        public const string kIOServicePlane = "IOService";

        [DllImport(IOKit)]
        public static extern int IOCreatePlugInInterfaceForService(IntPtr service, Guid pluginType, Guid interfaceType, out IntPtr pluginInterface, out int score);

        [DllImport(IOKit)]
        public static extern int IOIteratorReset(IntPtr iterator);

        [DllImport(IOKit)]
        public static extern int IOIteratorIsValid(IntPtr iterator);

        [DllImport(CoreFoundation)]
        public static extern void CFRelease(IntPtr obj);

        public static readonly Guid kIOUSBDeviceUserClientTypeID = new Guid("9d7d2100-ba54-11d4-8113-0005020c020c");
        public static readonly Guid kIOUSBDeviceInterfaceID = new Guid("5c3a030d-27d1-11d4-9d10-0005020c020c");
        public static readonly Guid kIOUSBDeviceInterfaceID197 = new Guid("9db09066-5e58-11d5-ba38-0005020c020c");
        public static readonly Guid kIOCFPlugInInterfaceID = new Guid("c244e858-109c-11d4-91d4-0050e4c6426f");
        public static readonly Guid kIOUSBInterfaceUserClientTypeID = new Guid("2d9786c6-9ef3-11d4-ad51-000a27052861");

        public static readonly Guid kIOUSBInterfaceInterfaceID = new Guid("736a5375-da3a-11d4-9988-0005020c020c");
        public static readonly Guid kIOUSBInterfaceInterfaceID190 = new Guid("d44fd2f8-002d-11d6-8e5e-000a27052861");
        public static readonly Guid kIOUSBInterfaceInterfaceID197 = new Guid("1e50f3c0-022e-11d6-b49d-000a27052861");

        public const int S_OK = 0;
        public const int KERN_SUCCESS = 0;
        public const int kIOReturnNoDevice = unchecked((int)0xE00002C0);
        public const int kIOReturnAborted = unchecked((int)0xE00002EB);
        public const int kIOReturnTimeout = unchecked((int)0xE00002D6);
        public const int kIOReturnNotResponding = unchecked((int)0xE00002ED);

        [StructLayout(LayoutKind.Sequential)]
        public struct IOUSBFindInterfaceRequest
        {
            public ushort bInterfaceClass;
            public ushort bInterfaceSubClass;
            public ushort bInterfaceProtocol;
            public ushort bAlternateSetting;
        }

        public const ushort kIOUSBFindInterfaceDontCare = 0xFFFF;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int USBDeviceOpenDelegate(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int USBDeviceCloseDelegate(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int USBDeviceResetDelegate(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int USBGetDeviceVendorDelegate(IntPtr self, out ushort devVendor);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int USBGetDeviceProductDelegate(IntPtr self, out ushort devProduct);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int USBGetSerialNumberStringIndexDelegate(IntPtr self, out byte serialIndex);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int USBDeviceCreateInterfaceIteratorDelegate(IntPtr self, ref IOUSBFindInterfaceRequest request, out IntPtr iterator);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int USBGetConfigurationDelegate(IntPtr self, out byte configNumber);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int USBSetConfigurationDelegate(IntPtr self, byte configNumber);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int QueryInterfaceDelegate(IntPtr self, Guid iid, out IntPtr ppv);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate uint ReleaseDelegate(IntPtr self);

        [StructLayout(LayoutKind.Sequential)]
        public struct IOUSBDevRequest
        {
            public byte bmRequestType;
            public byte bRequest;
            public ushort wValue;
            public ushort wIndex;
            public ushort wLength;
            public IntPtr pData;
            public uint wLenDone;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int DeviceRequestDelegate(IntPtr self, ref IOUSBDevRequest request);

        // VTable offsets for IOCFPlugInInterface (Plugin factory)
        public const int Offset_Plugin_QueryInterface = 1;
        public const int Offset_Plugin_Release = 3;

        // VTable offsets for common IUnknown (Interface, Device)
        public const int Offset_IUnknown_QueryInterface = 1;
        public const int Offset_IUnknown_Release = 3;

        // VTable offsets for IOUSBDeviceInterface (relative to IUnknown start)
        public const int Offset_DeviceRequest = 7;
        public const int Offset_USBDeviceOpen = 14;
        public const int Offset_USBDeviceClose = 15;
        public const int Offset_USBGetDeviceVendor = 16;
        public const int Offset_USBGetDeviceProduct = 17;
        public const int Offset_USBGetSerialNumberStringIndex = 21;
        public const int Offset_USBGetConfiguration = 24;
        public const int Offset_USBSetConfiguration = 25;
        public const int Offset_USBDeviceCreateInterfaceIterator = 26;
        public const int Offset_USBDeviceReset = 27;

        // Delegates for IOUSBInterfaceInterface
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int USBInterfaceOpenDelegate(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int USBInterfaceCloseDelegate(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ReadPipeDelegate(IntPtr self, byte pipeRef, IntPtr data, ref uint size);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int WritePipeDelegate(IntPtr self, byte pipeRef, IntPtr data, uint size);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int GetNumEndpointsDelegate(IntPtr self, out byte numEndpoints);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int GetPipePropertiesDelegate(IntPtr self, byte pipeRef, out byte direction, out byte number, out byte transferType, out ushort maxPacketSize, out byte interval);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ClearPipeStallBothEndsDelegate(IntPtr self, byte pipeRef);

        // VTable offsets for IOUSBInterfaceInterface
        // These are indices in the vtable (0-based)
        public const int Offset_USBInterfaceOpen = 8;
        public const int Offset_USBInterfaceClose = 9;
        public const int Offset_GetNumEndpoints = 17;
        public const int Offset_GetPipeProperties = 18;
        public const int Offset_ClearPipeStallBothEnds = 25; // This may vary, but let's try 190+ offset
        public const int Offset_ReadPipe = 26;
        public const int Offset_WritePipe = 27;
        public const int Offset_ReadPipeTO = 28;
        public const int Offset_WritePipeTO = 29;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ReadPipeTODelegate(IntPtr self, byte pipeRef, IntPtr data, ref uint size, uint noDataTimeout, uint completionTimeout);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int WritePipeTODelegate(IntPtr self, byte pipeRef, IntPtr data, uint size, uint noDataTimeout, uint completionTimeout);

        public static T GetDelegate<T>(IntPtr self, int offset) where T : class
        {
            IntPtr vtable = Marshal.ReadIntPtr(self);
            IntPtr methodPtr = Marshal.ReadIntPtr(vtable, offset * IntPtr.Size);
            return (T)(object)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(T));
        }
    }
}