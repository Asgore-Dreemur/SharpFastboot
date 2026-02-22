namespace SharpFastboot.DataModel
{
    public class FastbootReceivedFromDeviceEventArgs
    {
        public FastbootState Type { get; set; }
        public string? NewInfo { get; set; }
        public string? NewText { get; set; }

        public FastbootReceivedFromDeviceEventArgs(FastbootState type, string? newInfo = null, string? newText = null)
        {
            Type = type;
            NewInfo = newInfo;
            NewText = newText;
        }
    }
}
