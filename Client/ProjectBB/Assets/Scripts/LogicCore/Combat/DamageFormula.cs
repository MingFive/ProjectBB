using System;

namespace ProjectBB.LogicCore
{
    /// <summary>首版生命伤害纯计算，不接收实体，也不提交死亡。</summary>
    public static class DamageFormula
    {
        public static int CalculateHpLoss(int currentHp, int requestedDamage, bool immune)
        {
            if (currentHp < 0 || requestedDamage < 0) throw new ArgumentOutOfRangeException(nameof(requestedDamage));
            return immune ? 0 : Math.Min(currentHp, requestedDamage);
        }
    }
}
