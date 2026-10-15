using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using ProjectBB.Common;
using ProjectBB.LogicCore;

namespace ProjectBB.GamePlay
{
    /// <summary>校验成功后发布的单场只读配置快照；记录索引和跨表引用仅在加载期建立。</summary>
    public sealed class BattleContent
    {
        private readonly Dictionary<string, SkillDefinition> _skills;
        public BattleSettings Settings { get; }
        public BallContentDefinition FirstBall { get; }
        public BallContentDefinition SecondBall { get; }
        public string Source { get; }
        public IEnumerable<SkillDefinition> Skills => _skills.Values;

        private BattleContent(string source, BattleSettings settings, BallContentDefinition first, BallContentDefinition second,
            Dictionary<string, SkillDefinition> skills)
        { Source = source; Settings = settings; FirstBall = first; SecondBall = second; _skills = skills; }

        public bool TryGetSkill(string id, out SkillDefinition skill)
        {
            skill = null;
            return id != null && _skills.TryGetValue(id, out skill);
        }

        public static BattleContent Load(string json, string source)
        {
            BattleContentRows rows = JsonConfigReader.Read<BattleContentRows>(json, source);
            void Require(bool valid, string field, string reason)
            { if (!valid) throw new InvalidDataException($"{source}: {field}: {reason}"); }
            double Number(double? value, string field, double minimum, double maximum, bool allowMinimum = false)
            {
                Require(value.HasValue && PlanarVector.IsFiniteNumber(value.Value) &&
                    (allowMinimum ? value.Value >= minimum : value.Value > minimum) && value.Value <= maximum,
                    field, $"必须为有限数，范围 {(allowMinimum ? "[" : "(")}{minimum}, {maximum}]。");
                return value.Value;
            }
            int Integer(int? value, string field, int minimum, int maximum = int.MaxValue)
            {
                Require(value.HasValue && value.Value >= minimum && value.Value <= maximum, field, $"必须为整数，范围 [{minimum}, {maximum}]。");
                return value.Value;
            }
            string Id(string value, string field)
            {
                Require(!string.IsNullOrWhiteSpace(value) && value == value.Trim(), field, "ID 不能为空或含首尾空白。");
                return value;
            }

            Require(rows.SchemaVersion == 1, "schemaVersion", "仅支持版本 1。");
            Require(rows.Battle != null, "battle", "缺失记录。");
            Require(rows.Skills != null && rows.Traits != null && rows.Balls != null, "skills/traits/balls", "必须显式提供记录数组。");
            BattleRow battle = rows.Battle;
            double width = Number(battle.WidthCells, "battle.widthCells", 0, 10000);
            double height = Number(battle.HeightCells, "battle.heightCells", 0, 10000);
            double radius = Number(battle.BallRadiusCells, "battle.ballRadiusCells", 0, 1000);
            Require(width > radius * 4 && height > radius * 4, "battle.ballRadiusCells", "场地宽高必须大于四倍球半径。");
            double speed = Number(battle.StandardSpeedCellsPerSecond, "battle.standardSpeedCellsPerSecond", 0, 10000);
            double speedIndex = Number(battle.SpeedIndex, "battle.speedIndex", 0, 10000, true);
            Require(speed * speedIndex / 100 <= 10000, "battle.speedIndex", "实际速度不能超过 10000 格/秒。");
            int seed = Integer(battle.Seed, "battle.seed", int.MinValue);
            int maxBattleMs = Integer(battle.MaxBattleMs, "battle.maxBattleMs", 1);

            var skills = new Dictionary<string, SkillDefinition>(StringComparer.Ordinal);
            for (int i = 0; i < rows.Skills.Length; i++)
            {
                SkillRow row = rows.Skills[i];
                Require(row != null, $"skills[{i}]", "记录不能为 null。");
                string id = Id(row.Id, $"skills[{i}].id");
                string path = $"skills[{id}]";
                Require(!skills.ContainsKey(id), path + ".id", "重复 ID。");
                SkillKind kind;
                switch (row.Kind)
                {
                    case "circleProjectile": kind = SkillKind.CircleProjectile; break;
                    case "summon": kind = SkillKind.Summon; break;
                    case "area": kind = SkillKind.Area; break;
                    case "trap": kind = SkillKind.Trap; break;
                    case "orbit": kind = SkillKind.Orbit; break;
                    case "drain": kind = SkillKind.Drain; break;
                    default: throw new InvalidDataException($"{source}: {path}.kind: 不支持的技能类型 {row.Kind}。");
                }
                double projectileRadius = Number(row.RadiusCells, path + ".radiusCells", 0, Math.Min(width, height) * 0.5);
                Require(projectileRadius < Math.Min(width, height) * 0.5, path + ".radiusCells", "弹道必须能完整容纳在场地内。");
                ProjectileSpawnAnchor anchor;
                switch (row.SpawnAnchor)
                {
                    case "caster": anchor = ProjectileSpawnAnchor.Caster; break;
                    case "target": anchor = ProjectileSpawnAnchor.Target; break;
                    case "arenaCenter": anchor = ProjectileSpawnAnchor.ArenaCenter; break;
                    default: throw new InvalidDataException($"{source}: {path}.spawnAnchor: 仅支持 caster/target/arenaCenter。");
                }
                ProjectileDirectionMode mode;
                switch (row.DirectionMode)
                {
                    case "towardTarget": mode = ProjectileDirectionMode.TowardTarget; break;
                    case "casterMovement": mode = ProjectileDirectionMode.CasterMovement; break;
                    case "fixedAngle": mode = ProjectileDirectionMode.FixedAngle; break;
                    default: throw new InvalidDataException($"{source}: {path}.directionMode: 仅支持 towardTarget/casterMovement/fixedAngle。");
                }
                var offset = new PlanarVector(
                    Number(row.SpawnOffsetXCells, path + ".spawnOffsetXCells", -width, width, true),
                    Number(row.SpawnOffsetYCells, path + ".spawnOffsetYCells", -height, height, true));
                double angle = Number(row.DirectionAngleDegrees, path + ".directionAngleDegrees", -360, 360, true);
                Require(row.RandomDirection.HasValue, path + ".randomDirection", "必须为布尔值。");
                Require(row.BounceOnWall.HasValue, path + ".bounceOnWall", "必须为布尔值。");
                var launch = new ProjectileLaunchDefinition(anchor, offset, mode, angle, row.RandomDirection.Value);
                SummonDefinition summon = null;
                double skillSpeed = kind == SkillKind.CircleProjectile
                    ? Number(row.SpeedCellsPerSecond, path + ".speedCellsPerSecond", 1e-12, 10000)
                    : Number(row.SpeedCellsPerSecond, path + ".speedCellsPerSecond", 0, 10000, true);
                int lifetimeMs = Integer(row.LifetimeMs, path + ".lifetimeMs", kind == SkillKind.Orbit ? 0 : 1, 60000);
                double lifetime = kind == SkillKind.Orbit && lifetimeMs == 0 ? double.PositiveInfinity : lifetimeMs / 1000.0;
                int maximum = Integer(row.MaxPerOwner, path + ".maxPerOwner", 1, 62);
                Require(kind != SkillKind.Orbit || maximum >= 2, path + ".maxPerOwner", "环绕技能需要容纳两把武器。");
                int count = Integer(row.Count, path + ".count", 1, 32);
                double spread = Number(row.SpreadDegrees, path + ".spreadDegrees", 0, 360, true);
                if (kind == SkillKind.Summon)
                {
                    BallRow body = null;
                    foreach (BallRow candidate in rows.Balls) if (candidate != null && candidate.Id == row.SummonBallId) body = candidate;
                    Require(body != null, path + ".summonBallId", "缺失召唤角色定义。");
                    Require(body.TraitIds != null && body.TraitIds.Length == 0 && body.DamageImmune.HasValue,
                        path + ".summonBallId", "首版召唤角色要求空 traitIds 和明确免疫开关，禁止递归生成。");
                    summon = new SummonDefinition(id, projectileRadius, skillSpeed,
                        Number(row.Mass, path + ".mass", 0.01, 1000, true), lifetime, maximum,
                        new CombatDefinition(Integer(body.MaxHp, path + ".summon.maxHp", 1),
                            Integer(body.CollisionDamage, path + ".summon.collisionDamage", 0), body.DamageImmune.Value));
                }
                Require(kind == SkillKind.CircleProjectile || !row.BounceOnWall.Value, path + ".bounceOnWall", "仅弹道允许墙反弹开关。");
                Require(kind == SkillKind.CircleProjectile || kind == SkillKind.Summon || count == 1,
                    path + ".count", "生成数量只用于弹道或召唤；环绕技能固定创建两把武器。");
                skills.Add(id, new SkillDefinition(id, skillSpeed, projectileRadius, lifetime,
                    Integer(row.Damage, path + ".damage", 0), launch, row.BounceOnWall.Value, kind, summon, count, spread, maximum,
                    Number(row.OrbitRadiusCells, path + ".orbitRadiusCells", 0, kind == SkillKind.Orbit ? Math.Min(width, height) : 10000),
                    Number(row.AngularSpeedDegrees, path + ".angularSpeedDegrees", -1440, 1440, true),
                    Integer(row.HitIntervalMs, path + ".hitIntervalMs", 10) / 1000.0,
                    Integer(row.HealRatioBps, path + ".healRatioBps", 0, 10000),
                    Integer(row.SpeedMultiplierBps, path + ".speedMultiplierBps", 0, 10000)));
            }
            var traits = new Dictionary<string, PeriodicTraitDefinition>(StringComparer.Ordinal);
            for (int i = 0; i < rows.Traits.Length; i++)
            {
                TraitRow row = rows.Traits[i];
                Require(row != null, $"traits[{i}]", "记录不能为 null。");
                string id = Id(row.Id, $"traits[{i}].id");
                string path = $"traits[{id}]";
                Require(!traits.ContainsKey(id), path + ".id", "重复 ID。");
                Require(row.Trigger == "periodic" || row.Trigger == "onStart" || row.Trigger == "onWall" || row.Trigger == "onContact",
                    path + ".trigger", "仅支持 periodic/onStart/onWall/onContact。");
                string skillId = Id(row.SkillId, path + ".skillId");
                Require(skills.ContainsKey(skillId), path + ".skillId", $"缺失技能 {skillId}。");
                // 当前固定子步为 1/120 秒；10ms 下限保证每能力每子步至多一次，不产生无界补发。
                int periodMs = Integer(row.PeriodMs, path + ".periodMs", 10);
                Require(row.FireOnStart.HasValue, path + ".fireOnStart", "必须明确首次触发方式。");
                traits.Add(id, new PeriodicTraitDefinition(id, skills[skillId], periodMs / 1000.0, row.FireOnStart.Value, row.Trigger));
            }
            var balls = new Dictionary<string, BallContentDefinition>(StringComparer.Ordinal);
            for (int i = 0; i < rows.Balls.Length; i++)
            {
                BallRow row = rows.Balls[i];
                Require(row != null, $"balls[{i}]", "记录不能为 null。");
                string id = Id(row.Id, $"balls[{i}].id");
                string path = $"balls[{id}]";
                Require(!balls.ContainsKey(id), path + ".id", "重复 ID。");
                Require(row.TraitIds != null && row.TraitIds.Length <= 16, path + ".traitIds", "必须显式提供数组，每球最多 16 个 Trait。");
                Require(row.DamageImmune.HasValue, path + ".damageImmune", "必须明确免疫开关。");
                var initial = new PeriodicTraitDefinition[row.TraitIds.Length];
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (int j = 0; j < initial.Length; j++)
                {
                    string traitId = Id(row.TraitIds[j], path + $".traitIds[{j}]");
                    Require(traits.ContainsKey(traitId), path + $".traitIds[{j}]", $"缺失 Trait {traitId}。");
                    Require(seen.Add(traitId), path + $".traitIds[{j}]", $"重复引用 {traitId}。");
                    initial[j] = traits[traitId];
                }
                balls.Add(id, new BallContentDefinition(id, new CombatDefinition(
                    Integer(row.MaxHp, path + ".maxHp", 1), Integer(row.CollisionDamage, path + ".collisionDamage", 0),
                    row.DamageImmune.Value), initial));
            }
            string firstId = Id(battle.FirstBallId, "battle.firstBallId");
            string secondId = Id(battle.SecondBallId, "battle.secondBallId");
            Require(balls.ContainsKey(firstId), "battle.firstBallId", $"缺失小球 {firstId}。");
            Require(balls.ContainsKey(secondId), "battle.secondBallId", $"缺失小球 {secondId}。");
            var settings = new BattleSettings(width, height, radius, speed, speedIndex, seed,
                firstCombat: balls[firstId].Combat, secondCombat: balls[secondId].Combat, maxBattleSeconds: maxBattleMs / 1000.0,
                projectileCapacity: Integer(battle.ProjectileCapacity, "battle.projectileCapacity", 1, 4096));
            return new BattleContent(source, settings, balls[firstId], balls[secondId], skills);
        }
    }

    /// <summary>已落地的有限技能模块，枚举不承诺任意效果组合。</summary>
    public enum SkillKind { CircleProjectile, Summon, Area, Trap, Orbit, Drain }

    /// <summary>技能只读定义；数值和生成规则仅在 JSON 维护。</summary>
    public sealed class SkillDefinition
    {
        public string Id { get; }
        public double Speed { get; }
        public double Radius { get; }
        public double Lifetime { get; }
        public int Damage { get; }
        public ProjectileLaunchDefinition Launch { get; }
        public bool BounceOnWall { get; }
        public SkillKind Kind { get; }
        public SummonDefinition Summon { get; }
        public int Count { get; }
        public double SpreadDegrees { get; }
        public int MaxPerOwner { get; }
        public double OrbitRadius { get; }
        public double AngularSpeed { get; }
        public double HitInterval { get; }
        public int HealRatioBps { get; }
        public double SpeedMultiplier { get; }
        internal SkillDefinition(string id, double speed, double radius, double lifetime, int damage,
            ProjectileLaunchDefinition launch, bool bounceOnWall, SkillKind kind, SummonDefinition summon,
            int count, double spread, int maximum, double orbitRadius, double angularSpeed, double interval, int heal, int slow)
        {
            Id = id; Speed = speed; Radius = radius; Lifetime = lifetime; Damage = damage; Launch = launch; BounceOnWall = bounceOnWall;
            Kind = kind; Summon = summon; Count = count; SpreadDegrees = spread; MaxPerOwner = maximum;
            OrbitRadius = orbitRadius; AngularSpeed = angularSpeed; HitInterval = interval; HealRatioBps = heal; SpeedMultiplier = slow / 10000.0;
        }
    }

    /// <summary>周期触发定义，计时和触发次数属于角色的 TraitManager。</summary>
    public sealed class PeriodicTraitDefinition
    {
        public string Id { get; }
        public SkillDefinition Skill { get; }
        public double Period { get; }
        public bool FireOnStart { get; }
        public string Trigger { get; }
        internal PeriodicTraitDefinition(string id, SkillDefinition skill, double period, bool fireOnStart, string trigger)
        { Id = id; Skill = skill; Period = period; FireOnStart = fireOnStart; Trigger = trigger; }
    }

    /// <summary>角色初始配置，复制能力列表后仅公开不可变包装，不泄漏反序列化记录。</summary>
    public sealed class BallContentDefinition
    {
        public string Id { get; }
        public CombatDefinition Combat { get; }
        public ReadOnlyCollection<PeriodicTraitDefinition> Traits { get; }
        internal BallContentDefinition(string id, CombatDefinition combat, PeriodicTraitDefinition[] traits)
        { Id = id; Combat = combat; Traits = Array.AsReadOnly((PeriodicTraitDefinition[])traits.Clone()); }
    }
}
