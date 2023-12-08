namespace TelloSharp
{
    public class ControllerState
    {
        public float Rx { get; set; }
        public float Ry { get; set; }
        public float Lx { get; set; }
        public float Ly { get; set; }
        public int Speed { get; set; }
        public double DeadBand { get; set; } = 0.15D;

        public void SetAxis(float lx, float ly, float rx, float ry)
        {
            Rx = rx;
            Ry = ry;
            Lx = lx;
            Ly = ly;
        }
        public void SetSpeedMode(int mode)
        {
            Speed = mode;
        }
    }
}