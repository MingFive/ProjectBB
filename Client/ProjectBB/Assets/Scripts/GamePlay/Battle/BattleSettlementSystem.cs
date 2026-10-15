using System;
using ProjectBB.LogicCore;

namespace ProjectBB.GamePlay
{
    /// <summary>单场战斗结果，胜者身份由结算器单独提供。</summary>
    public enum BattleOutcome { Running = 0, Victory = 1, Draw = 2 }
    /// <summary>结束原因，死亡结算不会被随后超时覆盖。</summary>
    public enum BattleEndReason { None = 0, Elimination = 1, Timeout = 2 }

    /// <summary>单场批次、延迟死亡与一次性胜负裁决；不直接写入生命值。</summary>
    public sealed class BattleSettlementSystem
    {
        private readonly Guid _sessionId;
        private readonly BallCharacter _first;
        private readonly BallCharacter _second;
        private long _lastContactRound;
        private readonly Func<int, BallCharacter> _find;
        private bool _closed;
        public BattleOutcome Outcome { get; private set; }
        public BattleEndReason EndReason { get; private set; }
        public int WinnerEntityId { get; private set; }
        public int ResultCommitCount { get; private set; }
        public bool IsFinished => Outcome != BattleOutcome.Running;

        internal BattleSettlementSystem(Guid sessionId, BallCharacter first, BallCharacter second, Func<int, BallCharacter> find = null)
        {
            if (sessionId == Guid.Empty || first == null || second == null || first.EntityId == second.EntityId)
                throw new ArgumentException("结算需要有效会话及两个不同实体。");
            _sessionId = sessionId; _first = first; _second = second;
            _find = find ?? (id => id == first.EntityId ? first : id == second.EntityId ? second : null);
        }

        /// <summary>仅接收当前会话的新接触，双方请求先捕获、后执行，最后统一提交死亡。</summary>
        internal bool TryResolveContact(Guid sessionId, long round, double timeSeconds, out CollisionBatchResult result)
            => TryResolveContact(sessionId, round, timeSeconds, _first, _second, out result);

        internal bool TryResolveContact(Guid sessionId, long round, double timeSeconds,
            BallCharacter first, BallCharacter second, out CollisionBatchResult result)
        {
            result = default;
            if (first == null || second == null || _find(first.EntityId) != first || _find(second.EntityId) != second) return false;
            if (_closed || IsFinished || sessionId != _sessionId || round <= _lastContactRound || round <= 0 ||
                !PlanarVector.IsFiniteNumber(timeSeconds) || timeSeconds < 0 || !CircleProjectileSystem.CanHit(first, second)) return false;
            _lastContactRound = round;
            DamageRequest firstRequest = first.Skills.CaptureCollision(_sessionId, round, second);
            DamageRequest secondRequest = second.Skills.CaptureCollision(_sessionId, round, first);
            DamageResult firstResult = first.Skills.ResolveCapturedHit(firstRequest, second);
            DamageResult secondResult = second.Skills.ResolveCapturedHit(secondRequest, first);
            first.Combat.CommitDeath();
            second.Combat.CommitDeath();
            CheckElimination();
            result = new CollisionBatchResult(firstResult, secondResult, timeSeconds);
            return true;
        }

        /// <summary>弹道池保证每个实例仅提交一次；每个单向命中独立提交死亡，后续攻击重新校验。</summary>
        internal bool TryResolveProjectile(Guid sessionId, long attackId, int sourceId, int targetId, int amount, out DamageResult result)
        {
            result = default;
            BallCharacter source = _find(sourceId);
            BallCharacter target = _find(targetId);
            if (_closed || IsFinished || sessionId != _sessionId || attackId <= 0 || amount < 0 || !CircleProjectileSystem.CanHit(source, target)) return false;
            DamageRequest request = source.Skills.CaptureProjectile(sessionId, attackId, target, amount);
            result = source.Skills.ResolveCapturedHit(request, target);
            target.Combat.CommitDeath();
            CheckElimination();
            return result.Accepted;
        }

        private void CheckElimination()
        {
            if (_first.Combat.IsDead || _second.Combat.IsDead)
                Finish(_first.Combat.IsDead ? (_second.Combat.IsDead ? 0 : _second.EntityId) : _first.EntityId, BattleEndReason.Elimination);
        }

        internal bool TryResolveSkill(long attackId, int sourceId, int targetId, int amount, int healRatioBps, out DamageResult result)
        {
            result = default;
            BallCharacter source = _find(sourceId), target = _find(targetId);
            if (_closed || IsFinished || attackId <= 0 || amount < 0 || !CircleProjectileSystem.CanHit(source, target)) return false;
            var request = new DamageRequest(_sessionId, 0, sourceId, targetId, amount, DamageSourceKind.Skill, attackId);
            result = source.Skills.ResolveCapturedHit(request, target);
            // 实际 HP 损失为基数，先承伤后治疗；死亡者不复活。
            if (result.Accepted && healRatioBps > 0) source.Combat.Heal((int)((long)result.HpLoss * healRatioBps / 10000));
            target.Combat.CommitDeath();
            CheckElimination();
            return result.Accepted;
        }

        internal void ResolveTimeout()
        {
            if (_closed || IsFinished) return;
            // 整数交叉相乘精确比较生命比例，避免浮点误差；int×int 在 long 范围内。
            long firstRatio = (long)_first.Combat.CurrentHp * _second.Combat.MaxHp;
            long secondRatio = (long)_second.Combat.CurrentHp * _first.Combat.MaxHp;
            Finish(firstRatio == secondRatio ? 0 : firstRatio > secondRatio ? _first.EntityId : _second.EntityId, BattleEndReason.Timeout);
        }

        private void Finish(int winnerId, BattleEndReason reason)
        {
            if (IsFinished) return;
            WinnerEntityId = winnerId;
            Outcome = winnerId == 0 ? BattleOutcome.Draw : BattleOutcome.Victory;
            EndReason = reason;
            ResultCommitCount++;
        }

        internal void Close() { _closed = true; }
    }
}
