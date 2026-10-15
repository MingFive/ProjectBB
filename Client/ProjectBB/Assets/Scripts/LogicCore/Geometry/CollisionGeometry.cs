using System;

namespace ProjectBB.LogicCore
{
    /// <summary>同一时刻命中的墙集合；角落保留两个边界身份，但只代表一次撞击。</summary>
    [Flags]
    public enum ArenaWall { None = 0, Left = 1, Right = 2, Bottom = 4, Top = 8 }

    /// <summary>纯几何连续检测；只查询碰撞时间，不修改实体或决定战斗行为。</summary>
    public static class CollisionGeometry
    {
        public const double TimeTolerance = 1e-10;
        public const double ContactTolerance = 1e-7;

        public static bool SweepWalls(ArenaBounds bounds, PlanarVector position, PlanarVector velocity,
            double radius, double duration, out double time, out ArenaWall walls)
        {
            time = double.PositiveInfinity;
            walls = ArenaWall.None;
            double limitX = bounds.Width * 0.5 - radius;
            double limitY = bounds.Height * 0.5 - radius;
            if (velocity.X > 0) Consider((limitX - position.X) / velocity.X, ArenaWall.Right, duration, ref time, ref walls);
            if (velocity.X < 0) Consider((-limitX - position.X) / velocity.X, ArenaWall.Left, duration, ref time, ref walls);
            if (velocity.Y > 0) Consider((limitY - position.Y) / velocity.Y, ArenaWall.Top, duration, ref time, ref walls);
            if (velocity.Y < 0) Consider((-limitY - position.Y) / velocity.Y, ArenaWall.Bottom, duration, ref time, ref walls);
            return walls != ArenaWall.None;
        }

        private static void Consider(double candidate, ArenaWall wall, double duration, ref double time, ref ArenaWall walls)
        {
            if (candidate < -TimeTolerance || candidate > duration + TimeTolerance) return;
            candidate = Math.Max(0, Math.Min(duration, candidate));
            if (candidate < time - TimeTolerance) { time = candidate; walls = wall; }
            else if (Math.Abs(candidate - time) <= TimeTolerance) walls |= wall;
        }

        public static bool SweepCircles(PlanarVector firstPosition, PlanarVector firstVelocity, double firstRadius,
            PlanarVector secondPosition, PlanarVector secondVelocity, double secondRadius, double duration, out double time)
        {
            time = double.PositiveInfinity;
            PlanarVector offset = secondPosition - firstPosition;
            PlanarVector relative = secondVelocity - firstVelocity;
            double approach = PlanarVector.Dot(offset, relative);
            double speedSquared = relative.LengthSquared;
            if (approach >= 0 || speedSquared <= 1e-24) return false;
            double radius = firstRadius + secondRadius;
            double gap = offset.LengthSquared - radius * radius;
            if (gap <= 0) { time = 0; return true; }
            double discriminant = approach * approach - speedSquared * gap;
            // 相切没有向内法向速度，不制造反弹或接触攻击。
            if (discriminant <= 0) return false;
            double candidate = gap / (-approach + Math.Sqrt(discriminant));
            if (candidate > duration + TimeTolerance) return false;
            time = Math.Max(0, Math.Min(duration, candidate));
            return true;
        }

        public static PlanarVector ReflectWalls(PlanarVector direction, ArenaWall walls)
        {
            bool horizontal = (walls & (ArenaWall.Left | ArenaWall.Right)) != 0;
            bool vertical = (walls & (ArenaWall.Bottom | ArenaWall.Top)) != 0;
            return new PlanarVector(horizontal ? -direction.X : direction.X, vertical ? -direction.Y : direction.Y);
        }
    }
}
