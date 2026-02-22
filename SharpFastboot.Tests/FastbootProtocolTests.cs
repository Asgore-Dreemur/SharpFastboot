using SharpFastboot.DataModel;
using SharpFastboot.Usb;
using System.Text;

namespace SharpFastboot.Tests
{
    public class FastbootProtocolTests
    {
        private class MockUsbDevice : UsbDevice
        {
            private Queue<byte[]> _responses = new Queue<byte[]>();

            public MockUsbDevice()
            {
                DevicePath = "mock";
            }

            public void EnqueueResponse(string response)
            {
                _responses.Enqueue(Encoding.UTF8.GetBytes(response));
            }

            public override byte[] Read(int length)
            {
                if (_responses.Count == 0) return Array.Empty<byte>();
                return _responses.Dequeue();
            }

            public override long Write(byte[] data, int length) => length;
            public override int GetSerialNumber() => 0;
            public override int CreateHandle() => 0;
            public override void Reset() { }
            public override void Dispose() { }
        }

        [Fact]
        public void HandleResponse_Success_ReturnsOkay()
        {
            var mockUsb = new MockUsbDevice { DevicePath = "mock" };
            mockUsb.EnqueueResponse("OKAYDONE");
            var util = new FastbootUtil(mockUsb);

            var response = util.HandleResponse();

            Assert.Equal(FastbootState.Success, response.Result);
            Assert.Equal("DONE", response.Response);
        }

        [Fact]
        public void HandleResponse_Fail_ReturnsFailure()
        {
            var mockUsb = new MockUsbDevice { DevicePath = "mock" };
            mockUsb.EnqueueResponse("FAILERROR_MESSAGE");
            var util = new FastbootUtil(mockUsb);

            var response = util.HandleResponse();

            Assert.Equal(FastbootState.Fail, response.Result);
            Assert.Equal("ERROR_MESSAGE", response.Response);
        }

        [Fact]
        public void HandleResponse_InfoThenOkay_CollectsInfo()
        {
            var mockUsb = new MockUsbDevice { DevicePath = "mock" };
            mockUsb.EnqueueResponse("INFOPartial Info 1");
            mockUsb.EnqueueResponse("INFOPartial Info 2");
            mockUsb.EnqueueResponse("OKAY");
            var util = new FastbootUtil(mockUsb);

            var response = util.HandleResponse();

            Assert.Equal(FastbootState.Success, response.Result);
            Assert.Equal(2, response.Info.Count);
            Assert.Equal("Partial Info 1", response.Info[0]);
            Assert.Equal("Partial Info 2", response.Info[1]);
        }

        [Fact]
        public void HandleResponse_Data_ReturnsDataAndSize()
        {
            var mockUsb = new MockUsbDevice { DevicePath = "mock" };
            mockUsb.EnqueueResponse("DATA00000400"); // 1024 in hex
            var util = new FastbootUtil(mockUsb);

            var response = util.HandleResponse();

            Assert.Equal(FastbootState.Data, response.Result);
            Assert.Equal(1024, response.DataSize);
        }
    }
}
