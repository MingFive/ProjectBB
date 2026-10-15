using System;
using ProjectBB.LogicCore;
using UnityEngine;

namespace ProjectBB.GamePlay
{
    /// <summary>会话持有的巡航与运动控制入口；只读物理快照，不自行积分或反弹。</summary>
    public sealed class BallMotor
    {
        private PlanarVector _pendingImpulse;
        private double _cruiseHoldSeconds;
        private bool _released;
        public PlanarVector Position { get; private set; }
        public PlanarVector PreviousPosition { get; private set; }
        internal PlanarVector SegmentStart { get; private set; }
        public PlanarVector Direction { get; private set; }
        public PlanarVector Velocity { get; private set; }
        public double Radius { get; }
        public double Speed { get; private set; }
        public bool IsStopped { get; private set; }
        internal double SpeedMultiplier { get; set; } = 1;
        internal long ImpulseRevision { get; private set; }

        public BallMotor(PlanarVector position, PlanarVector direction, double radius, double speed)
        {
            if (!position.IsFinite || !direction.IsFinite || direction.LengthSquared < 1e-24)
                throw new ArgumentException("位置与方向必须有效，初始方向不能为零。");
            if (!PlanarVector.IsFiniteNumber(radius) || radius <= 0) throw new ArgumentOutOfRangeException(nameof(radius));
            Position = PreviousPosition = position;
            Direction = direction.NormalizedOr(new PlanarVector(1, 0));
            Radius = radius;
            SetSpeed(speed);
            Velocity = Direction * Speed;
        }

        /// <summary>修改巡航目标，按配置加速度渐进恢复；零目标为渐进减速。</summary>
        public void SetSpeed(double speed)
        {
            if (!PlanarVector.IsFiniteNumber(speed) || speed < 0 || speed > float.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(speed));
            if (!_released) Speed = speed;
        }

        /// <summary>显式停止优先于击退和巡航；解除后保留方向，逐步恢复巡航。</summary>
        public void SetStopped(bool stopped)
        {
            if (_released) return;
            IsStopped = stopped;
            if (stopped) { _pendingImpulse = default; _cruiseHoldSeconds = 0; }
        }

        /// <summary>提交质量相关冲量，下一子步执行；保护期暂停巡航恢复，按战斗时间计时。</summary>
        public void ApplyImpulse(PlanarVector impulse, double cruiseHoldSeconds = 0.15)
        {
            if (!impulse.IsFinite || !PlanarVector.IsFiniteNumber(cruiseHoldSeconds) || cruiseHoldSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(impulse));
            PlanarVector total = _pendingImpulse + impulse;
            if (!total.IsFinite || Math.Abs(total.X) > float.MaxValue || Math.Abs(total.Y) > float.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(impulse));
            if (_released || IsStopped) return;
            _pendingImpulse = total;
            ImpulseRevision++;
            _cruiseHoldSeconds = Math.Max(_cruiseHoldSeconds, cruiseHoldSeconds);
        }

        internal void Prepare(Rigidbody2D body, double seconds, double acceleration)
        {
            if (IsStopped) { body.velocity = Vector2.zero; return; }
            Vector2 velocity = body.velocity;
            if (velocity.sqrMagnitude > 1e-10f) Direction = FromUnity(velocity.normalized);
            bool hasImpulse = _pendingImpulse.LengthSquared > 0;
            if (hasImpulse) body.AddForce(ToUnity(_pendingImpulse), ForceMode2D.Impulse);
            _pendingImpulse = default;
            if (_cruiseHoldSeconds > 0 || hasImpulse)
            {
                _cruiseHoldSeconds = Math.Max(0, _cruiseHoldSeconds - seconds);
                return;
            }
            body.velocity = Vector2.MoveTowards(velocity, ToUnity(Direction * (Speed * SpeedMultiplier)), (float)(acceleration * seconds));
        }

        internal void BeginSegment() { SegmentStart = Position; }
        internal void Capture(Rigidbody2D body)
        {
            Position = FromUnity(body.position);
            Velocity = FromUnity(body.velocity);
            if (Velocity.LengthSquared > 1e-10) Direction = Velocity.NormalizedOr(Direction);
        }
        internal void SynchronizePreviousPosition() { PreviousPosition = Position; }
        internal void Release() { _released = true; _pendingImpulse = default; _cruiseHoldSeconds = 0; }
        internal static Vector2 ToUnity(PlanarVector value) => new Vector2((float)value.X, (float)value.Y);
        internal static PlanarVector FromUnity(Vector2 value) => new PlanarVector(value.x, value.y);
    }
}
