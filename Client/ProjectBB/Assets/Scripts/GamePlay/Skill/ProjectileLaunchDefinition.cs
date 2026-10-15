using ProjectBB.LogicCore;

namespace ProjectBB.GamePlay
{
    /// <summary>弹道出生参考点，取释放瞬间的逻辑位置，不跟随之后的实体移动。</summary>
    public enum ProjectileSpawnAnchor { Caster, Target, ArenaCenter }

    /// <summary>非随机发射的方向来源；固定角度使用世界 XY 坐标。</summary>
    public enum ProjectileDirectionMode { TowardTarget, CasterMovement, FixedAngle }

    /// <summary>技能的只读生成参数；偏移为世界 XY 格，随机开关仅覆盖方向，不改变出生点。</summary>
    public sealed class ProjectileLaunchDefinition
    {
        public ProjectileSpawnAnchor SpawnAnchor { get; }
        public PlanarVector SpawnOffset { get; }
        public ProjectileDirectionMode DirectionMode { get; }
        public double DirectionAngleDegrees { get; }
        public bool RandomDirection { get; }
        internal ProjectileLaunchDefinition(ProjectileSpawnAnchor anchor, PlanarVector offset,
            ProjectileDirectionMode mode, double angleDegrees, bool randomDirection)
        { SpawnAnchor = anchor; SpawnOffset = offset; DirectionMode = mode; DirectionAngleDegrees = angleDegrees; RandomDirection = randomDirection; }
    }
}
