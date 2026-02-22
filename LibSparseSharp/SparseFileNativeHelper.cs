using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace LibSparseSharp;

public static partial class SparseFileNativeHelper
{
    [DllImport("Kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        uint nInBufferSize,
        IntPtr lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    private const uint FSCTL_SET_SPARSE = 0x000900C4;

    /// <summary>
    /// ? Windows ???????????
    /// </summary>
    public static void MarkAsSparse(FileStream fs)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            DeviceIoControl(fs.SafeFileHandle, FSCTL_SET_SPARSE, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero);
        }
    }
}
