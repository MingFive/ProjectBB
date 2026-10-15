using System;

namespace ProjectBB.LogicCore
{
    /// <summary>以原点为中心的矩形边界；球心范围由逻辑半径内缩得到。</summary>
    public readonly struct ArenaBounds
    {
        public double Width { get; }
        public double Height { get; }
        public ArenaBounds(double width, double height)
        {
            if (!PlanarVector.IsFiniteNumber(width) || !PlanarVector.IsFiniteNumber(height) || width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "场地宽高必须为有限正数。");
            Width = width;
            Height = height;
        }

        public PlanarVector Clamp(PlanarVector position, double radius)
        {
            double x = Width * 0.5 - radius;
            double y = Height * 0.5 - radius;
            return new PlanarVector(Math.Max(-x, Math.Min(x, position.X)), Math.Max(-y, Math.Min(y, position.Y)));
        }

        public bool Contains(PlanarVector position, double radius, double tolerance = 1e-7)
        {
            return position.IsFinite && Math.Abs(position.X) + radius <= Width * 0.5 + tolerance
                && Math.Abs(position.Y) + radius <= Height * 0.5 + tolerance;
        }
    }
}
