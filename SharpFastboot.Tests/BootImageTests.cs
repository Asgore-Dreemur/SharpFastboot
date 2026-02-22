using SharpFastboot.DataModel;
using System.Runtime.InteropServices;
using System.Text;

namespace SharpFastboot.Tests
{
    public class BootImageTests
    {
        [Fact]
        public void BootImageHeader_Create_HasCorrectMagic()
        {
            var header = BootImageHeader.Create();
            string magic = Encoding.ASCII.GetString(header.Magic);
            Assert.Equal("ANDROID!", magic);
        }

        [Fact]
        public void BootImageHeader_Size_IsExpected()
        {
            int size = Marshal.SizeOf<BootImageHeader>();
            // 8(magic) + 8(kernel) + 8(ramdisk) + 8(second) + 4(tags) + 4(page) + 8(unused) + 16(name) + 512(cmdline) + 32(id) + 4(kcrc) + 4(rcrc)
            // = 8 + 8 + 8 + 8 + 4 + 4 + 8 + 16 + 512 + 32 + 4 + 4 = 616
            Assert.Equal(616, size);
        }
    }
}
