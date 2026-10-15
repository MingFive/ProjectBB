using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using ProjectBB.GamePlay;
using ProjectBB.LogicCore;
using ProjectBB.SceneInit;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectBB.Tests
{
    /// <summary>实际 JSON 契约与配置技能集成；物理、时钟和重开在 Unity Play Mode 中验证。</summary>
    public sealed class ConfiguredSkillTests
    {
        private const double Step = 1.0 / 60;
        // 契约测试固定输入，避免制作样例的合法调参破坏测试替换与断言。
        private static string Sample => File.ReadAllText(Path.Combine(Application.dataPath, "Tests/EditMode/Fixtures/AutoProjectileBaseline.json"));
        private static string Fixture(int period = 100, double speed = 480, int lifetime = 2000, int damage = 7, bool immediate = false)
            => Sample.Replace("\"speedIndex\": 100", "\"speedIndex\": 0")
                .Replace("\"randomDirection\": true", "\"randomDirection\": false")
                .Replace("\"bounceOnWall\": true", "\"bounceOnWall\": false")
                .Replace("\"speedCellsPerSecond\": 12", "\"speedCellsPerSecond\": " + speed.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Replace("\"lifetimeMs\": 2000", "\"lifetimeMs\": " + lifetime)
                .Replace("\"periodMs\": 1000", "\"periodMs\": " + period)
                .Replace("\"damage\": 10", "\"damage\": " + damage)
                .Replace("\"fireOnStart\": false", "\"fireOnStart\": " + (immediate ? "true" : "false"))
                .Replace("\"collisionDamage\": 10", "\"collisionDamage\": 0")
                .Replace("\"id\": \"blue\", \"maxHp\": 100, \"collisionDamage\": 0, \"damageImmune\": false, \"traitIds\": [\"auto_shot\"]",
                    "\"id\": \"blue\", \"maxHp\": 100, \"collisionDamage\": 0, \"damageImmune\": false, \"traitIds\": []");
        private static BattleSession Create(string json)
        {
            var session = new BattleSession(BattleContent.Load(json, "test.json"));
            session.Collisions.SetTestState(new PlanarVector(-2, 0), default, new PlanarVector(2, 0), default);
            return session;
        }
        private static void Steps(BattleSession session, int count)
        { for (int i = 0; i < count; i++) session.Advance(Step); }

        [Test]
        public void DeliveredConfigurationHasResolvedImmutableDefinitions()
        {
            BattleContent content = BattleContent.Load(Sample, "sample.json");
            Assert.That(content.FirstBall.Traits[0], Is.SameAs(content.SecondBall.Traits[0]));
            Assert.That(content.FirstBall.Traits[0].Period, Is.EqualTo(1));
            Assert.That(content.TryGetSkill("circle_shot", out var skill), Is.True);
            Assert.That(skill.Speed, Is.EqualTo(12));
            Assert.That(skill.Radius, Is.EqualTo(0.15));
            Assert.That(skill.Lifetime, Is.EqualTo(2));
            Assert.That(skill.Damage, Is.EqualTo(10));
            Assert.Throws<NotSupportedException>(() => ((IList<PeriodicTraitDefinition>)content.FirstBall.Traits).Clear());
        }

        [TestCase("\"schemaVersion\": 1", "\"schemaVersion\": 2", "schemaVersion")]
        [TestCase("\"periodMs\": 1000", "\"periodMs\": 0", "periodMs")]
        [TestCase("\"periodMs\": 1000", "\"periodMs\": 1", "periodMs")]
        [TestCase("\"periodMs\": 1000", "\"periodMs\": 10.5", "periodMs")]
        [TestCase("\"periodMs\": 1000", "\"periodMs\": \"1000\"", "periodMs")]
        [TestCase("\"periodMs\": 1000", "\"periodMs\": 2147483648", "periodMs")]
        [TestCase("\"radiusCells\": 0.15", "\"radiusCells\": 0", "radiusCells")]
        [TestCase("\"radiusCells\": 0.15", "\"radiusCells\": 5", "radiusCells")]
        [TestCase("\"speedCellsPerSecond\": 12", "\"speedCellsPerSecond\": 10001", "speedCellsPerSecond")]
        [TestCase("\"speedCellsPerSecond\": 12", "\"speedCellsPerSecond\": 1e-13", "speedCellsPerSecond")]
        [TestCase("\"speedCellsPerSecond\": 12", "\"speedCellsPerSecond\": 1e999", "speedCellsPerSecond")]
        [TestCase("\"lifetimeMs\": 2000", "\"lifetimeMs\": -1", "lifetimeMs")]
        [TestCase("\"lifetimeMs\": 2000", "\"lifetimeMs\": 60001", "lifetimeMs")]
        [TestCase("\"damage\": 10", "\"damage\": -1", "damage")]
        [TestCase("\"damage\": 10", "\"damage\": 10, \"damage\": 11", "damage")]
        [TestCase("\"damage\": 10", "\"damage\": 10, \"typo\": 11", "typo")]
        [TestCase("\"skillId\": \"circle_shot\"", "\"skillId\": \"missing\"", "skillId")]
        [TestCase("\"traitIds\": [\"auto_shot\"]", "\"traitIds\": [\"missing\"]", "traitIds")]
        [TestCase("\"traitIds\": [\"auto_shot\"]", "\"traitIds\": [\"auto_shot\", \"auto_shot\"]", "traitIds")]
        [TestCase("\"firstBallId\": \"red\"", "\"firstBallId\": \"missing\"", "firstBallId")]
        [TestCase("\"id\": \"blue\"", "\"id\": \"red\"", "balls[red]")]
        [TestCase("\"fireOnStart\": false", "\"fireOnStart\": null", "fireOnStart")]
        [TestCase("\"periodMs\": 1000,", "", "periodMs")]
        [TestCase("\"trigger\": \"periodic\"", "\"trigger\": \"onHit\"", "trigger")]
        [TestCase("\"kind\": \"circleProjectile\"", "\"kind\": \"homing\"", "kind")]
        [TestCase("\"ballRadiusCells\": 0.5", "\"ballRadiusCells\": 3", "ballRadiusCells")]
        public void InvalidConfigurationFailsWithFileAndField(string oldValue, string newValue, string field)
        {
            var error = Assert.Throws<InvalidDataException>(() => BattleContent.Load(Sample.Replace(oldValue, newValue), "invalid.json"));
            Assert.That(error.Message, Does.Contain("invalid.json"));
            Assert.That(error.Message, Does.Contain(field));
        }

        [Test]
        public void DuplicateSkillTraitIdsAndInvalidDocumentsAreRejected()
        {
            string sample = Sample;
            foreach (string table in new[] { "skills", "traits" })
            {
                int start = sample.IndexOf('{', sample.IndexOf('[' , sample.IndexOf("\"" + table + "\"", StringComparison.Ordinal)));
                int end = sample.IndexOf('}', start);
                string row = sample.Substring(start, end - start + 1);
                Assert.That(Assert.Throws<InvalidDataException>(() => BattleContent.Load(sample.Insert(end + 1, "," + row), "duplicate.json")).Message,
                    Does.Contain("重复 ID"));
            }
            foreach (string json in new[] { "", "null", "[]", "{", sample + " {}" })
                Assert.Throws<InvalidDataException>(() => BattleContent.Load(json, "invalid.json"));
        }

        private static void Run(string name, Action action, List<string> report, List<string> failures)
        {
            try { action(); report.Add("PASS " + name); }
            catch (Exception exception) { report.Add("FAIL " + name + "\n" + exception); failures.Add(name + ": " + exception.Message); }
        }

        [UnityTest]
        public IEnumerator ConfiguredTraitsUseRealBattleClockAndDamagePipeline()
        {
            yield return new EnterPlayMode();
            yield return VerifyScenarios();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyScenarios()
        {
            int scenes = SceneManager.sceneCount;
            int bodies = UnityEngine.Object.FindObjectsOfType<Rigidbody2D>().Length;
            var report = new List<string>();
            var failures = new List<string>();
            Run("周期边界、下一子步飞行、统一承伤和技能身份", () =>
            {
                using (var session = Create(Fixture()))
                {
                    Steps(session, 5);
                    Assert.That(session.FirstBall.Traits.TriggerCount, Is.Zero);
                    Steps(session, 1);
                    Assert.That(session.FirstBall.Traits.SuccessfulCastCount, Is.EqualTo(1));
                    Assert.That(session.SecondBall.Combat.CurrentHp, Is.EqualTo(100));
                    Assert.That(session.Projectiles.ActiveCount, Is.EqualTo(1));
                    Steps(session, 1);
                    Assert.That(session.SecondBall.Combat.CurrentHp, Is.EqualTo(93));
                    Assert.That(session.TryDequeueProjectileFeedback(out var hit), Is.True);
                    Assert.That(hit.Damage.Request.SourceKind, Is.EqualTo(DamageSourceKind.Projectile));
                    Assert.That(hit.Damage.HpLoss, Is.EqualTo(7));
                    Assert.That(session.FirstBall.Combat.CurrentHp, Is.EqualTo(100));
                    Assert.That(session.SecondBall.Traits.TriggerCount, Is.Zero);
                }
            }, report, failures);
            Run("暂停、后台、倍速和拒绝旧身份/错误技能", () =>
            {
                using (var session = Create(Fixture(period: 1000)))
                {
                    Steps(session, 30);
                    session.SetPaused(true); session.Advance(30);
                    Assert.That(session.ElapsedSeconds, Is.EqualTo(0.5).Within(1e-9));
                    Assert.That(session.FirstBall.Skills.TryCast(new SkillCastRequest(session.SessionId, "circle_shot"), out _), Is.False);
                    session.SetPaused(false); session.SetSuspended(true); session.Advance(30);
                    Assert.That(session.FirstBall.Traits.TriggerCount, Is.Zero);
                    session.SetSuspended(false); session.SetTimeScale(2); Steps(session, 15);
                    Assert.That(session.FirstBall.Traits.TriggerCount, Is.EqualTo(1));
                    Assert.That(session.FirstBall.Skills.TryCast(new SkillCastRequest(Guid.NewGuid(), "circle_shot"), out _), Is.False);
                    Assert.That(session.FirstBall.Skills.TryCast(new SkillCastRequest(session.SessionId, "missing"), out _), Is.False);
                }
            }, report, failures);
            Run("开战即发射只在实际推进发生；移除与配置快照隔离", () =>
            {
                BattleContent content = BattleContent.Load(Fixture(period: 200, damage: 0, immediate: true), "shared.json");
                using (var first = new BattleSession(content))
                using (var second = new BattleSession(content))
                {
                    first.SetPaused(true); first.Advance(1); Assert.That(first.FirstBall.Traits.TriggerCount, Is.Zero);
                    first.SetPaused(false); Steps(first, 60);
                    Assert.That(first.FirstBall.Traits.TriggerCount, Is.EqualTo(6));
                    Assert.That(second.FirstBall.Traits.TriggerCount, Is.Zero);
                    Assert.That(first.FirstBall.Traits.Remove("auto_shot"), Is.True);
                    Assert.That(first.FirstBall.Traits.Remove("auto_shot"), Is.False);
                    Steps(first, 60); Assert.That(first.FirstBall.Traits.TriggerCount, Is.EqualTo(6));
                    Steps(second, 1); Assert.That(second.FirstBall.Traits.TriggerCount, Is.EqualTo(1));
                    Assert.That(content.FirstBall.Traits.Count, Is.EqualTo(1));
                }
            }, report, failures);
            Run("速度、半径、寿命来自配置且重新加载不改旧快照", () =>
            {
                string json = Fixture(period: 1000, speed: 3, lifetime: 50, immediate: true).Replace("\"radiusCells\": 0.15", "\"radiusCells\": 0.3");
                using (var session = Create(json))
                {
                    Steps(session, 1);
                    var flight = session.Projectiles.GetSnapshot(0);
                    Assert.That(flight.Radius, Is.EqualTo(0.3));
                    Assert.That(flight.Position.X, Is.EqualTo(-1.95).Within(1e-6));
                    BattleContent changed = BattleContent.Load(json.Replace("\"damage\": 7", "\"damage\": 19"), "changed.json");
                    Assert.That(changed.FirstBall.Traits[0].Skill.Damage, Is.EqualTo(19));
                    Assert.That(session.Content.FirstBall.Traits[0].Skill.Damage, Is.EqualTo(7));
                    Steps(session, 2);
                    Assert.That(session.Projectiles.ActiveCount, Is.Zero);
                    Assert.That(session.SecondBall.Combat.CurrentHp, Is.EqualTo(100));
                }
            }, report, failures);
            Run("子步量化无周期漂移；30/144 FPS 同时钟触发一致", () =>
            {
                foreach (int fps in new[] { 30, 144 })
                using (var session = Create(Fixture(period: 73, damage: 0)))
                {
                    for (int i = 0; i < fps * 3; i++) session.Advance(1.0 / fps);
                    Assert.That(session.ElapsedSeconds, Is.EqualTo(3).Within(1e-9));
                    Assert.That(session.FirstBall.Traits.TriggerCount, Is.EqualTo(41));
                    Assert.That(session.Projectiles.HitCount, Is.EqualTo(40));
                }
            }, report, failures);
            Run("结束、死亡、超时边界停止 Trait 并回收在途实例", () =>
            {
                using (var session = Create(Fixture(damage: 100)))
                {
                    Steps(session, 7);
                    Assert.That(session.IsFinished, Is.True);
                    Assert.That(session.FirstBall.Traits.IsStopped && session.SecondBall.Traits.IsStopped, Is.True);
                    Assert.That(session.Projectiles.ActiveCount, Is.Zero);
                    Steps(session, 100); Assert.That(session.FirstBall.Traits.TriggerCount, Is.EqualTo(1));
                    Assert.That(session.SecondBall.Combat.DeathCommitCount, Is.EqualTo(1));
                }
                using (var session = Create(Fixture().Replace("\"maxBattleMs\": 180000", "\"maxBattleMs\": 100")))
                {
                    Steps(session, 6);
                    Assert.That(session.IsFinished, Is.True);
                    Assert.That(session.FirstBall.Traits.TriggerCount, Is.Zero, "截止边界不新增技能。");
                }
                using (var session = Create(Fixture(period: 10).Replace("\"maxHp\": 100", "\"maxHp\": 5").Replace("\"collisionDamage\": 0", "\"collisionDamage\": 10")))
                {
                    session.Collisions.SetTestState(new PlanarVector(-0.51, 0), new PlanarVector(100, 0), new PlanarVector(0.51, 0), new PlanarVector(-100, 0));
                    Steps(session, 1);
                    Assert.That(session.Settlement.Outcome, Is.EqualTo(BattleOutcome.Draw));
                    Assert.That(session.FirstBall.Traits.TriggerCount, Is.Zero);
                }
            }, report, failures);
            Run("满池拒绝消耗本轮、不补发，发射位置在场内", () =>
            {
                using (var session = Create(Fixture(period: 100, speed: 0.01)))
                {
                    var request = new SkillCastRequest(session.SessionId, "circle_shot");
                    for (int i = 0; i < CircleProjectileSystem.Capacity; i++) Assert.That(session.FirstBall.Skills.TryCast(request, out _), Is.True);
                    Steps(session, 6);
                    Assert.That(session.FirstBall.Traits.RejectedCastCount, Is.EqualTo(1));
                    Assert.That(session.Projectiles.CapacityRejectedCount, Is.EqualTo(1));
                    session.Projectiles.Clear(); Steps(session, 1);
                    Assert.That(session.Projectiles.ActiveCount, Is.Zero);
                    Steps(session, 5); Assert.That(session.FirstBall.Traits.SuccessfulCastCount, Is.EqualTo(1));
                }
                using (var session = Create(Fixture().Replace("\"radiusCells\": 0.15", "\"radiusCells\": 1")))
                {
                    session.Collisions.SetTestState(new PlanarVector(-4.5, 4.5), default, new PlanarVector(2, 0), default);
                    Assert.That(session.FirstBall.Skills.TryCast(new SkillCastRequest(session.SessionId, "circle_shot"), out _), Is.True);
                    var flight = session.Projectiles.GetSnapshot(0);
                    Assert.That(session.Arena.Bounds.Contains(flight.Position, flight.Radius), Is.True);
                    Assert.That(session.FirstBall.Motor.Position.X, Is.EqualTo(-4.5));
                }
            }, report, failures);
            Run("共享配置三轮重开：新计时、旧请求拒绝与状态释放", () =>
            {
                BattleContent content = BattleContent.Load(Fixture(), "restart.json");
                SkillCastRequest oldRequest = default;
                for (int i = 0; i < 3; i++)
                {
                    var session = new BattleSession(content);
                    Assert.That(session.FirstBall.Skills.TryCast(oldRequest, out _), Is.False);
                    Assert.That(session.FirstBall.Traits.TriggerCount, Is.Zero);
                    Steps(session, 6);
                    oldRequest = new SkillCastRequest(session.SessionId, "circle_shot");
                    session.Dispose();
                    Assert.That(session.FirstBall.Traits.IsStopped, Is.True);
                    Assert.That(session.Projectiles.ActiveCount, Is.Zero);
                    Assert.That(session.FirstBall.Skills.TryCast(oldRequest, out _), Is.False);
                }
            }, report, failures);
            yield return null;
            yield return null;
            Run("专项会话释放后物理场景和刚体数量恢复", () =>
            {
                Assert.That(SceneManager.sceneCount, Is.EqualTo(scenes));
                Assert.That(UnityEngine.Object.FindObjectsOfType<Rigidbody2D>().Length, Is.EqualTo(bodies));
            }, report, failures);
            string output = Path.GetFullPath(Path.Combine(Application.dataPath,
                UnityEditor.SessionState.GetString("ProjectBB.ValidationPhase", "") == "summon_ability"
                    ? "../../outputs/summon_ability_validation" : UnityEditor.SessionState.GetString("ProjectBB.ValidationPhase", "") == "projectile_bounce"
                    ? "../../outputs/projectile_bounce_validation" : UnityEditor.SessionState.GetString("ProjectBB.ValidationPhase", "") == "projectile_launch"
                    ? "../../outputs/projectile_launch_validation" : "../../outputs/config_skill_validation"));
            Directory.CreateDirectory(output);
            File.WriteAllLines(Path.Combine(output, "configured-scenarios.txt"), report);
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DefaultBootstrapLoadsConfigurationAndRestartsAutomation()
        {
            yield return new EnterPlayMode();
            var bootstrap = UnityEngine.Object.FindObjectOfType<ProjectBootstrap>();
            Assert.That(bootstrap, Is.Not.Null);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(ProjectBootstrap).GetField("_usePrototypeSettings", flags).SetValue(bootstrap, false);
            MethodInfo restart = typeof(ProjectBootstrap).GetMethod("RestartBattle", flags);
            FieldInfo field = typeof(ProjectBootstrap).GetField("_session", flags);
            restart.Invoke(bootstrap, null);
            var session = (BattleSession)field.GetValue(bootstrap);
            Assert.That(session.Content, Is.Not.Null);
            Assert.That(Application.targetFrameRate, Is.EqualTo(60));
            Assert.That(QualitySettings.vSyncCount, Is.Zero);
            session.SetSuspended(false);
            Steps(session, 60);
            Assert.That(session.FirstBall.Traits.SuccessfulCastCount, Is.EqualTo(1));
            Assert.That(session.SecondBall.Traits.SuccessfulCastCount, Is.EqualTo(1));
            restart.Invoke(bootstrap, null);
            var next = (BattleSession)field.GetValue(bootstrap);
            Assert.That(next.Content, Is.Not.SameAs(session.Content), "每次重开重新加载并验证配置快照。");
            Assert.That(session.IsDisposed && session.FirstBall.Traits.IsStopped, Is.True);
            Assert.That(next.FirstBall.Traits.TriggerCount, Is.Zero);
            Assert.That(next.Projectiles.ActiveCount, Is.Zero);
            next.SetSuspended(false); Steps(next, 60);
            Assert.That(next.FirstBall.Traits.SuccessfulCastCount, Is.EqualTo(1));
            yield return null;
            LogAssert.NoUnexpectedReceived();
            yield return new ExitPlayMode();
        }
    }
}
