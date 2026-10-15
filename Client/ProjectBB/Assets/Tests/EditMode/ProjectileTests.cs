using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ProjectBB.GamePlay;
using ProjectBB.LogicCore;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectBB.Tests
{
    /// <summary>圆形弹道几何和实际物理集成；每项独立会话，退出后核对资源恢复。</summary>
    public sealed class ProjectileTests
    {
        private const double Step = 1.0 / 60;
        private static BattleSession Create(int hp = 100, bool immune = false, double deadline = 180)
        {
            var s = new BattleSession(new BattleSettings(speedIndex: 0,
                firstCombat: new CombatDefinition(hp, 0), secondCombat: new CombatDefinition(hp, 0, immune), maxBattleSeconds: deadline));
            s.Collisions.SetTestState(new PlanarVector(-3, -3), default, new PlanarVector(1, 0), default);
            return s;
        }
        private static long Fire(BattleSession s, PlanarVector start, PlanarVector velocity, int source = 1,
            double radius = 0.1, double lifetime = 2, int damage = 10)
        {
            Assert.That(s.TryLaunchProjectile(new CircleProjectileRequest(s.SessionId, source, start, velocity, radius, lifetime, damage), out long id), Is.True);
            return id;
        }
        private static void Run(string name, Action test, List<string> report, List<string> failures)
        {
            try { test(); report.Add("PASS " + name); }
            catch (Exception e) { report.Add("FAIL " + name + "\n" + e); failures.Add(name + ": " + e.Message); }
        }

        [Test]
        public void RelativeSweepIncludesCrossingOverlapAndTangent()
        {
            Assert.That(ProjectileGeometry.Sweep(new PlanarVector(-2, 0), new PlanarVector(480, 0), 0.1,
                new PlanarVector(0, -2), new PlanarVector(0, 480), 0.5, Step / 2, out double time), Is.True);
            Assert.That(time, Is.InRange(0.003, 0.0042));
            Assert.That(ProjectileGeometry.Sweep(default, new PlanarVector(-1, 0), 0.1, default, default, 0.5, 1, out time), Is.True);
            Assert.That(time, Is.Zero);
            Assert.That(ProjectileGeometry.Sweep(new PlanarVector(-2, 0.6), new PlanarVector(4, 0), 0.1,
                default, default, 0.5, 1, out time), Is.True);
            Assert.That(time, Is.EqualTo(0.5).Within(1e-8));
            Assert.That(ProjectileGeometry.Sweep(new PlanarVector(-2, 0.61), new PlanarVector(4, 0), 0.1,
                default, default, 0.5, 1, out _), Is.False);
        }

        [UnityTest]
        public IEnumerator ActualPhysicsProjectileScenarios()
        {
            yield return new EnterPlayMode();
            yield return Verify();
            yield return new ExitPlayMode();
        }

        private static IEnumerator Verify()
        {
            var report = new List<string>();
            var failures = new List<string>();
            int scenes = SceneManager.sceneCount;
            int bodies = UnityEngine.Object.FindObjectsOfType<Rigidbody2D>().Length;
            Run("静止目标高速扫过、身份、承伤与一次回收", () =>
            {
                using (var s = Create())
                {
                    long id = Fire(s, new PlanarVector(-2, 0), new PlanarVector(1000, 0));
                    s.Advance(Step);
                    Assert.That(s.SecondBall.Combat.CurrentHp, Is.EqualTo(90));
                    Assert.That(s.FirstBall.Combat.CurrentHp, Is.EqualTo(100));
                    Assert.That(s.Projectiles.ActiveCount, Is.Zero);
                    Assert.That(s.Projectiles.RecycledCount, Is.EqualTo(1));
                    Assert.That(s.TryDequeueProjectileFeedback(out var hit), Is.True);
                    Assert.That(hit.Damage.Request.AttackId, Is.EqualTo(id));
                    Assert.That(hit.Damage.Request.ContactRound, Is.Zero);
                    Assert.That(hit.Damage.Request.SourceKind, Is.EqualTo(DamageSourceKind.Projectile));
                    Assert.That(hit.Damage.HpLoss, Is.EqualTo(10));
                    s.Advance(Step);
                    Assert.That(s.SecondBall.Combat.CurrentHp, Is.EqualTo(90));
                    Assert.That(s.TryDequeueProjectileFeedback(out _), Is.False);
                }
            }, report, failures);
            Run("双方高速垂直交叉、正面交叉与近失", () =>
            {
                foreach (bool headOn in new[] { false, true })
                using (var s = Create())
                {
                    s.Collisions.SetTestState(new PlanarVector(-3, -3), default,
                        headOn ? new PlanarVector(2, 0) : new PlanarVector(0, -2),
                        headOn ? new PlanarVector(-480, 0) : new PlanarVector(0, 480));
                    s.SecondBall.Motor.SetSpeed(480);
                    Fire(s, new PlanarVector(-2, 0), new PlanarVector(480, 0));
                    s.Advance(Step);
                    Assert.That(s.SecondBall.Combat.CurrentHp, Is.EqualTo(90));
                    Assert.That(s.Projectiles.SweepBudgetExceededCount, Is.Zero);
                    Assert.That(s.Collisions.PhysicsSliceCount, Is.GreaterThan(2));
                }
                using (var s = Create())
                {
                    Fire(s, new PlanarVector(-2, 0.7), new PlanarVector(1000, 0));
                    s.Advance(Step);
                    Assert.That(s.SecondBall.Combat.CurrentHp, Is.EqualTo(100));
                }
            }, report, failures);
            Run("墙边折返峰值命中，整子步直线会漏检", () =>
            {
                using (var s = Create())
                {
                    s.Collisions.SetTestState(new PlanarVector(-3, -3), default, new PlanarVector(4, 0), new PlanarVector(240, 0));
                    s.SecondBall.Motor.SetSpeed(240);
                    Fire(s, new PlanarVector(4.8, -1), new PlanarVector(0, 480));
                    s.Advance(Step);
                    Assert.That(s.Collisions.WallImpactCount, Is.GreaterThanOrEqualTo(1));
                    Assert.That(s.SecondBall.Motor.Velocity.X, Is.LessThan(0));
                    Assert.That(s.SecondBall.Combat.CurrentHp, Is.EqualTo(90));
                    Assert.That(s.Projectiles.HitCount, Is.EqualTo(1));
                    Assert.That(ProjectileGeometry.Sweep(new PlanarVector(4.8, -1), new PlanarVector(0, 480), 0.1,
                        new PlanarVector(4, 0), new PlanarVector(-120, 0), 0.5, Step / 2, out _), Is.False);
                }
            }, report, failures);
            Run("墙边返回路径及安全间隙与细步参考一致", () =>
            {
                foreach (double x in new[] { 3.5, 4.8 })
                {
                    int[] hp = new int[2];
                    for (int reference = 0; reference < 2; reference++)
                    using (var s = new BattleSession(new BattleSettings(speedIndex: 0, fixedStep: reference == 0 ? Step : Step / 20,
                        firstCombat: new CombatDefinition(collisionDamage: 0), secondCombat: new CombatDefinition(collisionDamage: 0))))
                    {
                        s.Collisions.SetTestState(new PlanarVector(-3, -3), default, new PlanarVector(4, 0), new PlanarVector(240, 0));
                        s.SecondBall.Motor.SetSpeed(240);
                        Fire(s, new PlanarVector(x, -3), new PlanarVector(0, 480));
                        for (int i = 0; i < (reference == 0 ? 1 : 20); i++) s.Advance(reference == 0 ? Step : Step / 20);
                        hp[reference] = s.SecondBall.Combat.CurrentHp;
                    }
                    Assert.That(hp[0], Is.EqualTo(hp[1]));
                    Assert.That(hp[0], Is.EqualTo(x == 3.5 ? 90 : 100));
                }
            }, report, failures);
            Run("目标初始重叠、免疫消耗、实体过滤", () =>
            {
                using (var s = Create(immune: true))
                {
                    Fire(s, s.SecondBall.Motor.Position, new PlanarVector(10, 0));
                    s.Advance(Step);
                    Assert.That(s.SecondBall.Combat.CurrentHp, Is.EqualTo(100));
                    Assert.That(s.Projectiles.ActiveCount, Is.Zero);
                    Assert.That(s.TryDequeueProjectileFeedback(out var hit), Is.True);
                    Assert.That(hit.Damage.Accepted && hit.Damage.WasImmune, Is.True);
                    Assert.That(hit.Damage.HpLoss, Is.Zero);
                    Assert.That(CircleProjectileSystem.CanHit(s.FirstBall, s.FirstBall), Is.False);
                    var ally = new BallCharacter(3, s.FirstBall.FactionId, s.SecondBall.Motor, new CombatDefinition());
                    Assert.That(CircleProjectileSystem.CanHit(s.FirstBall, ally), Is.False);
                    ally.Combat.ApplyDamage(100);
                    Assert.That(CircleProjectileSystem.CanHit(s.SecondBall, ally), Is.False);
                }
                using (var s = Create())
                {
                    Fire(s, s.FirstBall.Motor.Position, new PlanarVector(10, 0));
                    s.Advance(Step);
                    Assert.That(s.Projectiles.ActiveCount, Is.EqualTo(1));
                    Assert.That(s.FirstBall.Combat.CurrentHp, Is.EqualTo(100));
                }
            }, report, failures);
            Run("射墙回收、精确寿命截短、池复用新身份", () =>
            {
                using (var s = Create())
                {
                    long first = Fire(s, new PlanarVector(4.8, 3), new PlanarVector(1000, 0));
                    s.Advance(Step);
                    Assert.That(s.Projectiles.ActiveCount, Is.Zero);
                    long second = Fire(s, new PlanarVector(-2, 0), new PlanarVector(1000, 0), lifetime: 0.001);
                    s.Advance(Step);
                    Assert.That(second, Is.GreaterThan(first));
                    Assert.That(s.Projectiles.ActiveCount, Is.Zero);
                    Assert.That(s.SecondBall.Combat.CurrentHp, Is.EqualTo(100));
                    Fire(s, new PlanarVector(-2, 0), new PlanarVector(1000, 0)); s.Advance(Step);
                    Assert.That(s.SecondBall.Combat.CurrentHp, Is.EqualTo(90));
                    Assert.That(s.Projectiles.RecycledCount, Is.EqualTo(3));
                }
            }, report, failures);
            Run("容量满显式拒绝、全量查询无截断、反馈有界", () =>
            {
                using (var s = Create(hp: 10000))
                {
                    for (int i = 0; i < CircleProjectileSystem.Capacity; i++) Fire(s, new PlanarVector(-2, 0), new PlanarVector(1000, 0));
                    Assert.That(s.TryLaunchProjectile(new CircleProjectileRequest(s.SessionId, 1, default, new PlanarVector(1, 0)), out _), Is.False);
                    Assert.That(s.Projectiles.CapacityRejectedCount, Is.EqualTo(1));
                    s.Advance(Step);
                    Assert.That(s.SecondBall.Combat.CurrentHp, Is.EqualTo(10000 - CircleProjectileSystem.Capacity * 10));
                    Assert.That(s.Projectiles.HitCount, Is.EqualTo(CircleProjectileSystem.Capacity));
                    Assert.That(s.Projectiles.ActiveCount, Is.Zero);
                    int feedback = 0; while (s.TryDequeueProjectileFeedback(out _)) feedback++;
                    Assert.That(feedback, Is.EqualTo(64));
                    Assert.That(s.DroppedFeedbackCount, Is.EqualTo(64));
                }
            }, report, failures);
            Run("同边界独立攻击按身份顺序、致死停止并清全部弹道", () =>
            {
                using (var s = Create(hp: 5))
                {
                    Fire(s, s.FirstBall.Motor.Position, new PlanarVector(1, 0), source: 2);
                    Fire(s, s.SecondBall.Motor.Position, new PlanarVector(1, 0));
                    Fire(s, default, new PlanarVector(1, 0));
                    s.Advance(0.25);
                    Assert.That(s.Settlement.WinnerEntityId, Is.EqualTo(2));
                    Assert.That(s.SecondBall.Combat.CurrentHp, Is.EqualTo(5));
                    Assert.That(s.Projectiles.HitCount, Is.EqualTo(1));
                    Assert.That(s.Projectiles.ActiveCount, Is.Zero);
                    Assert.That(s.Projectiles.RecycledCount, Is.EqualTo(3));
                    Assert.That(s.Collisions.SimulationStepCount, Is.EqualTo(1));
                    Assert.That(s.FirstBall.Combat.DeathCommitCount, Is.EqualTo(1));
                }
            }, report, failures);
            Run("接触互伤先于同边界弹道、双方既定请求保留", () =>
            {
                using (var s = new BattleSession(new BattleSettings(standardSpeed: 100,
                    firstCombat: new CombatDefinition(5), secondCombat: new CombatDefinition(5))))
                {
                    s.Collisions.SetTestState(new PlanarVector(-1, 0), new PlanarVector(100, 0), new PlanarVector(1, 0), new PlanarVector(-100, 0));
                    Fire(s, new PlanarVector(1, 0), new PlanarVector(1, 0)); s.Advance(Step);
                    Assert.That(s.Settlement.Outcome, Is.EqualTo(BattleOutcome.Draw));
                    Assert.That(s.Projectiles.HitCount, Is.Zero);
                    Assert.That(s.Projectiles.ActiveCount, Is.Zero);
                }
            }, report, failures);
            Run("暂停后台倍速与帧率不改变弹道时钟", () =>
            {
                using (var s = Create())
                {
                    Fire(s, default, new PlanarVector(0, 1));
                    s.SetPaused(true); s.Advance(10);
                    Assert.That(s.Projectiles.GetSnapshot(0).Position.Y, Is.Zero);
                    s.SetSuspended(true); s.SetPaused(false); s.Advance(10);
                    Assert.That(s.Projectiles.GetSnapshot(0).Position.Y, Is.Zero);
                    s.SetSuspended(false); s.SetTimeScale(2); s.Advance(Step);
                    Assert.That(s.Projectiles.GetSnapshot(0).Position.Y, Is.EqualTo(Step * 2).Within(1e-8));
                }
                using (var a = Create()) using (var b = Create())
                {
                    Fire(a, default, new PlanarVector(0, 1)); Fire(b, default, new PlanarVector(0, 1));
                    for (int i = 0; i < 15; i++) a.Advance(1.0 / 30);
                    for (int i = 0; i < 72; i++) b.Advance(1.0 / 144);
                    Assert.That(a.Projectiles.GetSnapshot(0).Position.Y, Is.EqualTo(b.Projectiles.GetSnapshot(0).Position.Y).Within(1e-8));
                }
            }, report, failures);
            Run("超时边界命中优先、无命中超时也清弹道", () =>
            {
                using (var s = Create(hp: 5, deadline: 0.004))
                {
                    Fire(s, new PlanarVector(-2, 0), new PlanarVector(1000, 0)); s.Advance(Step);
                    Assert.That(s.Settlement.EndReason, Is.EqualTo(BattleEndReason.Elimination));
                    Assert.That(s.ElapsedSeconds, Is.EqualTo(0.004).Within(1e-10));
                }
                using (var s = Create(deadline: 0.004))
                {
                    Fire(s, default, new PlanarVector(0, 1)); s.Advance(Step);
                    Assert.That(s.Settlement.EndReason, Is.EqualTo(BattleEndReason.Timeout));
                    Assert.That(s.Projectiles.ActiveCount, Is.Zero);
                }
            }, report, failures);
            Run("极端速度细分预算显式取消，不扫未验证路程", () =>
            {
                using (var s = Create())
                {
                    s.Collisions.SetTestState(new PlanarVector(-3, -3), default, default, new PlanarVector(10000, 0));
                    s.SecondBall.Motor.SetSpeed(10000);
                    Fire(s, new PlanarVector(-3, 3), new PlanarVector(1, 0)); s.Advance(Step);
                    Assert.That(s.Projectiles.SweepBudgetExceededCount, Is.EqualTo(1));
                    Assert.That(s.Projectiles.ActiveCount, Is.Zero);
                    Assert.That(s.Projectiles.HitCount, Is.Zero);
                }
            }, report, failures);
            Run("重开隔离、旧请求失效、回收记录清空", () =>
            {
                var old = Create();
                var request = new CircleProjectileRequest(old.SessionId, 1, default, new PlanarVector(1, 0));
                Fire(old, new PlanarVector(-2, 0), new PlanarVector(1000, 0)); old.Advance(Step);
                Fire(old, default, new PlanarVector(0, 1));
                old.Dispose(); old.Dispose();
                Assert.That(old.Projectiles.ActiveCount, Is.Zero);
                Assert.That(old.TryDequeueProjectileFeedback(out _), Is.False);
                Assert.That(old.TryLaunchProjectile(request, out _), Is.False);
                using (var next = Create())
                {
                    Assert.That(next.TryLaunchProjectile(request, out _), Is.False);
                    Assert.That(next.Projectiles.ActiveCount, Is.Zero);
                    Assert.That(next.Projectiles.HitCount, Is.Zero);
                    Fire(next, new PlanarVector(-2, 0), new PlanarVector(1000, 0)); next.Advance(Step);
                    Assert.That(next.SecondBall.Combat.CurrentHp, Is.EqualTo(90));
                }
            }, report, failures);
            Run("非法配置及未知来源拒绝且不分配身份", () =>
            {
                using (var s = Create())
                {
                    foreach (var request in new[] {
                        default(CircleProjectileRequest),
                        new CircleProjectileRequest(s.SessionId, 99, default, new PlanarVector(1, 0)),
                        new CircleProjectileRequest(s.SessionId, 1, default, default),
                        new CircleProjectileRequest(s.SessionId, 1, default, new PlanarVector(1, 0), radius: double.NaN),
                        new CircleProjectileRequest(s.SessionId, 1, new PlanarVector(5, 0), new PlanarVector(1, 0)),
                        new CircleProjectileRequest(s.SessionId, 1, default, new PlanarVector(1, 0), lifetime: -1) })
                        Assert.That(s.TryLaunchProjectile(request, out _), Is.False);
                    Assert.That(Fire(s, default, new PlanarVector(0, 1)), Is.EqualTo(1));
                }
            }, report, failures);
            yield return null;
            yield return null;
            Run("释放后场景和刚体数量恢复", () =>
            {
                Assert.That(SceneManager.sceneCount, Is.EqualTo(scenes));
                Assert.That(UnityEngine.Object.FindObjectsOfType<Rigidbody2D>().Length, Is.EqualTo(bodies));
            }, report, failures);
            string output = Path.GetFullPath(Path.Combine(Application.dataPath,
                UnityEditor.SessionState.GetString("ProjectBB.ValidationPhase", "") == "summon_ability"
                    ? "../../outputs/summon_ability_validation" : UnityEditor.SessionState.GetString("ProjectBB.ValidationPhase", "") == "projectile_bounce"
                    ? "../../outputs/projectile_bounce_validation" : UnityEditor.SessionState.GetString("ProjectBB.ValidationPhase", "") == "projectile_launch"
                    ? "../../outputs/projectile_launch_validation" : UnityEditor.SessionState.GetString("ProjectBB.ValidationPhase", "") == "config_skill"
                    ? "../../outputs/config_skill_validation" : "../../outputs/projectile_validation"));
            Directory.CreateDirectory(output);
            File.WriteAllLines(Path.Combine(output, "projectile-scenarios.txt"), report);
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }
    }
}
