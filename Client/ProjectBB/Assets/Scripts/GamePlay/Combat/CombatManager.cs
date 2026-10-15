using System;
using ProjectBB.LogicCore;

namespace ProjectBB.GamePlay
{
    /// <summary>单球生命、免疫与死亡状态的唯一写入入口，随所属会话释放。</summary>
    public sealed class CombatManager
    {
        public int MaxHp { get; }
        public int CurrentHp { get; private set; }
        public int CollisionDamage { get; }
        public bool DamageImmune { get; }
        public bool IsPendingDeath => CurrentHp == 0 && !IsDead;
        public bool IsDead { get; private set; }
        public int DeathCommitCount { get; private set; }
        public SpeedBuffCollection SpeedBuffs { get; } = new SpeedBuffCollection();
        public CombatManager(CombatDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            MaxHp = CurrentHp = definition.MaxHp;
            CollisionDamage = definition.CollisionDamage;
            DamageImmune = definition.DamageImmune;
        }

        internal int ApplyDamage(int damage)
        {
            if (IsDead) return 0;
            int loss = DamageFormula.CalculateHpLoss(CurrentHp, damage, DamageImmune);
            CurrentHp -= loss;
            return loss;
        }

        internal bool CommitDeath()
        {
            if (!IsPendingDeath) return false;
            IsDead = true;
            DeathCommitCount++;
            SpeedBuffs.Clear();
            return true;
        }

        /// <summary>仅治疗存活角色；不允许待死亡或正式死亡者借吸血复活。</summary>
        internal int Heal(int amount)
        {
            if (amount <= 0 || IsDead || IsPendingDeath) return 0;
            int gain = Math.Min(amount, MaxHp - CurrentHp);
            CurrentHp += gain;
            return gain;
        }
    }
}
