using System.Runtime.InteropServices;

namespace SharpFastboot
{
    public class DataHelper
    {
        public static T Bytes2Struct<T>(byte[] data, int length) where T : struct
        {
            T str;
            IntPtr ptr = Marshal.AllocHGlobal(length);
            Marshal.Copy(data, 0, ptr, length);
            str = Marshal.PtrToStructure<T>(ptr);
            Marshal.FreeHGlobal(ptr);
            return str;
        }

        public static byte[] Struct2Bytes<T>(T str) where T : struct
        {
            int length = Marshal.SizeOf(str);
            byte[] data = new byte[length];
            IntPtr ptr = Marshal.AllocHGlobal(length);
            Marshal.StructureToPtr(str, ptr, true);
            Marshal.Copy(ptr, data, 0, length);
            Marshal.FreeHGlobal(ptr);
            return data;
        }
    }
}
