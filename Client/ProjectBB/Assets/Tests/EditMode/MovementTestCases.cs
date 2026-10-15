using System;
using ProjectBB.LogicCore;

namespace ProjectBB.Tests
{
    /// <summary>纯几何回归仅验证公式，不作为 Dynamic 模拟通过证据。</summary>
    public static class MovementTestCases
    {
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        private static void Near(double actual, double expected, double tolerance = 1e-7)
            => Require(Math.Abs(actual - expected) <= tolerance, "几何计算误差超限。");
        public static void SweptPairFindsHighSpeedCrossing()
        {
            bool hit = CollisionGeometry.SweepCircles(new PlanarVector(-4, 0), new PlanarVector(1000, 0), 0.5,
                new PlanarVector(4, 0), new PlanarVector(-1000, 0), 0.5, 1.0 / 60, out double time);
            Require(hit, "高速穿越必须被检测。");
            Near(time, 0.0035, 1e-12);
        }

        public static void SeparatingAndTangentialPairsDoNotBounce()
        {
            Require(!CollisionGeometry.SweepCircles(new PlanarVector(-0.5, 0), new PlanarVector(-1, 0), 0.5,
                new PlanarVector(0.5, 0), new PlanarVector(1, 0), 0.5, 1, out _), "正在分离不能反弹。");
            Require(!CollisionGeometry.SweepCircles(new PlanarVector(-2, 0), new PlanarVector(1, 0), 0.5,
                new PlanarVector(0, 1), new PlanarVector(0, 0), 0.5, 4, out _), "相切不能制造撞击。");
        }


    }
}
