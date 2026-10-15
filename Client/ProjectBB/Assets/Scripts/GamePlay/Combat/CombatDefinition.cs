using System;

namespace ProjectBB.GamePlay
{
    /// <summary>单球基础战斗参数的只读定义，每次会话创建独立运行状态。</summary>
    public sealed class CombatDefinition
    {
        public int MaxHp { get; }
        public int CollisionDamage { get; }
        public bool DamageImmune { get; }
        public CombatDefinition(int maxHp = 100, int collisionDamage = 10, bool damageImmune = false)
        {
            if (maxHp <= 0 || collisionDamage < 0) throw new ArgumentOutOfRangeException(nameof(maxHp));
            MaxHp = maxHp;
            CollisionDamage = collisionDamage;
            DamageImmune = damageImmune;
        }
    }
}
