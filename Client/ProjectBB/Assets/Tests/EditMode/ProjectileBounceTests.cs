using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ProjectBB.GamePlay;
using ProjectBB.LogicCore;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectBB.Tests
{
    /// <summary>墙反弹的配置、分段路径、承伤与生命周期；由用户在 Unity 中执行。</summary>
    public sealed class ProjectileBounceTests
    {
        private const double Step = 1.0 / 60;
        private static string Sample => File.ReadAllText(Path.Combine(Application.dataPath, "Resources/Config/AutoProjectileBattle.json"));
        private static string Set(string json, string field, string value)
            => Regex.Replace(json, "\"" + field + "\"\\s*:\\s*(\"[^\"]*\"|true|false|[-+0-9.eE]+)", "\"" + field + "\": " + value);
        private static BattleSession Create(bool immune = false, double deadline = 180, int hp = 100)
        {
            var session = new BattleSession(new BattleSettings(speedIndex: 0,
                firstCombat: new CombatDefinition(hp, 0), secondCombat: new CombatDefinition(hp, 0, immune), maxBattleSeconds: deadline));
            session.Collisions.SetTestState(new PlanarVector(-3, -3), default, new PlanarVector(2, 0), default);
            return session;
        }
        private static long Fire(BattleSession session, PlanarVector start, PlanarVector velocity,
            bool bounce = true, double lifetime = 2, double radius = 0.1)
        {
            Assert.That(session.TryLaunchProjectile(new CircleProjectileRequest(session.SessionId, 1, start, velocity,
                radius, lifetime, 10, bounce), out long id), Is.True);
            return id;
        }
        private static void Equal(PlanarVector actual, PlanarVector expected)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(1e-7));
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(1e-7));
        }
        // 独立的展开空间折叠参考，不复用生产代码的逐次墙碰撞算法。
        private static double Fold(double position, double distance, double limit)
        {
            double period = 4 * limit;
            double phase = ((position + distance + limit) % period + period) % period;
            return phase <= 2 * limit ? phase - limit : 3 * limit - phase;
        }

        [Test]
        public void WallBounceDefaultsToFalseAndAcceptsExplicitBoolean()
        {
            string legacy = Regex.Replace(Sample, ",\\s*\"bounceOnWall\"\\s*:\\s*(true|false)", "");
            Assert.That(BattleContent.Load(legacy, "legacy.json").FirstBall.Traits[0].Skill.BounceOnWall, Is.False);
            foreach (bool enabled in new[] { false, true })
                Assert.That(BattleContent.Load(Set(Sample, "bounceOnWall", enabled ? "true" : "false"), "bounce.json")
                    .FirstBall.Traits[0].Skill.BounceOnWall, Is.EqualTo(enabled));
        }

        [TestCase("null")]
        [TestCase("1")]
        [TestCase("\"true\"")]
        [TestCase("{}")]
        public void InvalidWallBounceValueReportsField(string literal)
        {
            var error = Assert.Throws<InvalidDataException>(() => BattleContent.Load(Set(Sample, "bounceOnWall", literal), "bad-bounce.json"));
            Assert.That(error.Message, Does.Contain("bad-bounce.json").And.Contain("bounceOnWall"));
        }

        [UnityTest]
        public IEnumerator FourWallsCornersAndMultipleBouncesPreserveTravel()
        {
            yield return new EnterPlayMode();
            foreach (var direction in new[] { new PlanarVector(1, 0), new PlanarVector(-1, 0), new PlanarVector(0, 1), new PlanarVector(0, -1) })
            using (var session = Create())
            {
                PlanarVector start = direction * 4.8 + (direction.X == 0 ? new PlanarVector(3, 0) : new PlanarVector(0, 3));
                Fire(session, start, direction * 60);
                session.Advance(Step);
                Equal(session.Projectiles.GetSnapshot(0).Position, start - direction * 0.8);
                Assert.That(session.Projectiles.WallBounceCount, Is.EqualTo(1));
                session.Advance(Step);
                Equal(session.Projectiles.GetSnapshot(0).Position, start - direction * 1.8);
            }
            using (var session = Create())
            {
                Fire(session, new PlanarVector(4.8, 4.8), new PlanarVector(60, 60));
                session.Advance(Step);
                Equal(session.Projectiles.GetSnapshot(0).Position, new PlanarVector(4, 4));
                Assert.That(session.Projectiles.WallBounceCount, Is.EqualTo(1), "墙角同时命中两面只算一次反弹。");
            }
            foreach (int sign in new[] { -1, 1 })
            using (var session = Create())
            {
                Fire(session, new PlanarVector(4.9, 3), new PlanarVector(sign * 60, 0));
                session.Advance(Step);
                Equal(session.Projectiles.GetSnapshot(0).Position, new PlanarVector(3.9, 3));
                Assert.That(session.Projectiles.WallBounceCount, Is.EqualTo(sign == 1 ? 1 : 0));
            }
            using (var session = Create())
            {
                Fire(session, new PlanarVector(0, 3), new PlanarVector(10000, 0));
                for (int i = 1; i <= 20; i++)
                {
                    session.Advance(Step);
                    Equal(session.Projectiles.GetSnapshot(0).Position, new PlanarVector(Fold(0, 10000 * Step * i, 4.9), 3));
                }
                Assert.That(session.Projectiles.WallBounceCount, Is.GreaterThan(300));
                Assert.That(session.Projectiles.WallBounceBudgetExceededCount, Is.Zero);
            }
            LogAssert.NoUnexpectedReceived();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ReflectedPathsHitOnceAndRespectLifetimeAndMovingTargetTime()
        {
            yield return new EnterPlayMode();
            foreach (bool immune in new[] { false, true })
            using (var session = Create(immune))
            {
                long id = Fire(session, new PlanarVector(4, 0), new PlanarVector(240, 0));
                session.Advance(Step);
                Assert.That(session.Projectiles.WallBounceCount, Is.EqualTo(1));
                Assert.That(session.Projectiles.ActiveCount, Is.Zero);
                Assert.That(session.SecondBall.Combat.CurrentHp, Is.EqualTo(immune ? 100 : 90));
                Assert.That(session.TryDequeueProjectileFeedback(out var feedback), Is.True);
                Assert.That(feedback.Damage.Request.AttackId, Is.EqualTo(id));
                Assert.That(feedback.Damage.Request.SourceKind, Is.EqualTo(DamageSourceKind.Projectile));
                Assert.That(feedback.Damage.WasImmune, Is.EqualTo(immune));
                session.Advance(Step);
                Assert.That(session.Projectiles.HitCount, Is.EqualTo(1));
            }
            using (var session = Create())
            {
                Fire(session, new PlanarVector(4, 0), new PlanarVector(240, 0), lifetime: 0.005);
                session.Advance(Step);
                Assert.That(session.Projectiles.WallBounceCount, Is.EqualTo(1));
                Assert.That(session.Projectiles.ActiveCount, Is.Zero);
                Assert.That(session.SecondBall.Combat.CurrentHp, Is.EqualTo(100));
                // 回收槽位不沿用旧反弹速度；关闭开关仍碰墙消失。
                Fire(session, new PlanarVector(4, 0), new PlanarVector(240, 0), bounce: false);
                session.Advance(Step);
                Assert.That(session.Projectiles.ActiveCount, Is.Zero);
                Assert.That(session.Projectiles.WallBounceCount, Is.EqualTo(1));
            }
            using (var session = Create())
            {
                Fire(session, default, new PlanarVector(240, 0));
                session.Advance(Step);
                Assert.That(session.Projectiles.HitCount, Is.EqualTo(1), "墙前的目标先命中。");
                Assert.That(session.Projectiles.WallBounceCount, Is.Zero);
            }
            foreach (bool bounce in new[] { false, true })
            using (var session = Create())
            {
                // 使用精确二进制数构造墙与目标同刻：墙先处理，反弹后的下一段才允许重叠命中。
                session.Collisions.SetTestState(new PlanarVector(-3, -3), default, new PlanarVector(4.5, 1), default);
                Fire(session, new PlanarVector(4.25, 0), new PlanarVector(8, 0), bounce: bounce, radius: 0.5);
                session.Projectiles.SweepSegment(0.03125, session.FirstBall.Motor.Position, session.SecondBall.Motor.Position);
                session.Projectiles.Resolve(0.03125);
                Assert.That(session.SecondBall.Combat.CurrentHp, Is.EqualTo(100));
                Assert.That(session.Projectiles.ActiveCount, Is.EqualTo(bounce ? 1 : 0));
                if (bounce)
                {
                    session.Projectiles.SweepSegment(0.01, session.FirstBall.Motor.Position, session.SecondBall.Motor.Position);
                    session.Projectiles.Resolve(0.04125);
                    Assert.That(session.SecondBall.Combat.CurrentHp, Is.EqualTo(90));
                }
            }
            // 为查询器提供已知线性目标片段，专门捕获反弹后错误重置目标片段时间的误报。
            foreach (bool shouldHit in new[] { false, true })
            using (var session = Create())
            {
                var start = new PlanarVector(2, shouldHit ? -2 : -1);
                session.Collisions.SetTestState(new PlanarVector(-3, -3), default, start + new PlanarVector(0, 2), default);
                Fire(session, new PlanarVector(4, 0), new PlanarVector(10, 0));
                session.Projectiles.SweepSegment(0.4, session.FirstBall.Motor.Position, start);
                session.Projectiles.Resolve(0.4);
                Assert.That(session.SecondBall.Combat.CurrentHp, Is.EqualTo(shouldHit ? 90 : 100));
            }
            LogAssert.NoUnexpectedReceived();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ConfiguredBounceUsesSessionClockAndCleansUp()
        {
            yield return new EnterPlayMode();
            string json = Set(Set(Set(Set(Set(Set(Sample, "randomDirection", "false"), "bounceOnWall", "true"),
                "spawnAnchor", "\"arenaCenter\""), "spawnOffsetYCells", "3"), "directionMode", "\"fixedAngle\""), "directionAngleDegrees", "0")
                .Replace("\"traitIds\": [\"auto_shot\"]", "\"traitIds\": []");
            using (var session = new BattleSession(BattleContent.Load(json, "configured-bounce.json")))
            {
                session.FirstBall.Motor.SetStopped(true); session.SecondBall.Motor.SetStopped(true);
                Assert.That(session.FirstBall.Skills.TryCast(new SkillCastRequest(session.SessionId, "circle_shot"), out _), Is.True);
                for (int i = 0; i < 30; i++) session.Advance(Step);
                Equal(session.Projectiles.GetSnapshot(0).Position, new PlanarVector(3.7, 3));
                Assert.That(session.Projectiles.WallBounceCount, Is.EqualTo(1));
                session.SetPaused(true); session.Advance(10);
                session.SetSuspended(true); session.SetPaused(false); session.Advance(10);
                Equal(session.Projectiles.GetSnapshot(0).Position, new PlanarVector(3.7, 3));
                session.SetSuspended(false); session.SetTimeScale(2); session.Advance(Step);
                Equal(session.Projectiles.GetSnapshot(0).Position, new PlanarVector(3.3, 3));
            }
            using (var a = Create()) using (var b = Create())
            {
                Fire(a, new PlanarVector(0, 3), new PlanarVector(80, 0));
                Fire(b, new PlanarVector(0, 3), new PlanarVector(80, 0));
                for (int i = 0; i < 15; i++) a.Advance(1.0 / 30);
                for (int i = 0; i < 72; i++) b.Advance(1.0 / 144);
                Equal(a.Projectiles.GetSnapshot(0).Position, b.Projectiles.GetSnapshot(0).Position);
                Assert.That(a.Projectiles.WallBounceCount, Is.EqualTo(b.Projectiles.WallBounceCount));
            }
            using (var session = Create(deadline: 0.02))
            {
                Fire(session, new PlanarVector(4.8, 3), new PlanarVector(60, 0));
                session.Advance(Step); session.Advance(Step);
                Assert.That(session.IsFinished, Is.True);
                Assert.That(session.Projectiles.ActiveCount, Is.Zero);
            }
            using (var session = Create(hp: 5))
            {
                Fire(session, new PlanarVector(4, 0), new PlanarVector(240, 0));
                Fire(session, new PlanarVector(0, 3), new PlanarVector(60, 0));
                session.Advance(Step);
                Assert.That(session.IsFinished, Is.True);
                Assert.That(session.Projectiles.ActiveCount, Is.Zero);
            }
            var old = Create();
            var request = new CircleProjectileRequest(old.SessionId, 1, new PlanarVector(0, 3), new PlanarVector(60, 0), bounceOnWall: true);
            Assert.That(old.TryLaunchProjectile(request, out _), Is.True);
            old.Dispose();
            Assert.That(old.Projectiles.ActiveCount, Is.Zero);
            using (var next = Create())
            {
                Assert.That(next.TryLaunchProjectile(request, out _), Is.False);
                Assert.That(next.Projectiles.WallBounceCount, Is.Zero);
            }
            LogAssert.NoUnexpectedReceived();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ExtremeNarrowArenaRetiresOnlyOverBudgetProjectile()
        {
            yield return new EnterPlayMode();
            using (var session = new BattleSession(new BattleSettings(width: 0.3, radius: 0.01, speedIndex: 0,
                firstCombat: new CombatDefinition(collisionDamage: 0), secondCombat: new CombatDefinition(collisionDamage: 0))))
            {
                session.Collisions.SetTestState(new PlanarVector(0, -3), default, new PlanarVector(0, 3), default);
                Fire(session, default, new PlanarVector(10000, 0), radius: 0.149);
                Fire(session, default, new PlanarVector(0, 1), radius: 0.01);
                session.Advance(Step);
                Assert.That(session.Projectiles.WallBounceCount, Is.EqualTo(CircleProjectileSystem.MaxWallBouncesPerSegment));
                Assert.That(session.Projectiles.WallBounceBudgetExceededCount, Is.EqualTo(1));
                Assert.That(session.Projectiles.RecycledCount, Is.EqualTo(1));
                Assert.That(session.Projectiles.ActiveCount, Is.EqualTo(1));
                Equal(session.Projectiles.GetSnapshot(0).Position, new PlanarVector(0, Step));
                Assert.That(session.Projectiles.HitCount, Is.Zero);
            }
            LogAssert.NoUnexpectedReceived();
            yield return new ExitPlayMode();
        }
    }
}
