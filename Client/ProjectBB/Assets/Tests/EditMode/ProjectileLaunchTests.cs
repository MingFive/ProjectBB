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
    /// <summary>配置生成点、方向和会话随机序列；运行验证沿用真实物理与弹道系统。</summary>
    public sealed class ProjectileLaunchTests
    {
        private const double Step = 1.0 / 60;
        private static string Sample => File.ReadAllText(Path.Combine(Application.dataPath, "Resources/Config/AutoProjectileBattle.json"));
        private static string Set(string json, string field, string literal)
        {
            string pattern = "\"" + field + "\"\\s*:\\s*(\"[^\"]*\"|true|false|[-+0-9.eE]+)";
            Assert.That(Regex.IsMatch(json, pattern), Is.True, "样例缺少测试字段 " + field);
            return Regex.Replace(json, pattern, "\"" + field + "\": " + literal);
        }
        private static string Fixture()
            => Set(Set(Set(Set(Set(Sample, "speedIndex", "0"), "speedCellsPerSecond", "3"), "collisionDamage", "0"),
                "randomDirection", "false"), "bounceOnWall", "false")
                .Replace("\"traitIds\": [\"auto_shot\"]", "\"traitIds\": []");
        private static string Fixed(string anchor, string x, string y, string angle)
            => Set(Set(Set(Set(Set(Fixture(), "spawnAnchor", "\"" + anchor + "\""), "spawnOffsetXCells", x),
                "spawnOffsetYCells", y), "directionMode", "\"fixedAngle\""), "directionAngleDegrees", angle);
        private static BattleSession Create(string json) => new BattleSession(BattleContent.Load(json, "launch-test.json"));
        private static void Fire(BattleSession session, BallCharacter source = null)
            => Assert.That((source ?? session.FirstBall).Skills.TryCast(new SkillCastRequest(session.SessionId, "circle_shot"), out _), Is.True);
        private static void Equal(PlanarVector actual, PlanarVector expected)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(1e-7));
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(1e-7));
        }

        [Test]
        public void LegacyRowsKeepOriginalLaunchDefaults()
        {
            string legacy = Sample;
            foreach (string key in new[] { "spawnAnchor", "spawnOffsetXCells", "spawnOffsetYCells", "directionMode", "directionAngleDegrees", "randomDirection" })
                legacy = Regex.Replace(legacy, ",\\s*\"" + key + "\"\\s*:\\s*(\"[^\"]*\"|true|false|[-+0-9.eE]+)", "");
            var content = BattleContent.Load(legacy, "legacy.json");
            var launch = content.FirstBall.Traits[0].Skill.Launch;
            Assert.That(launch.SpawnAnchor, Is.EqualTo(ProjectileSpawnAnchor.Caster));
            Assert.That(launch.DirectionMode, Is.EqualTo(ProjectileDirectionMode.TowardTarget));
            Assert.That(launch.RandomDirection, Is.False);
            Equal(launch.SpawnOffset, default);
            Assert.That(launch.DirectionAngleDegrees, Is.Zero);
        }

        [TestCase("spawnAnchor", "\"unknown\"")]
        [TestCase("spawnAnchor", "null")]
        [TestCase("directionMode", "\"homing\"")]
        [TestCase("directionMode", "null")]
        [TestCase("spawnOffsetXCells", "11")]
        [TestCase("spawnOffsetYCells", "-11")]
        [TestCase("spawnOffsetXCells", "1e999")]
        [TestCase("spawnOffsetYCells", "\"2\"")]
        [TestCase("directionAngleDegrees", "361")]
        [TestCase("directionAngleDegrees", "-361")]
        [TestCase("directionAngleDegrees", "1e999")]
        [TestCase("randomDirection", "1")]
        [TestCase("randomDirection", "\"true\"")]
        [TestCase("randomDirection", "null")]
        public void NewFieldsRejectInvalidValues(string field, string value)
        {
            var error = Assert.Throws<InvalidDataException>(() => BattleContent.Load(Set(Sample, field, value), "bad-launch.json"));
            Assert.That(error.Message, Does.Contain("bad-launch.json").And.Contain(field));
        }

        [UnityTest]
        public IEnumerator SpawnAnchorsAndFixedDirectionsUseConfiguredWorldCoordinates()
        {
            yield return new EnterPlayMode();
            foreach (string anchor in new[] { "caster", "target", "arenaCenter" })
            using (var session = Create(Fixed(anchor, "0.5", "1", "90")))
            {
                PlanarVector origin = anchor == "caster" ? session.FirstBall.Motor.Position :
                    anchor == "target" ? session.SecondBall.Motor.Position : default;
                Fire(session);
                var expected = origin + new PlanarVector(0.5, 1);
                Equal(session.Projectiles.GetSnapshot(0).Position, expected);
                session.Advance(Step);
                Equal(session.Projectiles.GetSnapshot(0).Position, expected + new PlanarVector(0, 3 * Step));
                // 固定方向是世界方向；蓝方同样朝上，不随阵营翻转。
                session.Projectiles.Clear();
                Fire(session, session.SecondBall);
                PlanarVector start = session.Projectiles.GetSnapshot(0).Position;
                session.Advance(Step);
                Equal(session.Projectiles.GetSnapshot(0).Position - start, new PlanarVector(0, 3 * Step));
            }
            foreach (int angle in new[] { 0, 90, 180, -90, 360 })
            using (var session = Create(Fixed("arenaCenter", "0", "0", angle.ToString())))
            {
                Fire(session); session.Advance(Step);
                double radians = angle * Math.PI / 180;
                Equal(session.Projectiles.GetSnapshot(0).Position, new PlanarVector(Math.Cos(radians), Math.Sin(radians)) * (3 * Step));
            }
            using (var session = Create(Fixed("arenaCenter", "10", "-10", "90")))
            {
                PlanarVector ownerBefore = session.FirstBall.Motor.Position;
                Fire(session);
                Equal(session.Projectiles.GetSnapshot(0).Position, new PlanarVector(4.85, -4.85));
                Equal(session.FirstBall.Motor.Position, ownerBefore);
            }
            LogAssert.NoUnexpectedReceived();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator TargetAndMovementDirectionsAreResolvedAtLaunchOnly()
        {
            yield return new EnterPlayMode();
            using (var session = Create(Set(Set(Fixture(), "spawnAnchor", "\"arenaCenter\""), "spawnOffsetYCells", "1")))
            {
                var expectedDirection = (session.SecondBall.Motor.Position - new PlanarVector(0, 1)).NormalizedOr(default);
                Fire(session);
                session.Advance(Step);
                Equal(session.Projectiles.GetSnapshot(0).Position, new PlanarVector(0, 1) + expectedDirection * (3 * Step));
                // 发射后移动目标不改变已生成弹道的方向。
                session.Collisions.SetTestState(new PlanarVector(-3, -3), default, new PlanarVector(3, 3), default);
                session.Advance(Step);
                Equal(session.Projectiles.GetSnapshot(0).Position, new PlanarVector(0, 1) + expectedDirection * (6 * Step));
            }
            using (var session = Create(Set(Set(Fixture(), "spawnAnchor", "\"arenaCenter\""), "directionMode", "\"casterMovement\"")))
            {
                session.Collisions.SetTestState(new PlanarVector(-3, -3), new PlanarVector(0, 3), new PlanarVector(3, 3), default);
                Fire(session); session.Advance(Step);
                Equal(session.Projectiles.GetSnapshot(0).Position, new PlanarVector(0, 3 * Step));
                session.Projectiles.Clear();
                session.FirstBall.Motor.SetStopped(true); session.Advance(Step);
                PlanarVector fallback = session.FirstBall.Motor.Direction;
                Fire(session); session.Advance(Step);
                Equal(session.Projectiles.GetSnapshot(0).Position, fallback * (3 * Step));
            }
            LogAssert.NoUnexpectedReceived();
            yield return new ExitPlayMode();
        }

        private static PlanarVector RandomShot(BattleSession session)
        {
            Fire(session);
            PlanarVector origin = session.Projectiles.GetSnapshot(0).Position;
            session.Advance(Step);
            PlanarVector velocity = (session.Projectiles.GetSnapshot(0).Position - origin) / Step;
            Assert.That(velocity.Length, Is.EqualTo(3).Within(1e-7));
            session.Advance(Step);
            Equal(session.Projectiles.GetSnapshot(0).Position - origin, velocity * (2 * Step));
            session.Projectiles.Clear();
            return velocity;
        }

        [UnityTest]
        public IEnumerator RandomDirectionOverridesModeAndReplaysWithSessionSeed()
        {
            yield return new EnterPlayMode();
            string json = Set(Fixed("arenaCenter", "0", "0", "90"), "randomDirection", "true");
            using (var first = Create(json))
            using (var replay = Create(json))
            using (var different = Create(Set(json, "seed", "17")))
            using (var nonRandom = Create(Set(json, "randomDirection", "false")))
            {
                Equal(first.FirstBall.Motor.Direction, nonRandom.FirstBall.Motor.Direction);
                Equal(first.SecondBall.Motor.Direction, nonRandom.SecondBall.Motor.Direction);
                // 失败的会话/暂停请求不能推进随机序列。
                first.SetPaused(true);
                Assert.That(first.FirstBall.Skills.TryCast(new SkillCastRequest(first.SessionId, "circle_shot"), out _), Is.False);
                first.SetPaused(false);
                Assert.That(first.FirstBall.Skills.TryCast(new SkillCastRequest(Guid.NewGuid(), "circle_shot"), out _), Is.False);
                PlanarVector prior = default;
                bool changed = false, differentSeed = false, overridesFixed = false;
                int quadrants = 0;
                for (int i = 0; i < 24; i++)
                {
                    PlanarVector velocity = RandomShot(first);
                    Equal(RandomShot(replay), velocity);
                    differentSeed |= (RandomShot(different) - velocity).Length > 1e-5;
                    if (i > 0) changed |= (velocity - prior).Length > 1e-5;
                    overridesFixed |= Math.Abs(velocity.X) > 0.1;
                    quadrants |= 1 << ((velocity.X >= 0 ? 1 : 0) + (velocity.Y >= 0 ? 2 : 0));
                    prior = velocity;
                }
                Assert.That(changed && differentSeed && overridesFixed, Is.True);
                Assert.That(quadrants, Is.EqualTo(15), "固定种子样本覆盖四象限；不是仅随机符号或单一方向。");
                Fire(nonRandom); nonRandom.Advance(Step);
                Equal(nonRandom.Projectiles.GetSnapshot(0).Position, new PlanarVector(0, 3 * Step));
            }
            LogAssert.NoUnexpectedReceived();
            yield return new ExitPlayMode();
        }
    }
}
