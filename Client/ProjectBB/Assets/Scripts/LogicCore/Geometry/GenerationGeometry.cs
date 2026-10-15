using System;

namespace ProjectBB.LogicCore
{
    /// <summary>运行时和编辑器共用的方向组几何；圆环不重复首尾方向，扇形包含两端。</summary>
    public static class GenerationGeometry
    {
        public static PlanarVector Direction(PlanarVector forward, int index, int count, double spreadDegrees)
        {
            if (count < 1 || index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
            double angle = count == 1 ? 0 : spreadDegrees == 360 ? 360.0 * index / count : -spreadDegrees / 2 + spreadDegrees * index / (count - 1);
            double radians = angle * Math.PI / 180;
            double c = Math.Cos(radians), s = Math.Sin(radians);
            return new PlanarVector(forward.X * c - forward.Y * s, forward.X * s + forward.Y * c);
        }
    }
}
