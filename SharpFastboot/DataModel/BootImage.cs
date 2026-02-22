using System.Runtime.InteropServices;
using System.Text;

namespace SharpFastboot.DataModel
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BootImageHeader
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] Magic; // "ANDROID!"

        public uint KernelSize;
        public uint KernelAddr;

        public uint RamdiskSize;
        public uint RamdiskAddr;

        public uint SecondSize;
        public uint SecondAddr;

        public uint TagsAddr;
        public uint PageSize;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public uint[] Unused;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] Name;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
        public byte[] Cmdline;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public uint[] Id;

        public uint Kcrc32;
        public uint Rcrc32;

        public static BootImageHeader Create()
        {
            return new BootImageHeader
            {
                Magic = Encoding.ASCII.GetBytes("ANDROID!"),
                Unused = new uint[2],
                Name = new byte[16],
                Cmdline = new byte[512],
                Id = new uint[8]
            };
        }
    }
}
