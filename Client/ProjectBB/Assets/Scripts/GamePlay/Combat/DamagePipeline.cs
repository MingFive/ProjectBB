namespace ProjectBB.GamePlay
{
    /// <summary>转发已捕获批次的命中至统一承伤入口，不检测几何或提前提交死亡。</summary>
    internal static class DamagePipeline
    {
        internal static DamageResult Resolve(DamageRequest request, BallCharacter target)
        {
            bool accepted = request.TargetId == target.EntityId && !target.Combat.IsDead && request.Amount >= 0;
            if (!accepted) return new DamageResult(request, false, false, 0, false, target.Motor.Position);
            int loss = target.Combat.ApplyDamage(request.Amount);
            return new DamageResult(request, true, target.Combat.DamageImmune, loss,
                loss > 0 && target.Combat.IsPendingDeath, target.Motor.Position);
        }
    }
}
