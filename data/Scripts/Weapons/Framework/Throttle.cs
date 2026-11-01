namespace Survivors
{
    /// <summary>
    /// Lets something happen at most once per gap (a camera shake, a rustle, a ground mark): ask
    /// <see cref="TryPass"/> with the clock, or <see cref="IsReady"/> and <see cref="Mark"/> when the thing itself may
    /// still fail. The first ask always passes. Keep it in a non-readonly field: it is a mutable struct.
    /// </summary>
    public struct Throttle
    {
        private readonly float gap;
        private float last;

        /// <param name="gap">Seconds that must pass between two passes.</param>
        public Throttle(float gap)
        {
            this.gap = gap;
            last = float.NegativeInfinity;
        }

        /// <summary>Whether the gap has passed since the last pass.</summary>
        public bool IsReady(float now) => now - last >= gap;

        /// <summary>Records a pass at <paramref name="now"/>.</summary>
        public void Mark(float now) => last = now;

        /// <summary>Records a pass and returns true when the gap has passed; false otherwise.</summary>
        public bool TryPass(float now)
        {
            if (!IsReady(now))
            {
                return false;
            }
            last = now;
            return true;
        }
    }
}
