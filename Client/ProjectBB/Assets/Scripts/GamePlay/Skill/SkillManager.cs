using System;
using ProjectBB.LogicCore;

namespace ProjectBB.GamePlay
{
    /// <summary>首版统一命中入口，随角色存在；支持基础碰撞与圆形弹道，尚无技能时间线。</summary>
    public sealed class SkillManager
    {
        private readonly BallCharacter _owner;
        private BattleSession _session;
        private BattleContent _content;
        internal SkillManager(BallCharacter owner) { _owner = owner; }
        internal void Bind(BattleSession session, BattleContent content) { _session = session; _content = content; }

        /// <summary>索敌与生成归技能侧；出生点按弹道半径内缩到场内，不改变小球运动或直接扣血。</summary>
        public bool TryCast(SkillCastRequest request, out long attackId)
        {
            attackId = 0;
            if (_session == null || _content == null || request.SessionId != _session.SessionId ||
                _session.IsDisposed || _session.IsFinished || _session.IsPaused || _session.IsSuspended ||
                _owner.Combat.IsDead || _owner.Combat.IsPendingDeath ||
                !_content.TryGetSkill(request.SkillId, out SkillDefinition skill)) return false;
            if (request.Anchor.HasValue && !request.Anchor.Value.IsFinite) return false;
            BallCharacter target = request.TargetId == 0 ? _session.FindEnemy(_owner) : _session.FindBall(request.TargetId);
            if (!CircleProjectileSystem.CanHit(_owner, target)) return false;
            ProjectileLaunchDefinition launch = skill.Launch;
            PlanarVector anchor = launch.SpawnAnchor == ProjectileSpawnAnchor.Target ? target.Motor.Position :
                launch.SpawnAnchor == ProjectileSpawnAnchor.ArenaCenter ? default : _owner.Motor.Position;
            PlanarVector position = (request.Anchor ?? anchor) + launch.SpawnOffset;
            if (skill.Kind == SkillKind.CircleProjectile || skill.Kind == SkillKind.Trap)
                position = _session.Arena.Bounds.Clamp(position, skill.Radius);
            PlanarVector direction;
            if (launch.RandomDirection) direction = _session.NextSkillDirection();
            else if (launch.DirectionMode == ProjectileDirectionMode.FixedAngle)
            {
                double radians = launch.DirectionAngleDegrees * Math.PI / 180;
                direction = new PlanarVector(Math.Cos(radians), Math.Sin(radians));
            }
            else if (launch.DirectionMode == ProjectileDirectionMode.CasterMovement)
                direction = _owner.Motor.Velocity.NormalizedOr(_owner.Motor.Direction);
            else direction = (target.Motor.Position - position).NormalizedOr(_owner.Motor.Direction);
            // 贴附只接受新接触的明确目标；普通释放请求不能凭最近目标建立约束。
            if (skill.Kind == SkillKind.Drain && request.TargetId == 0) return false;
            if (skill.Kind != SkillKind.CircleProjectile && skill.Kind != SkillKind.Summon)
                return _session.Abilities.TryCast(_owner, target, skill, position, out attackId);
            bool accepted = false;
            for (int i = 0; i < skill.Count; i++)
            {
                PlanarVector generated = GenerationGeometry.Direction(direction, i, skill.Count, skill.SpreadDegrees);
                if (skill.Kind == SkillKind.Summon)
                {
                    PlanarVector spawn = position;
                    if (launch.SpawnAnchor == ProjectileSpawnAnchor.Caster && launch.SpawnOffset.LengthSquared < 1e-24)
                        spawn = _owner.Motor.Position + generated * (_owner.Motor.Radius + skill.Radius + 0.05);
                    bool created = _session.TrySummon(new SummonRequest(request.SessionId, _owner.EntityId, skill.Summon, spawn, generated), out int id);
                    if (created) { accepted = true; attackId = id; }
                }
                else if (_session.TryLaunchProjectile(new CircleProjectileRequest(request.SessionId, _owner.EntityId,
                    position, generated * skill.Speed, skill.Radius, skill.Lifetime, skill.Damage, skill.BounceOnWall), out long id))
                { accepted = true; attackId = id; }
            }
            return accepted;
        }
        internal DamageRequest CaptureCollision(Guid sessionId, long round, BallCharacter target)
            => new DamageRequest(sessionId, round, _owner.EntityId, target.EntityId, _owner.Combat.CollisionDamage);

        internal DamageRequest CaptureProjectile(Guid sessionId, long attackId, BallCharacter target, int amount)
            => new DamageRequest(sessionId, 0, _owner.EntityId, target.EntityId, amount, DamageSourceKind.Projectile, attackId);

        // 来源有效性由批次开始时锁定，不因另一方向先扣血而取消本方向。
        internal DamageResult ResolveCapturedHit(DamageRequest request, BallCharacter target)
            => DamagePipeline.Resolve(request, target);
    }
}
