using System;

namespace ProjectBB.GamePlay
{
    /// <summary>伤害来源区分接触轮次与弹道攻击身份。</summary>
    public enum DamageSourceKind { Collision = 0, Projectile = 1, Skill = 2 }
    /// <summary>批次开始时捕获的有向伤害请求，携带会话与接触轮次身份。</summary>
    public readonly struct DamageRequest
    {
        public Guid SessionId { get; }
        public long ContactRound { get; }
        public long AttackId { get; }
        public DamageSourceKind SourceKind { get; }
        public int SourceId { get; }
        public int TargetId { get; }
        public int Amount { get; }
        internal DamageRequest(Guid sessionId, long contactRound, int sourceId, int targetId, int amount,
            DamageSourceKind sourceKind = DamageSourceKind.Collision, long attackId = 0)
        {
            SessionId = sessionId; ContactRound = contactRound;
            SourceId = sourceId; TargetId = targetId; Amount = amount;
            SourceKind = sourceKind; AttackId = attackId;
        }
    }
}
