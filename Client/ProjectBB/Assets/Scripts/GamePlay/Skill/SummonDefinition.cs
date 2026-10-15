using System;
using ProjectBB.LogicCore;

namespace ProjectBB.GamePlay
{
    /// <summary>有质量实体的不可变生成定义；运行身份、主人和到期时间属于每个实例。</summary>
    public sealed class SummonDefinition
    {
        public string Id { get; }
        public double Radius { get; }
        public double Speed { get; }
        public double Mass { get; }
        public double Lifetime { get; }
        public int MaxPerOwner { get; }
        public CombatDefinition Combat { get; }
        public SummonDefinition(string id, double radius, double speed, double mass, double lifetime,
            int maxPerOwner, CombatDefinition combat)
        {
            if (string.IsNullOrWhiteSpace(id) || !PlanarVector.IsFiniteNumber(radius) || radius <= 0 ||
                !PlanarVector.IsFiniteNumber(speed) || speed < 0 || speed > 10000 ||
                !PlanarVector.IsFiniteNumber(mass) || mass < 0.01 || mass > 1000 ||
                !PlanarVector.IsFiniteNumber(lifetime) || lifetime <= 0 || lifetime > 60 ||
                maxPerOwner < 1 || maxPerOwner > BattleSession.EntityCapacity - 2 || combat == null)
                throw new ArgumentException("召唤定义的半径、速度、质量、寿命、数量或战斗参数无效。");
            Id = id; Radius = radius; Speed = speed; Mass = mass; Lifetime = lifetime;
            MaxPerOwner = maxPerOwner; Combat = combat;
        }
    }

    /// <summary>会话校验的召唤请求；出生点必须在场内且不与实体重叠，失败不会推挤或瞬移其他实体。</summary>
    public readonly struct SummonRequest
    {
        public Guid SessionId { get; }
        public int OwnerId { get; }
        public SummonDefinition Definition { get; }
        public PlanarVector Position { get; }
        public PlanarVector Direction { get; }
        public SummonRequest(Guid sessionId, int ownerId, SummonDefinition definition, PlanarVector position, PlanarVector direction)
        { SessionId = sessionId; OwnerId = ownerId; Definition = definition; Position = position; Direction = direction; }
    }
}
