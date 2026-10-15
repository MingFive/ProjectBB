using System;

namespace ProjectBB.LogicCore
{
    /// <summary>圆形查询攻击的相对扫掠；初始重叠与相切均算命中，不决定物理响应。</summary>
    public static class ProjectileGeometry
    {
        public static bool Sweep(PlanarVector start, PlanarVector velocity, double radius,
            PlanarVector targetStart, PlanarVector targetVelocity, double targetRadius, double duration, out double time)
        {
            time = 0;
            PlanarVector offset = targetStart - start;
            PlanarVector relative = targetVelocity - velocity;
            double sum = radius + targetRadius;
            double gap = offset.LengthSquared - sum * sum;
            if (gap <= 0) return true;
            double approach = PlanarVector.Dot(offset, relative);
            double speed = relative.LengthSquared;
            if (approach >= 0 || speed <= 1e-24) return false;
            double discriminant = approach * approach - speed * gap;
            if (discriminant < 0) return false;
            time = gap / (-approach + Math.Sqrt(discriminant));
            return time <= duration;
        }
    }
}
