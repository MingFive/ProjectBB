using System;
using System.Collections.Generic;
using ProjectBB.LogicCore;

namespace ProjectBB.GamePlay
{
    /// <summary>单场双球战斗会话，独占固定步长、随机源和实体生命周期，由装配层推进。</summary>
    public sealed class BattleSession : IDisposable
    {
        public const int EntityCapacity = 64;
        private readonly List<BallCharacter> _entities = new List<BallCharacter>(EntityCapacity);
        private readonly Dictionary<int, BallCharacter> _entityById = new Dictionary<int, BallCharacter>(EntityCapacity);
        private int _nextEntityId = 2;
        private GenerationCue[] _sequence = Array.Empty<GenerationCue>();
        private int _nextCue;
        public long SequenceRejectedCount { get; private set; }
        public int EntityCount => _entities.Count;
        public long SummonRejectedCount { get; private set; }
        public long SummonCreatedCount { get; private set; }
        public long SummonRemovedCount { get; private set; }
        public int PeakEntityCount { get; private set; } = 2;
        public BallCharacter GetEntity(int index) => _entities[index];
        private double _accumulator;
        private readonly BattleSettings _settings;
        private readonly Random _skillRandom;
        private readonly Queue<CollisionBatchResult> _feedback = new Queue<CollisionBatchResult>(64);
        private readonly Queue<ProjectileHitResult> _projectileFeedback = new Queue<ProjectileHitResult>(64);
        public Guid SessionId { get; } = Guid.NewGuid();
        public ArenaManager Arena { get; }
        public BallCharacter FirstBall { get; }
        public BallCharacter SecondBall { get; }
        public BallCollisionSystem Collisions { get; }
        public BattleSettlementSystem Settlement { get; }
        public CircleProjectileSystem Projectiles { get; }
        public AbilitySystem Abilities { get; }
        public BattleContent Content { get; }
        public bool IsFinished => Settlement.IsFinished;
        public long TickCount { get; private set; }
        public double ElapsedSeconds { get; private set; }
        public double MaxBattleSeconds => _settings.MaxBattleSeconds;
        // 结束后只允许反馈淡出使用的表现时间推进，不能再驱动战斗状态。
        public double PresentationSeconds { get; private set; }
        public long DroppedFeedbackCount { get; private set; }
        public double TimeScale { get; private set; } = 1;
        public bool IsPaused { get; private set; }
        public bool IsSuspended { get; private set; }
        public bool IsDisposed { get; private set; }
        public long CatchUpLimitCount { get; private set; }
        public double InterpolationAlpha => IsPaused || IsSuspended || IsFinished ? 1 : _accumulator / _settings.FixedStep;

        public BattleSession(BattleContent content) : this(content?.Settings ?? throw new ArgumentNullException(nameof(content)), content) { }

        public BattleSession(BattleSettings settings) : this(settings, null) { }

        private BattleSession(BattleSettings settings, BattleContent content)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Content = content;
            Arena = new ArenaManager(settings.Bounds);
            var random = new Random(settings.Seed);
            // 技能使用独立序列，不因新增随机弹道改变初始运动方向；每场重建，禁止使用表现随机源。
            _skillRandom = new Random(unchecked(settings.Seed ^ 0x51ED270B));
            FirstBall = CreateBall(1, true, random);
            SecondBall = CreateBall(2, false, random);
            _entities.Add(FirstBall); _entities.Add(SecondBall);
            _entityById.Add(1, FirstBall); _entityById.Add(2, SecondBall);
            Settlement = new BattleSettlementSystem(SessionId, FirstBall, SecondBall, FindBall);
            Projectiles = new CircleProjectileSystem(this, settings.ProjectileCapacity);
            Collisions = new BallCollisionSystem(settings, FirstBall, SecondBall, SessionId);
            Abilities = new AbilitySystem(this);
            FirstBall.BindContent(this, content, content?.FirstBall);
            SecondBall.BindContent(this, content, content?.SecondBall);
        }

        public BallCharacter FindBall(int entityId)
            => _entityById.TryGetValue(entityId, out BallCharacter entity) ? entity : null;

        internal BallCharacter FindEnemy(BallCharacter source)
        {
            BallCharacter nearest = null;
            double distance = double.PositiveInfinity;
            for (int i = 0; i < _entities.Count; i++)
            {
                BallCharacter candidate = _entities[i];
                if (!CircleProjectileSystem.CanHit(source, candidate)) continue;
                double square = (candidate.Motor.Position - source.Motor.Position).LengthSquared;
                if (square < distance) { distance = square; nearest = candidate; }
            }
            return nearest;
        }

        public bool TrySummon(SummonRequest request, out int entityId)
        {
            entityId = 0;
            BallCharacter owner = FindBall(request.OwnerId);
            SummonDefinition definition = request.Definition;
            if (IsDisposed || IsFinished || IsPaused || IsSuspended || request.SessionId != SessionId ||
                owner == null || owner.IsSummon || owner.Combat.IsDead || owner.Combat.IsPendingDeath || definition == null ||
                !request.Position.IsFinite || !request.Direction.IsFinite || request.Direction.LengthSquared < 1e-24 ||
                !PlanarVector.IsFiniteNumber(request.Direction.LengthSquared) ||
                !Arena.Bounds.Contains(request.Position, definition.Radius, 0) || _entities.Count == EntityCapacity)
            { SummonRejectedCount++; return false; }
            int owned = 0;
            for (int i = 0; i < _entities.Count; i++)
            {
                BallCharacter other = _entities[i];
                if (other.OwnerId == owner.EntityId && other.DefinitionId == definition.Id) owned++;
                double separation = definition.Radius + other.Motor.Radius + 0.01;
                if ((other.Motor.Position - request.Position).LengthSquared < separation * separation)
                { SummonRejectedCount++; return false; }
            }
            if (owned >= definition.MaxPerOwner) { SummonRejectedCount++; return false; }
            int id = checked(++_nextEntityId);
            var entity = new BallCharacter(id, owner.FactionId,
                new BallMotor(request.Position, request.Direction, definition.Radius, definition.Speed),
                definition.Combat, owner.EntityId, definition.Id, ElapsedSeconds + definition.Lifetime);
            entity.BindContent(this, Content, null);
            Collisions.Register(entity, definition.Mass);
            _entities.Add(entity); _entityById.Add(id, entity);
            PeakEntityCount = Math.Max(PeakEntityCount, _entities.Count);
            SummonCreatedCount++; entityId = id;
            return true;
        }

        // 仅在完成结算后修改实体集合；ID 不复用，物理槽位复用时完整重置接触。
        private void CleanupSummons(bool all = false)
        {
            for (int i = _entities.Count - 1; i >= 2; i--)
            {
                BallCharacter entity = _entities[i];
                BallCharacter owner = FindBall(entity.OwnerId);
                if (!all && !entity.Combat.IsDead && ElapsedSeconds + 1e-12 < entity.ExpiresAt &&
                    owner != null && !owner.Combat.IsDead) continue;
                entity.Traits.Stop();
                Collisions.Unregister(entity.EntityId);
                _entityById.Remove(entity.EntityId); _entities.RemoveAt(i);
                SummonRemovedCount++;
            }
        }

        internal PlanarVector NextSkillDirection()
        {
            double angle = _skillRandom.NextDouble() * Math.PI * 2;
            return new PlanarVector(Math.Cos(angle), Math.Sin(angle));
        }

        public void SetSequence(GenerationCue[] cues)
        {
            if (IsDisposed || TickCount != 0 || Content == null || cues == null || cues.Length > 128)
                throw new InvalidOperationException("时间线只能在配置会话首次推进前装配，最多 128 项。");
            double last = -1;
            for (int i = 0; i < cues.Length; i++)
            {
                GenerationCue cue = cues[i];
                if (!PlanarVector.IsFiniteNumber(cue.Time) || cue.Time < last || cue.Time < 0 || cue.Time > 600 ||
                    (cue.CasterId != 1 && cue.CasterId != 2) || !Content.TryGetSkill(cue.SkillId, out _))
                    throw new ArgumentException("时间线要求递增合法时间、主球身份和已存在技能。");
                last = cue.Time;
            }
            _sequence = (GenerationCue[])cues.Clone(); _nextCue = 0;
        }

        /// <summary>发射请求带会话身份；暂停、后台、结束及旧会话请求均拒绝。</summary>
        public bool TryLaunchProjectile(CircleProjectileRequest request, out long attackId)
            => Projectiles.TryLaunch(request, out attackId);

        internal void PublishProjectileHit(DamageResult damage, double timeSeconds)
        {
            if (_projectileFeedback.Count == 64) { _projectileFeedback.Dequeue(); DroppedFeedbackCount++; }
            _projectileFeedback.Enqueue(new ProjectileHitResult(damage, timeSeconds));
        }

        public bool TryDequeueProjectileFeedback(out ProjectileHitResult result)
        {
            if (_projectileFeedback.Count == 0) { result = default; return false; }
            result = _projectileFeedback.Dequeue();
            return true;
        }

        private BallCharacter CreateBall(int id, bool left, Random random)
        {
            double angle = (random.NextDouble() - 0.5) * Math.PI * 0.5;
            if (!left) angle += Math.PI;
            var direction = new PlanarVector(Math.Cos(angle), Math.Sin(angle));
            return new BallCharacter(id, id,
                new BallMotor(Arena.GetSpawnPosition(left, _settings.Radius), direction, _settings.Radius, _settings.Speed),
                left ? _settings.FirstCombat : _settings.SecondCombat);
        }

        private bool ResolveContact(int firstId, int secondId, long round, double timeSeconds)
        {
            if (Settlement.TryResolveContact(SessionId, round, timeSeconds, FindBall(firstId), FindBall(secondId), out CollisionBatchResult result))
            {
                // 表现未消费时有界丢弃旧反馈；不会丢失承伤、死亡或结果状态。
                if (_feedback.Count == 64) { _feedback.Dequeue(); DroppedFeedbackCount++; }
                _feedback.Enqueue(result);
            }
            return !IsFinished;
        }

        public bool TryDequeueFeedback(out CollisionBatchResult result)
        {
            if (_feedback.Count == 0) { result = default; return false; }
            result = _feedback.Dequeue();
            return true;
        }

        /// <summary>输入未缩放帧时长；超长帧及补步预算外的时间被舍弃，不补算后台时间。</summary>
        public void Advance(double unscaledSeconds)
        {
            if (!PlanarVector.IsFiniteNumber(unscaledSeconds) || unscaledSeconds < 0) throw new ArgumentOutOfRangeException(nameof(unscaledSeconds));
            if (IsDisposed || IsPaused || IsSuspended) return;
            if (unscaledSeconds > 0.25) { unscaledSeconds = 0.25; CatchUpLimitCount++; }
            if (IsFinished)
            {
                StopTraits(); Projectiles.Clear(); Abilities.Clear(); CleanupSummons(true);
                PresentationSeconds += unscaledSeconds * TimeScale;
                return;
            }
            _accumulator += unscaledSeconds * TimeScale;
            int steps = 0;
            while (_accumulator + 1e-12 >= _settings.FixedStep && steps < _settings.MaxStepsPerFrame)
            {
                double duration = Math.Min(_settings.FixedStep, Math.Max(0, MaxBattleSeconds - ElapsedSeconds));
                Collisions.BeginStep();
                double consumed = 0;
                double substep = _settings.FixedStep / _settings.PhysicsSubsteps;
                while (consumed + 1e-12 < duration && !IsFinished)
                {
                    // 开战即发射也只发生在首个实际战斗步；暂停期间创建会话不会提前释放。
                    TickTraits();
                    if (IsFinished) break;
                    double seconds = Math.Min(substep, duration - consumed);
                    Abilities.BeforeSimulation();
                    Collisions.Simulate(seconds, Projectiles, Abilities);
                    consumed += seconds;
                    ElapsedSeconds += seconds;
                    Collisions.CollectContacts();
                    for (int c = 0; c < Collisions.ContactCount && !IsFinished; c++)
                    {
                        EntityContact contact = Collisions.GetContact(c);
                        ResolveContact(contact.FirstId, contact.SecondId, contact.Round, ElapsedSeconds);
                    }
                    // 不将路径比例伪装成与 Dynamic 接触可比较的 TOI：接触先结算，再按弹道生成顺序逐批处理。
                    Projectiles.Resolve(ElapsedSeconds);
                    Abilities.Resolve();
                    if (!IsFinished && ElapsedSeconds + 1e-12 < MaxBattleSeconds) NotifyContacts();
                    CleanupSummons(IsFinished);
                    // 同边界死亡和超时优先于到期触发；新弹道从下一子步开始运动。
                    if (!IsFinished && ElapsedSeconds + 1e-12 < MaxBattleSeconds) TickTraits();
                }
                PresentationSeconds = ElapsedSeconds;
                _accumulator = Math.Max(0, _accumulator - consumed);
                TickCount++;
                steps++;
                // 截止时刻发生的碰撞先完成互伤；未因死亡结束时再按剩余生命比例裁决。
                if (!IsFinished && ElapsedSeconds + 1e-12 >= MaxBattleSeconds) Settlement.ResolveTimeout();
                if (IsFinished) { StopTraits(); Projectiles.Clear(); Abilities.Clear(); CleanupSummons(true); ResetInterpolation(); break; }
            }
            if (_accumulator + 1e-12 >= _settings.FixedStep)
            {
                _accumulator = 0;
                Collisions.SynchronizePreviousPositions();
                CatchUpLimitCount++;
            }
        }

        public void SetPaused(bool paused) { IsPaused = paused; ResetInterpolation(); }
        private void NotifyContacts()
        {
            for (int i = 0; i < Collisions.ContactCount && !IsFinished; i++)
            {
                EntityContact contact = Collisions.GetContact(i);
                BallCharacter first = FindBall(contact.FirstId), second = FindBall(contact.SecondId);
                if (!CircleProjectileSystem.CanHit(first, second)) continue;
                first.Traits.Notify("onContact", second.EntityId);
                second.Traits.Notify("onContact", first.EntityId);
            }
            for (int i = 0; i < Collisions.WallCount && !IsFinished; i++)
            {
                WallContact contact = Collisions.GetWall(i);
                FindBall(contact.EntityId)?.Traits.Notify("onWall", anchor: contact.Position);
            }
        }
        private void TickTraits()
        {
            while (_nextCue < _sequence.Length && !IsFinished && _sequence[_nextCue].Time <= ElapsedSeconds + 1e-12)
            {
                GenerationCue cue = _sequence[_nextCue++];
                if (!FindBall(cue.CasterId).Skills.TryCast(new SkillCastRequest(SessionId, cue.SkillId), out _)) SequenceRejectedCount++;
            }
            int count = _entities.Count;
            for (int i = 0; i < count; i++) _entities[i].Traits.Tick(ElapsedSeconds);
        }
        private void StopTraits() { for (int i = 0; i < _entities.Count; i++) _entities[i].Traits.Stop(); }
        public void SetSuspended(bool suspended) { IsSuspended = suspended; ResetInterpolation(); }
        public void SetTimeScale(double scale)
        {
            if (!PlanarVector.IsFiniteNumber(scale) || scale <= 0 || scale > 4) throw new ArgumentOutOfRangeException(nameof(scale));
            TimeScale = scale;
        }

        private void ResetInterpolation() { _accumulator = 0; Collisions.SynchronizePreviousPositions(); }
        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            StopTraits();
            Settlement.Close();
            Abilities.Clear();
            CleanupSummons(true);
            Projectiles.Clear();
            _projectileFeedback.Clear();
            _feedback.Clear();
            ResetInterpolation();
            Collisions.Dispose();
        }
    }
}
