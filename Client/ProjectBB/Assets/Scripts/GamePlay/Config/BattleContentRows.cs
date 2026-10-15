using System.ComponentModel;

namespace ProjectBB.GamePlay
{
    // 仅供加载期反序列化的具名记录；可空值用于区分缺失字段与合法的零/false，不进入战斗实例。
    internal sealed class BattleContentRows
    {
        public int? SchemaVersion { get; set; }
        public BattleRow Battle { get; set; }
        public SkillRow[] Skills { get; set; }
        public TraitRow[] Traits { get; set; }
        public BallRow[] Balls { get; set; }
    }

    internal sealed class BattleRow
    {
        public double? WidthCells { get; set; }
        public double? HeightCells { get; set; }
        public double? BallRadiusCells { get; set; }
        public double? StandardSpeedCellsPerSecond { get; set; }
        public double? SpeedIndex { get; set; }
        public int? Seed { get; set; }
        public int? MaxBattleMs { get; set; }
        public string FirstBallId { get; set; }
        public string SecondBallId { get; set; }
        [DefaultValue(128)] public int? ProjectileCapacity { get; set; }
    }

    internal sealed class SkillRow
    {
        public string Id { get; set; }
        public string Kind { get; set; }
        public double? SpeedCellsPerSecond { get; set; }
        public double? RadiusCells { get; set; }
        public int? LifetimeMs { get; set; }
        public int? Damage { get; set; }
        // 版本 1 的兼容扩展；省略时保持原来的自身中心出生、朝敌方直线发射。
        [DefaultValue("caster")] public string SpawnAnchor { get; set; }
        [DefaultValue(0d)] public double? SpawnOffsetXCells { get; set; }
        [DefaultValue(0d)] public double? SpawnOffsetYCells { get; set; }
        [DefaultValue("towardTarget")] public string DirectionMode { get; set; }
        [DefaultValue(0d)] public double? DirectionAngleDegrees { get; set; }
        [DefaultValue(false)] public bool? RandomDirection { get; set; }
        [DefaultValue(false)] public bool? BounceOnWall { get; set; }
        [DefaultValue("")] public string SummonBallId { get; set; }
        [DefaultValue(1d)] public double? Mass { get; set; }
        [DefaultValue(10)] public int? MaxPerOwner { get; set; }
        [DefaultValue(1)] public int? Count { get; set; }
        [DefaultValue(0d)] public double? SpreadDegrees { get; set; }
        [DefaultValue(1d)] public double? OrbitRadiusCells { get; set; }
        [DefaultValue(180d)] public double? AngularSpeedDegrees { get; set; }
        [DefaultValue(250)] public int? HitIntervalMs { get; set; }
        [DefaultValue(10000)] public int? HealRatioBps { get; set; }
        [DefaultValue(5000)] public int? SpeedMultiplierBps { get; set; }
    }

    internal sealed class TraitRow
    {
        public string Id { get; set; }
        public string Trigger { get; set; }
        public string SkillId { get; set; }
        public int? PeriodMs { get; set; }
        public bool? FireOnStart { get; set; }
    }

    internal sealed class BallRow
    {
        public string Id { get; set; }
        public int? MaxHp { get; set; }
        public int? CollisionDamage { get; set; }
        public bool? DamageImmune { get; set; }
        public string[] TraitIds { get; set; }
    }
}
