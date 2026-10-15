using System;

namespace ProjectBB.LogicCore
{
    /// <summary>逻辑平面的双精度向量，不依赖 Unity；作为值随运动快照传递。</summary>
    public readonly struct PlanarVector
    {
        public readonly double X;
        public readonly double Y;
        public PlanarVector(double x, double y) { X = x; Y = y; }
        public double LengthSquared => X * X + Y * Y;
        public double Length => Math.Sqrt(LengthSquared);
        public bool IsFinite => IsFiniteNumber(X) && IsFiniteNumber(Y);
        public static bool IsFiniteNumber(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        public PlanarVector NormalizedOr(PlanarVector fallback) => LengthSquared > 1e-24 ? this / Length : fallback;
        public static double Dot(PlanarVector a, PlanarVector b) => a.X * b.X + a.Y * b.Y;
        public static PlanarVector operator +(PlanarVector a, PlanarVector b) => new PlanarVector(a.X + b.X, a.Y + b.Y);
        public static PlanarVector operator -(PlanarVector a, PlanarVector b) => new PlanarVector(a.X - b.X, a.Y - b.Y);
        public static PlanarVector operator -(PlanarVector a) => new PlanarVector(-a.X, -a.Y);
        public static PlanarVector operator *(PlanarVector a, double scale) => new PlanarVector(a.X * scale, a.Y * scale);
        public static PlanarVector operator /(PlanarVector a, double scale) => new PlanarVector(a.X / scale, a.Y / scale);
    }
}
