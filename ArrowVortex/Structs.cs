namespace RDPlaySongVortex.ArrowVortex
{
    public struct Onset
    {
        public double time;
        public float strength;
    }

    public struct TempoResult
    {
        public double bpm;
        public double fitness; // confidence
        public double offset;
    }
}
