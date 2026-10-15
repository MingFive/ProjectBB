using System;
using ProjectBB.LogicCore;

namespace ProjectBB.GamePlay
{
    /// <summary>一次圆形弹道释放的值快照；会话身份防止重开后旧请求进入新战斗。</summary>
    public readonly struct CircleProjectileRequest
    {
        public Guid SessionId { get; }
        public int SourceId { get; }
        public PlanarVector Position { get; }
        public PlanarVector Velocity { get; }
        public double Radius { get; }
        public double Lifetime { get; }
        public int Damage { get; }
        public bool BounceOnWall { get; }

        public CircleProjectileRequest(Guid sessionId, int sourceId, PlanarVector position, PlanarVector velocity,
            double radius = 0.15, double lifetime = 2, int damage = 10, bool bounceOnWall = false)
        {
            SessionId = sessionId; SourceId = sourceId; Position = position; Velocity = velocity;
            Radius = radius; Lifetime = lifetime; Damage = damage;
            BounceOnWall = bounceOnWall;
        }
    }

    /// <summary>表现用弹道快照，不泄漏池内可变实例或 Unity 对象。</summary>
    public readonly struct CircleProjectileSnapshot
    {
        public long AttackId { get; }
        public int SourceId { get; }
        public PlanarVector Position { get; }
        public double Radius { get; }
        internal CircleProjectileSnapshot(long attackId, int sourceId, PlanarVector position, double radius)
        { AttackId = attackId; SourceId = sourceId; Position = position; Radius = radius; }
    }

    /// <summary>完成死亡提交后供表现读取的单次弹道承伤事实。</summary>
    public readonly struct ProjectileHitResult
    {
        public DamageResult Damage { get; }
        public double TimeSeconds { get; }
        internal ProjectileHitResult(DamageResult damage, double timeSeconds)
        { Damage = damage; TimeSeconds = timeSeconds; }
    }
}
