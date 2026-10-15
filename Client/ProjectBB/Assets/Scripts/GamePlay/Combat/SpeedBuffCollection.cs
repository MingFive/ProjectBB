using System;

namespace ProjectBB.GamePlay
{
    /// <summary>承伤组件持有的有界临时减速；按来源移除，最强减速生效，不改基础巡航速度。</summary>
    public sealed class SpeedBuffCollection
    {
        private readonly long[] _sources = new long[64];
        private readonly double[] _multipliers = new double[64];
        public double Multiplier
        {
            get { double value = 1; for (int i = 0; i < _sources.Length; i++) if (_sources[i] != 0) value = Math.Min(value, _multipliers[i]); return value; }
        }
        internal bool Add(long source, double multiplier)
        {
            for (int i = 0; i < _sources.Length; i++) if (_sources[i] == source)
            { _multipliers[i] = multiplier; return true; }
            for (int i = 0; i < _sources.Length; i++) if (_sources[i] == 0)
            { _sources[i] = source; _multipliers[i] = multiplier; return true; }
            return false;
        }
        internal void Remove(long source)
        { for (int i = 0; i < _sources.Length; i++) if (_sources[i] == source) _sources[i] = 0; }
        internal void Clear() { Array.Clear(_sources, 0, _sources.Length); }
    }
}
