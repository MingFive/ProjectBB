using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using ProjectBB.GamePlay;
using ProjectBB.LogicCore;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectBB.Tests
{
    /// <summary>多实体、召唤生命周期、能力与联合预算；真实物理只在 Unity Play Mode 验收。</summary>
    public sealed class SummonAbilityTests
    {
        private const double Step = 1.0 / 60;
        private static SummonDefinition Definition(double lifetime = 10, double mass = 1, int hp = 30, int damage = 0)
            => new SummonDefinition("test_summon", 0.2, 0, mass, lifetime, 30, new CombatDefinition(hp, damage));
        private static BattleSession Create(int capacity = 128, int hp = 100)
            => new BattleSession(new BattleSettings(speedIndex: 0, projectileCapacity: capacity,
                firstCombat: new CombatDefinition(hp, 0), secondCombat: new CombatDefinition(hp, 0)));
        private static int Spawn(BattleSession session, int owner, PlanarVector position, SummonDefinition definition = null)
        {
            Assert.That(session.TrySummon(new SummonRequest(session.SessionId, owner, definition ?? Definition(), position, new PlanarVector(1, 0)), out int id), Is.True);
            return id;
        }
        private static string Sample(string name) => File.ReadAllText(Path.Combine(Application.dataPath, "Resources/Config/" + name + ".json"));
        private static void Steps(BattleSession session, int count) { for (int i = 0; i < count; i++) session.Advance(Step); }
        private static bool Cast(BattleSession session, int source, string skill, int target = 0, PlanarVector? anchor = null)
            => session.FindBall(source).Skills.TryCast(new SkillCastRequest(session.SessionId, skill, target, anchor), out _);

        [Test]
        public void NewContentAndGenerationGeometryHaveValidatedContracts()
        {
            foreach (string name in new[] { "SummonBattle", "AreaVsTrap", "DrainVsOrbit" })
                Assert.That(BattleContent.Load(Sample(name), name).FirstBall, Is.Not.Null);
            Assert.Throws<InvalidDataException>(() => BattleContent.Load(Sample("SummonBattle").Replace("\"summonBallId\": \"minion\"", "\"summonBallId\": \"missing\""), "missing"));
            Assert.Throws<InvalidDataException>(() => BattleContent.Load(Sample("SummonBattle").Replace("\"mass\": 0.5", "\"mass\": 0"), "mass"));
            Assert.Throws<ArgumentException>(() => Definition(mass: double.NaN));
            var directions = new HashSet<PlanarVector>();
            for (int i = 0; i < 8; i++)
            {
                PlanarVector direction = GenerationGeometry.Direction(new PlanarVector(1, 0), i, 8, 360);
                Assert.That(direction.Length, Is.EqualTo(1).Within(1e-12));
                Assert.That(directions.Add(direction), Is.True);
            }
            Assert.That(GenerationGeometry.Direction(new PlanarVector(1, 0), 0, 3, 90).Y, Is.LessThan(0));
            Assert.That(GenerationGeometry.Direction(new PlanarVector(1, 0), 2, 3, 90).Y, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator SummonLifecyclePoolingAndOwnerRules()
        {
            yield return new EnterPlayMode();
            using (var session = Create())
            {
                int id = Spawn(session, 1, default, Definition(lifetime: 0.1, mass: 2));
                BallCharacter old = session.FindBall(id);
                Assert.That(old.OwnerId, Is.EqualTo(1));
                Assert.That(old.FactionId, Is.EqualTo(session.FirstBall.FactionId));
                session.SetPaused(true); session.Advance(1);
                Assert.That(session.EntityCount, Is.EqualTo(3));
                session.SetPaused(false); session.SetSuspended(true); session.Advance(1);
                Assert.That(session.EntityCount, Is.EqualTo(3));
                session.SetSuspended(false); session.SetTimeScale(2); Steps(session, 3);
                Assert.That(session.FindBall(id), Is.Null);
                Assert.That(session.IsFinished, Is.False);
                int created = session.Collisions.BodyCreatedCount;
                int next = Spawn(session, 1, default);
                Assert.That(next, Is.GreaterThan(id));
                Assert.That(session.Collisions.BodyCreatedCount, Is.EqualTo(created), "刚体槽复用，不复用实体身份。");
                Assert.That(session.TrySummon(new SummonRequest(Guid.NewGuid(), 1, Definition(), new PlanarVector(0, 2), new PlanarVector(1, 0)), out _), Is.False);
                Assert.That(session.TrySummon(new SummonRequest(session.SessionId, next, Definition(), new PlanarVector(0, 2), new PlanarVector(1, 0)), out _), Is.False);
                Assert.That(session.TrySummon(new SummonRequest(session.SessionId, 1, Definition(), default, new PlanarVector(1, 0)), out _), Is.False);
                Assert.That(session.Settlement.TryResolveProjectile(session.SessionId, 1, 2, 1, 100, out _), Is.True);
                session.Advance(Step);
                // 已在外部测试入口结束的会话也必须清理召唤物。
                Assert.That(session.IsFinished, Is.True);
                Assert.That(session.EntityCount, Is.EqualTo(2));
                Assert.That(session.Settlement.WinnerEntityId, Is.EqualTo(2));
            }
            using (var session = Create())
            {
                int id = Spawn(session, 2, default, Definition(hp: 5));
                Assert.That(session.TryLaunchProjectile(new CircleProjectileRequest(session.SessionId, 1, new PlanarVector(-1, 0), new PlanarVector(100, 0)), out _), Is.True);
                Steps(session, 1);
                Assert.That(session.FindBall(id), Is.Null);
                Assert.That(session.IsFinished, Is.False, "召唤物死亡不能决定主球胜负。");
                Assert.That(session.SecondBall.Combat.CurrentHp, Is.EqualTo(100));
            }
            yield return null;
            LogAssert.NoUnexpectedReceived();
            yield return new ExitPlayMode();
        }

        [Test]
        public void SequenceSnapshotSortsAndDoesNotShareMutableAuthoringData()
        {
            GenerationSequenceAsset asset = ScriptableObject.CreateInstance<GenerationSequenceAsset>();
            try
            {
                var later = new GenerationSequenceAsset.Cue { timeSeconds = 0.5f, casterId = 1, skillId = "summon_ball" };
                var earlier = new GenerationSequenceAsset.Cue { timeSeconds = 0.25f, casterId = 2, skillId = "shot" };
                typeof(GenerationSequenceAsset).GetField("_cues", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(asset, new List<GenerationSequenceAsset.Cue> { later, earlier });
                BattleContent content = BattleContent.Load(Sample("SummonBattle"), "sequence");
                GenerationCue[] snapshot = asset.Snapshot(content);
                Assert.That(snapshot[0].SkillId, Is.EqualTo("shot"));
                Assert.That(snapshot[1].Time, Is.EqualTo(0.5));
                later.timeSeconds = 4;
                Assert.That(snapshot[1].Time, Is.EqualTo(0.5), "运行快照不引用可变 SO 记录。");
                earlier.skillId = "missing";
                Assert.Throws<ArgumentException>(() => asset.Snapshot(content));
            }
            finally { UnityEngine.Object.DestroyImmediate(asset); }
        }

        [UnityTest]
        public IEnumerator MassContactsAndNearestProjectileTarget()
        {
            yield return new EnterPlayMode();
            using (var session = Create())
            {
                int a = Spawn(session, 1, new PlanarVector(-0.5, 2), Definition(mass: 1, damage: 7));
                int b = Spawn(session, 2, new PlanarVector(0.5, 2), Definition(mass: 3, damage: 11));
                session.FindBall(a).Motor.ApplyImpulse(new PlanarVector(3, 0), 1);
                session.FindBall(b).Motor.ApplyImpulse(new PlanarVector(-3, 0), 1);
                Steps(session, 12);
                Assert.That(session.FindBall(a).Combat.CurrentHp, Is.EqualTo(19));
                Assert.That(session.FindBall(b).Combat.CurrentHp, Is.EqualTo(23));
                Assert.That(session.FindBall(a).Motor.Velocity.X, Is.LessThan(0));
                Assert.That(session.IsFinished, Is.False);
            }
            using (var session = Create())
            {
                int near = Spawn(session, 2, new PlanarVector(-1, 2));
                int far = Spawn(session, 2, new PlanarVector(1, 2));
                Spawn(session, 1, new PlanarVector(-2, 2));
                Assert.That(session.TryLaunchProjectile(new CircleProjectileRequest(session.SessionId, 1, new PlanarVector(-3, 2), new PlanarVector(480, 0)), out _), Is.True);
                Steps(session, 1);
                Assert.That(session.FindBall(near).Combat.CurrentHp, Is.EqualTo(20));
                Assert.That(session.FindBall(far).Combat.CurrentHp, Is.EqualTo(30));
                Assert.That(session.Projectiles.HitCount, Is.EqualTo(1));
            }
            LogAssert.NoUnexpectedReceived();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ConfiguredGenerationAndSequenceUseBattleClock()
        {
            yield return new EnterPlayMode();
            using (var session = new BattleSession(BattleContent.Load(Sample("SummonBattle"), "summon")))
            {
                session.FirstBall.Traits.Remove("summoner"); session.FirstBall.Traits.Remove("shoot");
                session.SecondBall.Traits.Remove("summoner"); session.SecondBall.Traits.Remove("shoot");
                session.FirstBall.Motor.SetStopped(true); session.SecondBall.Motor.SetStopped(true);
                session.SetSequence(new[] { new GenerationCue(0.1, 1, "summon_ball"), new GenerationCue(0.2, 2, "summon_ball") });
                Steps(session, 5); Assert.That(session.EntityCount, Is.EqualTo(2));
                Steps(session, 1); Assert.That(session.EntityCount, Is.EqualTo(3));
                Steps(session, 6); Assert.That(session.EntityCount, Is.EqualTo(4));
                session.Dispose(); Assert.That(session.EntityCount, Is.EqualTo(2));
            }
            LogAssert.NoUnexpectedReceived();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator AreaTrapOrbitAndDrainShareSettlementAndCleanup()
        {
            yield return new EnterPlayMode();
            using (var session = new BattleSession(BattleContent.Load(Sample("AreaVsTrap"), "abilities")))
            {
                session.FirstBall.Traits.Remove("superman"); session.SecondBall.Traits.Remove("teemo");
                session.Collisions.SetTestState(new PlanarVector(-1, 0), default, new PlanarVector(1, 0), default);
                session.FirstBall.Motor.SetStopped(true); session.SecondBall.Motor.SetStopped(true);
                Assert.That(Cast(session, 1, "burst"), Is.True);
                Assert.That(session.SecondBall.Combat.CurrentHp, Is.EqualTo(475));
                Assert.That(Cast(session, 1, "mushroom", anchor: session.SecondBall.Motor.Position), Is.True);
                Steps(session, 1);
                Assert.That(session.SecondBall.Combat.CurrentHp, Is.EqualTo(460));
                Assert.That(session.Abilities.ActiveCount, Is.Zero);
                Steps(session, 2); Assert.That(session.SecondBall.Combat.CurrentHp, Is.EqualTo(460));
            }
            using (var session = new BattleSession(BattleContent.Load(Sample("DrainVsOrbit").Replace("\"collisionDamage\": 10", "\"collisionDamage\": 0"), "drain")))
            {
                session.FirstBall.Traits.Remove("vampire_contact"); session.SecondBall.Traits.Remove("berserker");
                session.Collisions.SetTestState(new PlanarVector(-0.5, 0), default, new PlanarVector(0.5, 0), default);
                session.FirstBall.Motor.SetStopped(true); session.SecondBall.Motor.SetStopped(true);
                session.Settlement.TryResolveSkill(100, 2, 1, 50, 0, out _);
                Assert.That(Cast(session, 1, "vampire", 2), Is.True);
                Assert.That(Cast(session, 2, "vampire", 1), Is.True);
                Assert.That(session.Abilities.LinkCount, Is.EqualTo(1));
                Assert.That(Cast(session, 1, "vampire", 2), Is.False, "持续接触不能无限刷新。");
                Steps(session, 13);
                Assert.That(session.Abilities.HitCount, Is.EqualTo(2));
                Assert.That(session.FirstBall.Combat.CurrentHp, Is.EqualTo(450));
                Assert.That(session.SecondBall.Combat.CurrentHp, Is.EqualTo(500));
                Steps(session, 60);
                Assert.That(session.Abilities.LinkCount, Is.Zero);
                Assert.That(session.FirstBall.Combat.SpeedBuffs.Multiplier, Is.EqualTo(1));
                Assert.That(session.SecondBall.Combat.SpeedBuffs.Multiplier, Is.EqualTo(1));
            }
            using (var session = new BattleSession(BattleContent.Load(Sample("DrainVsOrbit"), "orbit")))
            {
                session.FirstBall.Traits.Remove("vampire_contact");
                session.Collisions.SetTestState(new PlanarVector(1.2, 0), default, default, default);
                session.FirstBall.Motor.SetStopped(true); session.SecondBall.Motor.SetStopped(true);
                Steps(session, 1);
                Assert.That(session.Abilities.ActiveCount, Is.EqualTo(2));
                Assert.That(session.FirstBall.Combat.CurrentHp, Is.EqualTo(495));
                Steps(session, 2); Assert.That(session.FirstBall.Combat.CurrentHp, Is.EqualTo(495));
                Assert.That(session.SecondBall.Traits.Remove("berserker"), Is.True);
                Assert.That(session.Abilities.ActiveCount, Is.Zero);
                Assert.That(session.SecondBall.Traits.Remove("berserker"), Is.False);
            }
            LogAssert.NoUnexpectedReceived();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator WallTriggersImmunityAndImpulseRelease()
        {
            yield return new EnterPlayMode();
            using (var session = new BattleSession(BattleContent.Load(Sample("AreaVsTrap"), "wall")))
            {
                session.FirstBall.Traits.Remove("superman");
                session.Collisions.SetTestState(new PlanarVector(-3, -3), default, new PlanarVector(4.45, 4.45), new PlanarVector(10, 10));
                Steps(session, 1);
                Assert.That(session.SecondBall.Traits.SuccessfulCastCount, Is.EqualTo(1), "墙角同子步只生成一颗蘑菇。");
                Assert.That(session.Abilities.ActiveCount, Is.EqualTo(1));
                session.SecondBall.Motor.SetStopped(true);
                Steps(session, 3);
                Assert.That(session.SecondBall.Traits.SuccessfulCastCount, Is.EqualTo(1));
                session.SecondBall.Traits.Remove("teemo");
                Assert.That(session.Abilities.ActiveCount, Is.Zero);
            }
            string drain = Sample("DrainVsOrbit").Replace("\"damageImmune\": false", "\"damageImmune\": true");
            using (var session = new BattleSession(BattleContent.Load(drain, "immune")))
            {
                session.FirstBall.Traits.Remove("vampire_contact"); session.SecondBall.Traits.Remove("berserker");
                session.Collisions.SetTestState(new PlanarVector(-0.5, 0), default, new PlanarVector(0.5, 0), default);
                session.FirstBall.Motor.SetSpeed(0); session.SecondBall.Motor.SetSpeed(0);
                Assert.That(Cast(session, 1, "vampire", 2), Is.True);
                Steps(session, 13);
                Assert.That(session.SecondBall.Combat.CurrentHp, Is.EqualTo(500));
                Assert.That(session.Abilities.HitCount, Is.EqualTo(1), "免疫仍是有效命中，但没有 HP 损失和治疗。");
                session.FirstBall.Motor.ApplyImpulse(new PlanarVector(-2, 0));
                Steps(session, 1);
                Assert.That(session.Abilities.LinkCount, Is.Zero);
                Assert.That(session.FirstBall.Combat.SpeedBuffs.Multiplier, Is.EqualTo(1));
            }
            LogAssert.NoUnexpectedReceived();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator JointCapacityRecordsReal300And20Workload()
        {
            yield return new EnterPlayMode();
            yield return RunCapacity();
            yield return new ExitPlayMode();
        }
        private static IEnumerator RunCapacity()
        {
            var report = new List<string> { "Editor workload only; not GPU/frame timing or target-device 60 FPS evidence.",
                "Unity=" + Application.unityVersion + "; device=" + SystemInfo.processorType };
            foreach (bool dense in new[] { false, true })
            using (var session = Create(384, 1000000))
            {
                for (int i = 0; i < 20; i++)
                {
                    double spacing = dense ? 0.43 : 0.9;
                    int id = Spawn(session, i % 2 + 1, new PlanarVector((i % 5 - 2) * spacing, (i / 5 - 1.5) * spacing), Definition(lifetime: 60));
                    session.FindBall(id).Motor.SetSpeed(dense ? 3 : 1);
                }
                var samples = new double[300];
                int gc = GC.CollectionCount(0);
                long beforeMemory = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
                var watch = new Stopwatch();
                for (int frame = 0; frame < samples.Length; frame++)
                {
                    watch.Restart();
                    while (session.Projectiles.ActiveCount < 300)
                        if (!session.TryLaunchProjectile(new CircleProjectileRequest(session.SessionId, 1,
                            new PlanarVector(0, 4), new PlanarVector(12, 0), 0.05, 1, 0, true), out _))
                            throw new InvalidOperationException("联合压测未能填充 300 弹道。");
                    if (session.EntityCount != 22 || session.Projectiles.ActiveCount != 300)
                        throw new InvalidOperationException("联合压测对象数量不符。");
                    session.Advance(Step);
                    watch.Stop(); samples[frame] = watch.Elapsed.TotalMilliseconds;
                }
                Array.Sort(samples);
                report.Add($"dense={dense}; battleSeconds={session.ElapsedSeconds:F2}; summons=20; projectilePeak={session.Projectiles.PeakCount}; CPU_ms_P50={samples[150]:F3}; P95={samples[285]:F3}; P99={samples[297]:F3}; max={samples[299]:F3}; GC0={GC.CollectionCount(0)-gc}; monoMemoryDelta={UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong()-beforeMemory}; candidates={session.Projectiles.CandidateQueryCount}; narrow={session.Projectiles.NarrowPhaseCount}; simulate={session.Collisions.PhysicsSliceCount}; rejected={session.Projectiles.CapacityRejectedCount}; sweepBudget={session.Projectiles.SweepBudgetExceededCount}");
                Assert.That(session.Projectiles.PeakCount, Is.EqualTo(300));
                Assert.That(session.Projectiles.SweepBudgetExceededCount, Is.Zero);
                Assert.That(session.Projectiles.CapacityRejectedCount, Is.Zero);
                Assert.That(session.IsFinished, Is.False);
                session.Dispose(); Assert.That(session.EntityCount, Is.EqualTo(2));
            }
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../outputs/summon_ability_validation"));
            Directory.CreateDirectory(output); File.WriteAllLines(Path.Combine(output, "capacity.txt"), report);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }
    }
}

