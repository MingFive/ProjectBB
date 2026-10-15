using System;
using ProjectBB.LogicCore;

namespace ProjectBB.GamePlay
{
    /// <summary>基础战斗原型的只读参数快照，后续由正式配置装配，不承担运行状态。</summary>
    public sealed class BattleSettings
    {
        public ArenaBounds Bounds { get; }
        public double Radius { get; }
        public double Speed { get; }
        public int Seed { get; }
        public double FixedStep { get; }
        public int MaxStepsPerFrame { get; }
        public int PhysicsSubsteps { get; }
        public double CruiseAcceleration { get; }
        public CombatDefinition FirstCombat { get; }
        public CombatDefinition SecondCombat { get; }
        public double MaxBattleSeconds { get; }
        public int ProjectileCapacity { get; }

        public BattleSettings(double width = 10, double height = 10, double radius = 0.5,
            double standardSpeed = 3.5355339059327378, double speedIndex = 100, int seed = 20261010,
            double fixedStep = 1.0 / 60.0, int maxStepsPerFrame = 8,
            CombatDefinition firstCombat = null, CombatDefinition secondCombat = null, double maxBattleSeconds = 180,
            int physicsSubsteps = 2, double cruiseAcceleration = 7.0710678118654755, int projectileCapacity = 128)
        {
            Bounds = new ArenaBounds(width, height);
            if (!PlanarVector.IsFiniteNumber(radius) || radius <= 0 || width <= radius * 4 || height <= radius * 4)
                throw new ArgumentOutOfRangeException(nameof(radius), "运动样例要求场地宽高均大于四倍球半径。");
            if (!PlanarVector.IsFiniteNumber(standardSpeed) || standardSpeed <= 0 ||
                !PlanarVector.IsFiniteNumber(speedIndex) || speedIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(speedIndex));
            if (!PlanarVector.IsFiniteNumber(fixedStep) || fixedStep <= 0 || fixedStep > 0.1 ||
                maxStepsPerFrame <= 0 || physicsSubsteps <= 0 || physicsSubsteps > 8) throw new ArgumentOutOfRangeException(nameof(fixedStep));
            if (!PlanarVector.IsFiniteNumber(cruiseAcceleration) || cruiseAcceleration <= 0 || cruiseAcceleration > float.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(cruiseAcceleration));
            Radius = radius;
            Speed = standardSpeed * (speedIndex / 100.0);
            if (!PlanarVector.IsFiniteNumber(Speed)) throw new ArgumentOutOfRangeException(nameof(speedIndex));
            Seed = seed;
            FixedStep = fixedStep;
            MaxStepsPerFrame = maxStepsPerFrame;
            PhysicsSubsteps = physicsSubsteps;
            CruiseAcceleration = cruiseAcceleration;
            if (!PlanarVector.IsFiniteNumber(maxBattleSeconds) || maxBattleSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxBattleSeconds));
            FirstCombat = firstCombat ?? new CombatDefinition();
            SecondCombat = secondCombat ?? new CombatDefinition();
            MaxBattleSeconds = maxBattleSeconds;
            if (projectileCapacity < 1 || projectileCapacity > 4096) throw new ArgumentOutOfRangeException(nameof(projectileCapacity));
            ProjectileCapacity = projectileCapacity;
        }
    }
}
