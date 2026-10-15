using System;
using ProjectBB.LogicCore;

namespace ProjectBB.GamePlay
{
    /// <summary>会话持有的有界圆形弹道池；分段查询只记录候选，结算边界统一承伤与回收。</summary>
    public sealed class CircleProjectileSystem
    {
        public const int Capacity = 128;
        // 圆目标每段预计路程上限；折返段仍作弦近似，不冒充精确物理 TOI。
        public const double TargetSegmentDistance = 0.02;
        public const int MaxSegmentsPerSubstep = 256;
        // 每颗弹道每个查询细段的墙反弹预算；极窄可行区域也不能产生无界零时刻循环。
        public const int MaxWallBouncesPerSegment = 64;
        private readonly BattleSession _session;
        private readonly Flight[] _flights;
        public int Limit => _flights.Length;
        public int PeakCount { get; private set; }
        public long CandidateQueryCount { get; private set; }
        public long NarrowPhaseCount { get; private set; }
        private long _nextId;
        public int ActiveCount { get; private set; }
        public long RejectedLaunchCount { get; private set; }
        public long CapacityRejectedCount { get; private set; }
        public long SweepBudgetExceededCount { get; private set; }
        public long WallBounceCount { get; private set; }
        public long WallBounceBudgetExceededCount { get; private set; }
        public long RecycledCount { get; private set; }
        public long HitCount { get; private set; }
        internal bool HasFlyingProjectiles
        {
            get
            {
                for (int i = 0; i < ActiveCount; i++) if (!_flights[i].Retire) return true;
                return false;
            }
        }

        // 单次命中即消耗；无 Collider 列表与跨生命期命中集合，按实体资格查询恰好一次。
        private struct Flight
        {
            public long Id;
            public CircleProjectileRequest Request;
            public PlanarVector Position;
            public PlanarVector Velocity;
            public double Remaining;
            public int TargetId;
            public bool Retire;
        }

        internal CircleProjectileSystem(BattleSession session, int capacity = Capacity) { _session = session; _flights = new Flight[capacity]; }

        internal bool TryLaunch(CircleProjectileRequest request, out long attackId)
        {
            attackId = 0;
            BallCharacter source = _session.FindBall(request.SourceId);
            if (_session.IsDisposed || _session.IsFinished || _session.IsPaused || _session.IsSuspended ||
                request.SessionId != _session.SessionId || source == null || source.Combat.IsDead || source.Combat.IsPendingDeath ||
                !request.Position.IsFinite || !request.Velocity.IsFinite || !PlanarVector.IsFiniteNumber(request.Velocity.LengthSquared) ||
                request.Velocity.LengthSquared <= 1e-24 || request.Velocity.Length > 10000 ||
                !PlanarVector.IsFiniteNumber(request.Radius) || request.Radius <= 0 ||
                !PlanarVector.IsFiniteNumber(request.Lifetime) || request.Lifetime <= 0 || request.Lifetime > 60 || request.Damage < 0 ||
                !_session.Arena.Bounds.Contains(request.Position, request.Radius, 0))
            { RejectedLaunchCount++; return false; }
            if (ActiveCount == Limit) { RejectedLaunchCount++; CapacityRejectedCount++; return false; }
            // 始终按生成顺序紧凑存储；移除保持相对次序，池复用不会改变结算顺序。
            attackId = ++_nextId;
            _flights[ActiveCount++] = new Flight { Id = attackId, Request = request, Position = request.Position,
                Velocity = request.Velocity, Remaining = request.Lifetime };
            PeakCount = Math.Max(PeakCount, ActiveCount);
            return true;
        }

        public CircleProjectileSnapshot GetSnapshot(int index)
        {
            if (index < 0 || index >= ActiveCount) throw new ArgumentOutOfRangeException(nameof(index));
            Flight flight = _flights[index];
            return new CircleProjectileSnapshot(flight.Id, flight.Request.SourceId, flight.Position, flight.Request.Radius);
        }

        internal static bool CanHit(BallCharacter source, BallCharacter target)
            => source != null && target != null && source.EntityId != target.EntityId && source.FactionId != target.FactionId &&
                !source.Combat.IsDead && !source.Combat.IsPendingDeath && !target.Combat.IsDead && !target.Combat.IsPendingDeath;

        // 保留双球片段测试入口；正式物理路径使用每实体的片段起点。
        internal void SweepSegment(double seconds, PlanarVector firstStart, PlanarVector secondStart)
            => SweepSegmentCore(seconds, firstStart, secondStart, true);
        internal void SweepSegment(double seconds) => SweepSegmentCore(seconds, default, default, false);
        private void SweepSegmentCore(double seconds, PlanarVector firstStart, PlanarVector secondStart, bool supplied)
        {
            for (int i = 0; i < ActiveCount; i++)
            {
                ref Flight flight = ref _flights[i];
                if (flight.Retire) continue;
                double duration = Math.Min(seconds, flight.Remaining);
                double elapsed = 0;
                int bounces = 0;
                do
                {
                    double remaining = duration - elapsed;
                    bool wall = CollisionGeometry.SweepWalls(_session.Arena.Bounds, flight.Position, flight.Velocity,
                        flight.Request.Radius, remaining, out double wallTime, out ArenaWall walls);
                    double limit = wall ? wallTime : remaining;
                    double hitTime = double.PositiveInfinity;
                    flight.TargetId = 0;
                    for (int e = 0; e < _session.EntityCount; e++)
                    {
                        BallCharacter target = _session.GetEntity(e);
                        PlanarVector start = supplied && target.EntityId == 1 ? firstStart :
                            supplied && target.EntityId == 2 ? secondStart : target.Motor.SegmentStart;
                        FindTarget(ref flight, target, start, seconds, elapsed, limit, ref hitTime);
                    }
                    // 同刻墙优先；反弹后的剩余路径重新查询，目标也从对应的段内时间开始。
                    bool hit = flight.TargetId != 0 && (!wall || hitTime < wallTime - CollisionGeometry.TimeTolerance);
                    flight.Position += flight.Velocity * (hit ? hitTime : limit);
                    if (hit) { flight.Retire = true; break; }
                    flight.TargetId = 0;
                    if (!wall) break;
                    flight.Position = _session.Arena.Bounds.Clamp(flight.Position, flight.Request.Radius);
                    if (!flight.Request.BounceOnWall) { flight.Retire = true; break; }
                    if (bounces == MaxWallBouncesPerSegment)
                    {
                        WallBounceBudgetExceededCount++;
                        flight.Retire = true;
                        break;
                    }
                    // 角落同时反转两个速度分量，算一次反弹；速度大小与剩余寿命不重置。
                    flight.Velocity = CollisionGeometry.ReflectWalls(flight.Velocity, walls);
                    WallBounceCount++;
                    bounces++;
                    elapsed += limit;
                } while (elapsed < duration);
                flight.Remaining -= duration;
                flight.Retire |= flight.Remaining <= 1e-12;
            }
        }

        private void FindTarget(ref Flight flight, BallCharacter target, PlanarVector start, double seconds,
            double elapsed, double limit, ref double earliest)
        {
            if (!CanHit(_session.FindBall(flight.Request.SourceId), target)) return;
            CandidateQueryCount++;
            PlanarVector velocity = (target.Motor.Position - start) / seconds;
            PlanarVector targetStart = start + velocity * elapsed;
            PlanarVector relativeEnd = flight.Position - targetStart + (flight.Velocity - velocity) * limit;
            PlanarVector relativeStart = flight.Position - targetStart;
            double radius = flight.Request.Radius + target.Motor.Radius;
            // 相对路径包围盒是保守裁剪，保留相切；不使用仅终点重叠替代扫掠。
            if (Math.Min(relativeStart.X, relativeEnd.X) > radius || Math.Max(relativeStart.X, relativeEnd.X) < -radius ||
                Math.Min(relativeStart.Y, relativeEnd.Y) > radius || Math.Max(relativeStart.Y, relativeEnd.Y) < -radius) return;
            NarrowPhaseCount++;
            if (!ProjectileGeometry.Sweep(flight.Position, flight.Velocity, flight.Request.Radius,
                start + velocity * elapsed, velocity, target.Motor.Radius, limit, out double time)) return;
            if (time < earliest || (time == earliest && target.EntityId < flight.TargetId))
            { earliest = time; flight.TargetId = target.EntityId; }
        }

        internal void ExhaustSweepBudget()
        {
            SweepBudgetExceededCount++;
            for (int i = 0; i < ActiveCount; i++) _flights[i].Retire = true;
        }

        internal void Resolve(double timeSeconds)
        {
            int kept = 0;
            int count = ActiveCount;
            for (int i = 0; i < count; i++)
            {
                Flight flight = _flights[i];
                if (flight.TargetId != 0 && !_session.IsFinished &&
                    _session.Settlement.TryResolveProjectile(flight.Request.SessionId, flight.Id,
                        flight.Request.SourceId, flight.TargetId, flight.Request.Damage, out DamageResult result))
                { HitCount++; _session.PublishProjectileHit(result, timeSeconds); }
                if (flight.Retire || _session.IsFinished || _session.FindBall(flight.Request.SourceId) == null ||
                    _session.FindBall(flight.Request.SourceId).Combat.IsDead) RecycledCount++;
                else _flights[kept++] = flight;
            }
            // 后面的弹道可能致死，连同已保留的在途实例一起清理。
            if (_session.IsFinished) { RecycledCount += kept; kept = 0; }
            Array.Clear(_flights, kept, count - kept);
            ActiveCount = kept;
        }

        internal void Clear()
        {
            RecycledCount += ActiveCount;
            Array.Clear(_flights, 0, ActiveCount);
            ActiveCount = 0;
        }
    }
}
