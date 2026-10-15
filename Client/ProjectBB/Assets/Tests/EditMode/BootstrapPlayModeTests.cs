using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ProjectBB.GamePlay;
using ProjectBB.LogicCore;
using ProjectBB.SceneInit;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectBB.Tests
{
    /// <summary>进入真实 Play Mode 验证当前 Bootstrap 的推进、暂停、重开与双方同死。</summary>
    public sealed class BootstrapPlayModeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static BattleSession Session(ProjectBootstrap bootstrap)
            => (BattleSession)typeof(ProjectBootstrap).GetField("_session", Private).GetValue(bootstrap);
        private static void Set(ProjectBootstrap bootstrap, string field, object value)
            => typeof(ProjectBootstrap).GetField(field, Private).SetValue(bootstrap, value);
        private static void Restart(ProjectBootstrap bootstrap)
            => typeof(ProjectBootstrap).GetMethod("RestartBattle", Private).Invoke(bootstrap, null);

        [UnityTest]
        public IEnumerator ProjectileViewsAndRepeatedRestartCleanUp()
        {
            yield return new EnterPlayMode();
            var bootstrap = Object.FindObjectOfType<ProjectBootstrap>();
            Assert.That(bootstrap, Is.Not.Null);
            for (int iteration = 0; iteration < 3; iteration++)
            {
                Restart(bootstrap);
                BattleSession old = Session(bootstrap);
                old.SetSuspended(false);
                var request = new CircleProjectileRequest(old.SessionId, old.FirstBall.EntityId, default, new PlanarVector(0, 1));
                for (int i = 0; i < 8; i++) Assert.That(old.TryLaunchProjectile(request, out _), Is.True);
                old.SetPaused(true);
                object view = typeof(ProjectBootstrap).GetField("_view", Private).GetValue(bootstrap);
                view.GetType().GetMethod("Render").Invoke(view, null);
                Transform root = null;
                // Destroy 延迟至帧末；同帧重开后仅选择本次启用的表现根节点。
                foreach (Transform child in bootstrap.transform)
                    if (child.name == "BattleView" && child.gameObject.activeSelf) root = child;
                Assert.That(root, Is.Not.Null);
                int flights = 0;
                foreach (Transform child in root) if (child.name == "CircleProjectile" && child.gameObject.activeSelf) flights++;
                Assert.That(flights, Is.EqualTo(8));
                Restart(bootstrap);
                BattleSession next = Session(bootstrap);
                Assert.That(old.IsDisposed, Is.True);
                Assert.That(old.Projectiles.ActiveCount, Is.Zero);
                Assert.That(old.TryDequeueProjectileFeedback(out _), Is.False);
                Assert.That(next.Projectiles.ActiveCount, Is.Zero);
                Assert.That(next.TryLaunchProjectile(request, out _), Is.False);
                // EditMode 测试轮询可能快于限帧后的真实渲染帧；Destroy 必须等实际帧末，不按协程轮询次数猜测。
                int requiredFrame = Time.frameCount + 2;
                float deadline = Time.realtimeSinceStartup + 5;
                while (Time.frameCount < requiredFrame && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(Time.frameCount, Is.GreaterThanOrEqualTo(requiredFrame), "等待真实渲染帧超时。");
                Assert.That(root == null, Is.True, "重开必须销毁旧表现池。");
                int activeRoots = 0;
                foreach (Transform child in bootstrap.transform) if (child.name == "BattleView") activeRoots++;
                Assert.That(activeRoots, Is.EqualTo(1));
            }
            LogAssert.NoUnexpectedReceived();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ActualBootstrapPauseRestartAndMutualDeath()
        {
            yield return new EnterPlayMode();
            var bootstrap = Object.FindObjectOfType<ProjectBootstrap>();
            Assert.That(bootstrap, Is.Not.Null, "请在 Bootstrap 场景执行集成验证。");
            // 仅修改 Play Mode 实例，退出后恢复用户的编辑态场景与参数。
            Set(bootstrap, "_usePrototypeSettings", true);
            Set(bootstrap, "_firstMaxHp", 100);
            Set(bootstrap, "_secondMaxHp", 100);
            Set(bootstrap, "_firstCollisionDamage", 10);
            Set(bootstrap, "_secondCollisionDamage", 10);
            Set(bootstrap, "_firstDamageImmune", false);
            Set(bootstrap, "_secondDamageImmune", false);
            Set(bootstrap, "_maxBattleSeconds", 180f);
            Restart(bootstrap);
            BattleSession session = Session(bootstrap);
            session.SetSuspended(false);
            for (int i = 0; i < 10000 && session.FirstBall.Combat.CurrentHp == 100; i++) session.Advance(1.0 / 60);
            Assert.That(session.FirstBall.Combat.CurrentHp, Is.EqualTo(90));
            Assert.That(session.SecondBall.Combat.CurrentHp, Is.EqualTo(90));
            session.SetPaused(true);
            double elapsed = session.ElapsedSeconds;
            var position = session.FirstBall.Motor.Position;
            yield return null;
            yield return null;
            session.Advance(10);
            Assert.That(session.ElapsedSeconds, Is.EqualTo(elapsed));
            Assert.That(session.FirstBall.Motor.Position, Is.EqualTo(position));
            Restart(bootstrap);
            BattleSession restarted = Session(bootstrap);
            Assert.That(session.IsDisposed, Is.True);
            Assert.That(restarted.SessionId, Is.Not.EqualTo(session.SessionId));
            Assert.That(restarted.FirstBall.Combat.CurrentHp, Is.EqualTo(100));
            Set(bootstrap, "_firstMaxHp", 5);
            Set(bootstrap, "_secondMaxHp", 5);
            Restart(bootstrap);
            session = Session(bootstrap);
            session.SetSuspended(false);
            for (int i = 0; i < 10000 && !session.IsFinished; i++) session.Advance(1.0 / 60);
            Assert.That(session.Settlement.Outcome, Is.EqualTo(BattleOutcome.Draw));
            Assert.That(session.Settlement.EndReason, Is.EqualTo(BattleEndReason.Elimination));
            Assert.That(session.FirstBall.Combat.IsDead && session.SecondBall.Combat.IsDead, Is.True);
            yield return null;
            LogAssert.NoUnexpectedReceived();
            yield return new ExitPlayMode();
        }
    }
}
