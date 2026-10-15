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
    /// <summary>实际 Play Mode 中驱动独立 PhysicsScene2D，验证回调、控制、结算和释放。</summary>
    public sealed class DynamicPhysicsTests
    {
        private const double Step = 1.0 / 60;
        private static BattleSession HeadOn(int hp = 100, double speed = 3.5355339059, double deadline = 180, bool immune = false)
        {
            var session = new BattleSession(new BattleSettings(standardSpeed: speed,
                firstCombat: new CombatDefinition(hp, 10, immune), secondCombat: new CombatDefinition(hp), maxBattleSeconds: deadline));
            session.Collisions.SetTestState(new PlanarVector(-1, 0), new PlanarVector(speed, 0), new PlanarVector(1, 0), new PlanarVector(-speed, 0));
            return session;
        }
        private static void UntilContact(BattleSession session)
        {
            for (int i = 0; i < 120 && session.Collisions.ContactRound == 0; i++) session.Advance(Step);
            Assert.That(session.Collisions.ContactRound, Is.EqualTo(1));
        }
        private static void Run(string name, Action action, List<string> results, List<string> failures)
        {
            try { action(); results.Add("PASS " + name); }
            catch (Exception exception) { results.Add("FAIL " + name + "\n" + exception); failures.Add(name + ": " + exception.Message); }
        }

        [UnityTest]
        public IEnumerator PhysicsIntegrationScenarios()
        {
            yield return new EnterPlayMode();
            yield return VerifyScenarios();
            yield return new ExitPlayMode();
        }

        // 域重载完成后才创建带闭包的协程，避免测试运行器恢复时丢失闭包对象。
        private static IEnumerator VerifyScenarios()
        {
            var results = new List<string>();
            var failures = new List<string>();
            SimulationMode2D originalMode = Physics2D.simulationMode;
            int sceneCount = SceneManager.sceneCount;
            int bodyCount = UnityEngine.Object.FindObjectsOfType<Rigidbody2D>().Length;
            Run("基础互伤与双方回调去重", () =>
            {
                using (var s = HeadOn())
                {
                    UntilContact(s);
                    Assert.That(s.FirstBall.Combat.CurrentHp, Is.EqualTo(90));
                    Assert.That(s.SecondBall.Combat.CurrentHp, Is.EqualTo(90));
                    Assert.That(s.FirstBall.Motor.Velocity.X, Is.LessThan(0));
                    Assert.That(s.SecondBall.Motor.Velocity.X, Is.GreaterThan(0));
                    Assert.That(s.TryDequeueFeedback(out _), Is.True);
                    Assert.That(s.TryDequeueFeedback(out _), Is.False);
                }
            }, results, failures);
            Run("同死平局与死亡边界停止", () =>
            {
                using (var s = HeadOn(5, 100))
                {
                    s.Advance(0.25);
                    Assert.That(s.Settlement.Outcome, Is.EqualTo(BattleOutcome.Draw));
                    Assert.That(s.Settlement.EndReason, Is.EqualTo(BattleEndReason.Elimination));
                    Assert.That(s.FirstBall.Combat.CurrentHp, Is.Zero);
                    Assert.That(s.SecondBall.Combat.CurrentHp, Is.Zero);
                    Assert.That(s.Collisions.SimulationStepCount, Is.EqualTo(1));
                    Assert.That(s.ElapsedSeconds, Is.EqualTo(Step / 2).Within(1e-10));
                    var p = s.FirstBall.Motor.Position;
                    s.Advance(0.25);
                    Assert.That(s.FirstBall.Motor.Position, Is.EqualTo(p));
                    Assert.That(s.Settlement.ResultCommitCount, Is.EqualTo(1));
                }
            }, results, failures);
            Run("持续接触不重复扣血", () =>
            {
                using (var s = HeadOn(speed: 0.1))
                {
                    s.Collisions.SetTestState(new PlanarVector(-0.501, 0), new PlanarVector(0.1, 0), new PlanarVector(0.501, 0), new PlanarVector(-0.1, 0));
                    UntilContact(s);
                    s.FirstBall.Motor.SetStopped(true); s.SecondBall.Motor.SetStopped(true);
                    for (int i = 0; i < 120; i++) s.Advance(Step);
                    Assert.That(s.Collisions.IsTouching, Is.True);
                    Assert.That(s.Collisions.ContactRound, Is.EqualTo(1));
                    Assert.That(s.FirstBall.Combat.CurrentHp, Is.EqualTo(90));
                }
            }, results, failures);
            Run("真实分离后重撞", () =>
            {
                using (var s = HeadOn())
                {
                    for (int i = 0; i < 600 && s.Collisions.ContactRound < 2; i++) s.Advance(Step);
                    Assert.That(s.Collisions.ContactRound, Is.EqualTo(2));
                    Assert.That(s.FirstBall.Combat.CurrentHp, Is.EqualTo(80));
                }
            }, results, failures);
            Run("击退保护与有限巡航恢复", () =>
            {
                using (var s = new BattleSession(new BattleSettings()))
                {
                    s.Collisions.SetTestState(new PlanarVector(-2, 2), new PlanarVector(1, 0), new PlanarVector(2, -2), default);
                    s.FirstBall.Motor.SetSpeed(1);
                    s.FirstBall.Motor.ApplyImpulse(new PlanarVector(0, 5));
                    s.Advance(Step);
                    Assert.That(s.FirstBall.Motor.Velocity.Y, Is.EqualTo(5).Within(0.01));
                    s.SetPaused(true); s.Advance(10); s.SetPaused(false);
                    for (int i = 0; i < 4; i++) s.Advance(Step);
                    Assert.That(s.FirstBall.Motor.Velocity.Length, Is.GreaterThan(5));
                    for (int i = 0; i < 5; i++) s.Advance(Step);
                    double speed = s.FirstBall.Motor.Velocity.Length;
                    Assert.That(speed, Is.GreaterThan(4));
                    s.Advance(Step);
                    Assert.That(speed - s.FirstBall.Motor.Velocity.Length, Is.InRange(0, 7.071068 * Step + 0.001));
                }
            }, results, failures);
            Run("停止优先与减速恢复", () =>
            {
                using (var s = HeadOn())
                {
                    s.FirstBall.Motor.SetStopped(true);
                    s.FirstBall.Motor.ApplyImpulse(new PlanarVector(10, 0));
                    s.Advance(Step);
                    Assert.That(s.FirstBall.Motor.Velocity.Length, Is.Zero);
                    s.FirstBall.Motor.SetSpeed(0.5); s.FirstBall.Motor.SetStopped(false);
                    for (int i = 0; i < 8; i++) s.Advance(Step);
                    Assert.That(s.FirstBall.Motor.Velocity.Length, Is.EqualTo(0.5).Within(0.001));
                }
            }, results, failures);
            Run("墙角归并与墙面无伤害", () =>
            {
                using (var s = HeadOn())
                {
                    s.Collisions.SetTestState(new PlanarVector(4.4, 4.4), new PlanarVector(3, 3), new PlanarVector(-3, -3), default);
                    for (int i = 0; i < 10; i++) s.Advance(Step);
                    Assert.That(s.Collisions.WallImpactCount, Is.EqualTo(1));
                    Assert.That(s.FirstBall.Motor.Velocity.X, Is.LessThan(0));
                    Assert.That(s.FirstBall.Motor.Velocity.Y, Is.LessThan(0));
                    Assert.That(s.FirstBall.Combat.CurrentHp, Is.EqualTo(100));
                }
            }, results, failures);
            Run("免疫不取消物理响应", () =>
            {
                using (var s = HeadOn(immune: true))
                {
                    UntilContact(s);
                    Assert.That(s.FirstBall.Combat.CurrentHp, Is.EqualTo(100));
                    Assert.That(s.SecondBall.Combat.CurrentHp, Is.EqualTo(90));
                    Assert.That(s.FirstBall.Motor.Velocity.X, Is.LessThan(0));
                }
            }, results, failures);
            Run("初始穿透不伪造攻击", () =>
            {
                using (var s = HeadOn())
                {
                    s.Collisions.SetTestState(new PlanarVector(-0.25, 0), default, new PlanarVector(0.25, 0), default);
                    s.FirstBall.Motor.SetSpeed(0); s.SecondBall.Motor.SetSpeed(0);
                    for (int i = 0; i < 120; i++) s.Advance(Step);
                    Assert.That(s.Collisions.ContactRound, Is.Zero);
                    Assert.That((s.FirstBall.Motor.Position - s.SecondBall.Motor.Position).Length, Is.GreaterThan(0.98));
                    Assert.That(s.FirstBall.Combat.CurrentHp, Is.EqualTo(100));
                }
            }, results, failures);
            Run("超时末步与边界接触优先", () =>
            {
                using (var s = HeadOn(deadline: 0.02))
                {
                    s.Advance(0.1);
                    Assert.That(s.ElapsedSeconds, Is.EqualTo(0.02).Within(1e-10));
                    Assert.That(s.Settlement.EndReason, Is.EqualTo(BattleEndReason.Timeout));
                    Assert.That(s.Collisions.SimulationStepCount, Is.EqualTo(3));
                }
                using (var s = HeadOn(5, 100, Step / 2))
                {
                    s.Advance(Step);
                    Assert.That(s.Settlement.EndReason, Is.EqualTo(BattleEndReason.Elimination));
                }
            }, results, failures);
            Run("暂停后台倍速与补步预算", () =>
            {
                using (var s = HeadOn())
                {
                    s.SetPaused(true); s.Advance(100);
                    s.SetSuspended(true); s.SetPaused(false); s.Advance(100);
                    Assert.That(s.Collisions.SimulationStepCount, Is.Zero);
                    s.SetSuspended(false); s.SetTimeScale(2); s.Advance(Step);
                    Assert.That(s.Collisions.SimulationStepCount, Is.EqualTo(4));
                    long before = s.Collisions.SimulationStepCount;
                    s.Advance(100);
                    Assert.That(s.Collisions.SimulationStepCount - before, Is.EqualTo(16));
                    Assert.That(s.CatchUpLimitCount, Is.GreaterThan(0));
                }
            }, results, failures);
            Run("独立世界与渲染帧率一致", () =>
            {
                using (var a = HeadOn()) using (var b = HeadOn())
                {
                    var bStart = b.FirstBall.Motor.Position;
                    for (int i = 0; i < 60; i++) a.Advance(1.0 / 30);
                    Assert.That(b.FirstBall.Motor.Position, Is.EqualTo(bStart));
                    for (int i = 0; i < 288; i++) b.Advance(1.0 / 144);
                    Assert.That((a.FirstBall.Motor.Position - b.FirstBall.Motor.Position).Length, Is.LessThan(0.0001));
                    Assert.That(a.FirstBall.Combat.CurrentHp, Is.EqualTo(b.FirstBall.Combat.CurrentHp));
                }
            }, results, failures);
            Run("多种子标准及十倍速度长运行", () =>
            {
                for (int seed = 0; seed < 8; seed++)
                using (var s = new BattleSession(new BattleSettings(seed: seed, speedIndex: seed % 2 == 0 ? 100 : 1000,
                    firstCombat: new CombatDefinition(collisionDamage: 0), secondCombat: new CombatDefinition(collisionDamage: 0))))
                {
                    for (int i = 0; i < 1800; i++)
                    {
                        s.Advance(Step);
                        Assert.That(s.Arena.Bounds.Contains(s.FirstBall.Motor.Position, 0.5, 0.03), Is.True, "首球越界");
                        Assert.That(s.Arena.Bounds.Contains(s.SecondBall.Motor.Position, 0.5, 0.03), Is.True, "次球越界");
                        Assert.That((s.FirstBall.Motor.Position - s.SecondBall.Motor.Position).Length, Is.GreaterThan(0.95));
                    }
                    Assert.That(s.Collisions.SimulationStepCount, Is.EqualTo(3600));
                }
            }, results, failures);
            yield return null;
            yield return null;
            Run("物理场景释放与全局设置保持", () =>
            {
                Assert.That(Physics2D.simulationMode, Is.EqualTo(originalMode));
                Assert.That(SceneManager.sceneCount, Is.EqualTo(sceneCount));
                Assert.That(UnityEngine.Object.FindObjectsOfType<Rigidbody2D>().Length, Is.EqualTo(bodyCount));
            }, results, failures);
            string path = Path.GetFullPath(Path.Combine(Application.dataPath,
                UnityEditor.SessionState.GetString("ProjectBB.ValidationPhase", "") == "summon_ability"
                    ? "../../outputs/summon_ability_validation" : UnityEditor.SessionState.GetString("ProjectBB.ValidationPhase", "") == "projectile_bounce"
                    ? "../../outputs/projectile_bounce_validation" : UnityEditor.SessionState.GetString("ProjectBB.ValidationPhase", "") == "projectile_launch"
                    ? "../../outputs/projectile_launch_validation" : UnityEditor.SessionState.GetString("ProjectBB.ValidationPhase", "") == "config_skill"
                    ? "../../outputs/config_skill_validation" : UnityEditor.SessionState.GetString("ProjectBB.ValidationPhase", "") == "projectile"
                    ? "../../outputs/projectile_validation" : "../../outputs/dynamic_validation/dynamic"));
            Directory.CreateDirectory(path);
            File.WriteAllLines(Path.Combine(path, "physics-scenarios.txt"), results);
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }
    }
}
