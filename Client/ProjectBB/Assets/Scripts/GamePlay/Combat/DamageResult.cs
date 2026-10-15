using ProjectBB.LogicCore;

namespace ProjectBB.GamePlay
{
    /// <summary>单方向承伤事实，实际 HP 损失用于飘字；免疫与拒绝分别表达。</summary>
    public readonly struct DamageResult
    {
        public DamageRequest Request { get; }
        public bool Accepted { get; }
        public bool WasImmune { get; }
        public int HpLoss { get; }
        public bool WasLethal { get; }
        public PlanarVector TargetPosition { get; }
        internal DamageResult(DamageRequest request, bool accepted, bool immune, int loss, bool lethal, PlanarVector position)
        {
            Request = request; Accepted = accepted; WasImmune = immune;
            HpLoss = loss; WasLethal = lethal; TargetPosition = position;
        }
    }

    /// <summary>双方承伤及死亡提交后公开的批次快照，不持有角色或表现引用。</summary>
    public readonly struct CollisionBatchResult
    {
        public DamageResult FirstToSecond { get; }
        public DamageResult SecondToFirst { get; }
        public double TimeSeconds { get; }
        internal CollisionBatchResult(DamageResult firstToSecond, DamageResult secondToFirst, double timeSeconds)
        {
            FirstToSecond = firstToSecond; SecondToFirst = secondToFirst; TimeSeconds = timeSeconds;
        }
    }
}
