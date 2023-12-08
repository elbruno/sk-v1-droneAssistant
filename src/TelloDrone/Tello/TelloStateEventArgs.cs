using static TelloSharp.Messages;
using static TelloSharp.Tello;

namespace TelloSharp
{
    public class TelloStateEventArgs : EventArgs
    {
        public FlyData State { get; set; }
        public short LastCmdId { get; set; }

        public TelloStateEventArgs(FlyData state, short lastCmdId)
        {
            State = state;
            LastCmdId = lastCmdId;
        }
    }
}