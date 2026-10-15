using System;
using ProjectBB.GamePlay;
using ProjectBB.LogicCore;

namespace ProjectBB.Tests
{
    /// <summary>保留与运动实现无关的承伤、批次与超时规则；由 Unity Play Mode 回归。</summary>
    public static class CombatTestCases
    {
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        private static void Near(double actual, double expected)
            => Require(Math.Abs(actual - expected) < 1e-8, $"预期 {expected}，实际 {actual}");
        private static CollisionBatchResult Contact(BattleSession session, long round = 1)
        {
            Require(session.Settlement.TryResolveContact(session.SessionId, round, 0, out CollisionBatchResult batch), "合法批次应接受。");
            return batch;
        }
        public static void BasicContactDealsBothDirections()
        {
            using (var session = new BattleSession(new BattleSettings()))
            {
                CollisionBatchResult batch = Contact(session);
                Require(batch.FirstToSecond.Accepted && batch.SecondToFirst.Accepted, "两个方向都必须被接受。");
                Require(session.FirstBall.Combat.CurrentHp == 90 && session.SecondBall.Combat.CurrentHp == 90, "每方应受到 10 点伤害。");
                Require(batch.FirstToSecond.HpLoss == 10 && !session.IsFinished, "非致死接触继续战斗。");
                Require(batch.FirstToSecond.Request.SourceId == 1 && batch.FirstToSecond.Request.TargetId == 2 &&
                    batch.SecondToFirst.Request.SourceId == 2 && batch.SecondToFirst.Request.TargetId == 1, "来源目标不可混淆。");
            }
        }

        public static void MutualLethalContactIsDrawAndCommitsOnce()
        {
            using (var session = new BattleSession(new BattleSettings(firstCombat: new CombatDefinition(5), secondCombat: new CombatDefinition(5))))
            {
                CollisionBatchResult batch = Contact(session);
                Require(batch.FirstToSecond.HpLoss == 5 && batch.SecondToFirst.HpLoss == 5, "过量伤害应只记录实际扣血。");
                Require(batch.FirstToSecond.WasLethal && batch.SecondToFirst.WasLethal, "先承伤待死亡者仍必须执行已捕获攻击。");
                Require(session.Settlement.Outcome == BattleOutcome.Draw && session.Settlement.EndReason == BattleEndReason.Elimination, "双方死亡必须平局。");
                session.Settlement.ResolveTimeout();
                Require(!session.Settlement.TryResolveContact(session.SessionId, 2, 1, out _), "结束后拒绝新批次。");
                Require(!session.FirstBall.Combat.CommitDeath() && !session.SecondBall.Combat.CommitDeath(), "死亡不能重复提交。");
                Require(session.FirstBall.Combat.DeathCommitCount == 1 && session.SecondBall.Combat.DeathCommitCount == 1 &&
                    session.Settlement.ResultCommitCount == 1, "死亡与结果均只能提交一次。");
            }
        }

        public static void ReversingRegistrationPreservesWinner()
        {
            foreach (bool reversed in new[] { false, true })
            using (var session = new BattleSession(new BattleSettings(firstCombat: new CombatDefinition(20, 10), secondCombat: new CombatDefinition(5, 3))))
            {
                var settlement = new BattleSettlementSystem(session.SessionId,
                    reversed ? session.SecondBall : session.FirstBall, reversed ? session.FirstBall : session.SecondBall);
                Require(settlement.TryResolveContact(session.SessionId, 1, 0, out _), "应执行批次。");
                Require(settlement.WinnerEntityId == 1 && session.FirstBall.Combat.CurrentHp == 17 && session.SecondBall.Combat.IsDead,
                    "处理顺序不应影响胜者或死者本次攻击。");
            }
        }



        public static void DuplicateStaleAndForeignContactsAreRejected()
        {
            using (var session = new BattleSession(new BattleSettings()))
            {
                Require(!session.Settlement.TryResolveContact(Guid.NewGuid(), 1, 0, out _), "旧会话身份必须失效。");
                Contact(session, 2);
                Require(!session.Settlement.TryResolveContact(session.SessionId, 2, 0, out _) &&
                    !session.Settlement.TryResolveContact(session.SessionId, 1, 0, out _), "重复及过期轮次不能再扣血。");
                Require(!session.Settlement.TryResolveContact(session.SessionId, 3, double.NaN, out _), "非法时间不能污染去重状态。");
                Contact(session, 3);
                Require(session.FirstBall.Combat.CurrentHp == 80, "只执行两个合法轮次。");
            }
        }

        public static void SameFactionCannotDealCollisionDamage()
        {
            using (var session = new BattleSession(new BattleSettings()))
            {
                var ally = new BallCharacter(3, 1, session.SecondBall.Motor, new CombatDefinition());
                var settlement = new BattleSettlementSystem(session.SessionId, session.FirstBall, ally);
                Require(!settlement.TryResolveContact(session.SessionId, 1, 0, out _) && ally.Combat.CurrentHp == 100, "友方接触不造成基础伤害。");
            }
        }









        public static void TimeoutUsesRatioInsteadOfAbsoluteHp()
        {
            using (var session = new BattleSession(new BattleSettings(firstCombat: new CombatDefinition(200), secondCombat: new CombatDefinition(100))))
            {
                session.FirstBall.Combat.ApplyDamage(100);
                session.SecondBall.Combat.ApplyDamage(40);
                session.Settlement.ResolveTimeout();
                Require(session.Settlement.WinnerEntityId == 2 && session.Settlement.EndReason == BattleEndReason.Timeout, "50% 对 60% 应由蓝方获胜。");
                session.Settlement.ResolveTimeout();
                Require(session.Settlement.ResultCommitCount == 1 && !session.Settlement.TryResolveContact(session.SessionId, 1, 0, out _), "超时只确认一次且拦截后续伤害。");
            }
        }

        public static void EqualRatiosDrawAndLargeValuesDoNotOverflow()
        {
            using (var session = new BattleSession(new BattleSettings(firstCombat: new CombatDefinition(200), secondCombat: new CombatDefinition(100))))
            {
                session.FirstBall.Combat.ApplyDamage(100); session.SecondBall.Combat.ApplyDamage(50);
                session.Settlement.ResolveTimeout();
                Require(session.Settlement.Outcome == BattleOutcome.Draw, "相同比例必须平局。");
            }
            using (var session = new BattleSession(new BattleSettings(firstCombat: new CombatDefinition(int.MaxValue), secondCombat: new CombatDefinition(int.MaxValue - 1))))
            {
                session.FirstBall.Combat.ApplyDamage(1);
                session.Settlement.ResolveTimeout();
                Require(session.Settlement.WinnerEntityId == 2, "大数比例比较不能溢出或被浮点舍入为平局。");
            }
        }











        public static void InvalidCombatInputsFailBeforeBattle()
        {
            int rejected = 0;
            try { new CombatDefinition(0); } catch (ArgumentException) { rejected++; }
            try { new CombatDefinition(collisionDamage: -1); } catch (ArgumentException) { rejected++; }
            try { new BattleSettings(maxBattleSeconds: double.NaN); } catch (ArgumentException) { rejected++; }
            try { new BattleSettings(maxBattleSeconds: 0); } catch (ArgumentException) { rejected++; }
            Require(rejected == 4, "非法生命、伤害和截止时间均必须拒绝。");
        }
    }
}

