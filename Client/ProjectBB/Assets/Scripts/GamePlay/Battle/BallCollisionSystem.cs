using System;
using System.Collections.Generic;
using ProjectBB.LogicCore;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectBB.GamePlay
{
    /// <summary>新接触值快照；全局递增轮次，每对分别结算。</summary>
    public readonly struct EntityContact
    {
        public readonly int FirstId, SecondId;
        public readonly long Round;
        internal EntityContact(int first, int second, long round) { FirstId = first; SecondId = second; Round = round; }
    }

    /// <summary>同球同子步归并后的墙撞击锚点。</summary>
    public readonly struct WallContact
    {
        public readonly int EntityId;
        public readonly PlanarVector Position;
        internal WallContact(int entityId, PlanarVector position) { EntityId = entityId; Position = position; }
    }

    /// <summary>会话独占多实体物理适配；池化槽位重置身份及接触，回调仅收集事实。</summary>
    public sealed class BallCollisionSystem : IDisposable
    {
        private const int Limit = BattleSession.EntityCapacity;
        private const double SeparationTolerance = 0.03;
        private readonly BattleSettings _settings;
        private readonly Slot[] _slots = new Slot[Limit];
        private readonly Dictionary<int, int> _indices = new Dictionary<int, int>(Limit);
        private readonly bool[,] _touching = new bool[Limit, Limit], _observed = new bool[Limit, Limit];
        private readonly EntityContact[] _contacts = new EntityContact[Limit * (Limit - 1) / 2];
        private readonly int[] _ordered = new int[Limit];
        private readonly WallContact[] _walls = new WallContact[Limit];
        private int _count;
        private Scene _scene;
        private PhysicsScene2D _physics;
        private GameObject _root;
        private PhysicsMaterial2D _material;
        private bool _collecting, _disposed;
        private sealed class Slot
        {
            public BallCharacter Character;
            public Rigidbody2D Body;
            public BallContactRelay Relay;
            public CircleCollider2D Shape;
            public int Walls, ActiveWalls;
            public PlanarVector WallPoint;
        }
        public long ContactRound { get; private set; }
        public bool IsTouching => _touching[0, 1];
        public long WallImpactCount { get; private set; }
        public long SimulationStepCount { get; private set; }
        public long PhysicsSliceCount { get; private set; }
        public int ContactCount { get; private set; }
        public int BodyCreatedCount { get; private set; }
        public int WallCount { get; private set; }
        public EntityContact GetContact(int index) => _contacts[index];
        public WallContact GetWall(int index) => _walls[index];

        internal BallCollisionSystem(BattleSettings settings, BallCharacter first, BallCharacter second, Guid sessionId)
        {
            _settings = settings;
            try
            {
                _scene = SceneManager.CreateScene("BattlePhysics_" + sessionId.ToString("N"), new CreateSceneParameters(LocalPhysicsMode.Physics2D));
                _physics = _scene.GetPhysicsScene2D();
                _root = new GameObject("BattlePhysics");
                SceneManager.MoveGameObjectToScene(_root, _scene);
                _material = new PhysicsMaterial2D("BattleElastic_Runtime") { friction = 0, bounciness = 1 };
                Register(first, 1); Register(second, 1);
                float width = (float)settings.Bounds.Width, height = (float)settings.Bounds.Height;
                CreateWall("Left", new Vector2(-width / 2 - 0.5f, 0), new Vector2(1, height + 2), 1);
                CreateWall("Right", new Vector2(width / 2 + 0.5f, 0), new Vector2(1, height + 2), 2);
                CreateWall("Bottom", new Vector2(0, -height / 2 - 0.5f), new Vector2(width + 2, 1), 4);
                CreateWall("Top", new Vector2(0, height / 2 + 0.5f), new Vector2(width + 2, 1), 8);
            }
            catch { Dispose(); throw; }
        }
        private GameObject CreateNode(string name)
        {
            var node = new GameObject(name); node.transform.SetParent(_root.transform, false); return node;
        }
        internal void Register(BallCharacter character, double mass)
        {
            if (_disposed || _indices.ContainsKey(character.EntityId) || _count == Limit) throw new InvalidOperationException("物理实体注册无效或已满。");
            int index = 0;
            while (_slots[index] != null && _slots[index].Character != null) index++;
            Slot slot = _slots[index];
            if (slot == null)
            {
                GameObject node = CreateNode("BallPhysicsSlot_" + index); node.SetActive(false);
                slot = new Slot { Body = node.AddComponent<Rigidbody2D>(), Shape = node.AddComponent<CircleCollider2D>(), Relay = node.AddComponent<BallContactRelay>() };
                _slots[index] = slot; BodyCreatedCount++;
            }
            slot.Character = character; slot.Walls = slot.ActiveWalls = 0;
            Rigidbody2D body = slot.Body;
            body.bodyType = RigidbodyType2D.Dynamic; body.gravityScale = 0; body.mass = (float)mass;
            body.drag = body.angularDrag = 0; body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.None; body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            body.position = BallMotor.ToUnity(character.Motor.Position); body.velocity = BallMotor.ToUnity(character.Motor.Velocity);
            body.angularVelocity = 0; body.rotation = 0;
            slot.Shape.radius = (float)character.Motor.Radius; slot.Shape.sharedMaterial = _material;
            slot.Relay.Initialize(this, character.EntityId, 0);
            for (int i = 0; i < Limit; i++)
            {
                _observed[index, i] = _observed[i, index] = false;
                bool overlap = _slots[i] != null && _slots[i].Character != null && i != index &&
                    (character.Motor.Position - _slots[i].Character.Motor.Position).Length < character.Motor.Radius + _slots[i].Character.Motor.Radius;
                _touching[index, i] = _touching[i, index] = overlap;
            }
            _indices.Add(character.EntityId, index);
            // ID 单调递增，复用槽位不改变结算顺序。
            _ordered[_count++] = index; body.gameObject.SetActive(true);
        }
        internal void Unregister(int entityId)
        {
            if (!_indices.TryGetValue(entityId, out int index)) return;
            Slot slot = _slots[index];
            slot.Character.Motor.Release(); slot.Body.gameObject.SetActive(false);
            slot.Relay.Initialize(null, 0, 0); slot.Character = null; slot.Walls = slot.ActiveWalls = 0;
            _indices.Remove(entityId);
            for (int i = 0; i < _count; i++) if (_ordered[i] == index)
            { Array.Copy(_ordered, i + 1, _ordered, i, _count - i - 1); _count--; break; }
            for (int i = 0; i < Limit; i++) _touching[index, i] = _touching[i, index] = _observed[index, i] = _observed[i, index] = false;
        }
        private void CreateWall(string name, Vector2 position, Vector2 size, int wall)
        {
            GameObject node = CreateNode("WallPhysics_" + name); node.transform.localPosition = position;
            var shape = node.AddComponent<BoxCollider2D>(); shape.size = size; shape.sharedMaterial = _material;
            node.AddComponent<BallContactRelay>().Initialize(this, 0, wall);
        }
        internal void RecordContact(int entityId, int otherEntityId, int wall, PlanarVector point)
        {
            if (!_collecting || _disposed || !_indices.TryGetValue(entityId, out int index)) return;
            if (_indices.TryGetValue(otherEntityId, out int other) && other != index) _observed[index, other] = _observed[other, index] = true;
            Slot slot = _slots[index];
            // 墙角选择编号最小的边界；同边只取首个片段，避免双方回调/细分次数改变生成锚点。
            if (wall != 0 && (slot.Walls & (wall | (wall - 1))) == 0) slot.WallPoint = point;
            slot.Walls |= wall;
        }
        internal void BeginStep() { SynchronizePreviousPositions(); }
        internal void Simulate(double seconds, CircleProjectileSystem projectiles = null, AbilitySystem abilities = null)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(BallCollisionSystem));
            UpdateSeparation(); Array.Clear(_observed, 0, _observed.Length);
            for (int i = 0; i < _count; i++)
            { Slot slot = _slots[_ordered[i]]; slot.Walls = 0; slot.Character.Motor.Prepare(slot.Body, seconds, _settings.CruiseAcceleration); }
            double remaining = seconds; int slices = 0;
            bool sweep = (projectiles != null && projectiles.HasFlyingProjectiles) || (abilities != null && abilities.NeedsSweep);
            while (remaining > 1e-12)
            {
                if (sweep && (projectiles == null || !projectiles.HasFlyingProjectiles) && (abilities == null || !abilities.NeedsSweep)) sweep = false;
                if (sweep && slices == CircleProjectileSystem.MaxSegmentsPerSubstep)
                { projectiles?.ExhaustSweepBudget(); abilities?.ExhaustSweepBudget(); sweep = false; }
                double speed = 0;
                for (int i = 0; i < _count; i++)
                { Slot slot = _slots[_ordered[i]]; speed = Math.Max(speed, slot.Body.velocity.magnitude); slot.Character.Motor.BeginSegment(); }
                double slice = sweep && speed > 0 ? Math.Min(remaining, CircleProjectileSystem.TargetSegmentDistance / speed) : remaining;
                _collecting = true; bool simulated;
                try { simulated = _physics.Simulate((float)slice); }
                finally { _collecting = false; }
                if (!simulated) throw new InvalidOperationException("战斗物理子步未执行，禁止继续推进战斗时钟。");
                for (int i = 0; i < _count; i++) { Slot slot = _slots[_ordered[i]]; slot.Character.Motor.Capture(slot.Body); }
                if (sweep) { projectiles?.SweepSegment(slice); abilities?.SweepSegment(slice); }
                remaining -= slice; slices++; PhysicsSliceCount++;
            }
            SimulationStepCount++;
        }
        internal void CollectContacts()
        {
            ContactCount = 0;
            WallCount = 0;
            for (int a = 0; a < _count; a++)
            {
                int i = _ordered[a]; Slot first = _slots[i];
                if ((first.Walls & ~first.ActiveWalls) != 0)
                { WallImpactCount++; _walls[WallCount++] = new WallContact(first.Character.EntityId, first.WallPoint); }
                first.ActiveWalls |= first.Walls;
                for (int b = a + 1; b < _count; b++)
                {
                    int j = _ordered[b];
                    if (!_observed[i, j] || _touching[i, j]) continue;
                    _touching[i, j] = _touching[j, i] = true;
                    _contacts[ContactCount++] = new EntityContact(first.Character.EntityId, _slots[j].Character.EntityId, ++ContactRound);
                }
            }
            UpdateSeparation();
        }
        private void UpdateSeparation()
        {
            for (int a = 0; a < _count; a++)
            {
                int i = _ordered[a]; BallMotor first = _slots[i].Character.Motor;
                _slots[i].ActiveWalls &= NearbyWalls(first);
                for (int b = a + 1; b < _count; b++)
                {
                    int j = _ordered[b]; BallMotor second = _slots[j].Character.Motor;
                    double distance = first.Radius + second.Radius + SeparationTolerance;
                    if ((second.Position - first.Position).LengthSquared > distance * distance) _touching[i, j] = _touching[j, i] = false;
                }
            }
        }
        private int NearbyWalls(BallMotor motor)
        {
            double x = _settings.Bounds.Width * 0.5 - motor.Radius - SeparationTolerance;
            double y = _settings.Bounds.Height * 0.5 - motor.Radius - SeparationTolerance;
            return (motor.Position.X <= -x ? 1 : 0) | (motor.Position.X >= x ? 2 : 0) | (motor.Position.Y <= -y ? 4 : 0) | (motor.Position.Y >= y ? 8 : 0);
        }
        public void SynchronizePreviousPositions()
        { for (int i = 0; i < _count; i++) _slots[_ordered[i]].Character.Motor.SynchronizePreviousPosition(); }
        internal DistanceJoint2D Attach(int firstId, int secondId)
        {
            if (!_indices.TryGetValue(firstId, out int a) || !_indices.TryGetValue(secondId, out int b)) return null;
            Slot first = _slots[a], second = _slots[b];
            double distance = (first.Character.Motor.Position - second.Character.Motor.Position).Length;
            if (distance > first.Character.Motor.Radius + second.Character.Motor.Radius + 0.15) return null;
            var joint = first.Body.gameObject.AddComponent<DistanceJoint2D>();
            joint.connectedBody = second.Body;
            joint.autoConfigureDistance = false; joint.autoConfigureConnectedAnchor = false;
            joint.anchor = joint.connectedAnchor = Vector2.zero;
            joint.distance = (float)distance; joint.maxDistanceOnly = true; joint.enableCollision = true;
            return joint;
        }

        internal void SetEntityTestState(int id, PlanarVector position, PlanarVector velocity)
        {
            Slot slot = _slots[_indices[id]];
            slot.Body.position = BallMotor.ToUnity(position); slot.Body.velocity = BallMotor.ToUnity(velocity);
            slot.Character.Motor.Capture(slot.Body); slot.Character.Motor.SynchronizePreviousPosition();
        }
        internal void SetTestState(PlanarVector firstPosition, PlanarVector firstVelocity, PlanarVector secondPosition, PlanarVector secondVelocity)
        {
            SetEntityTestState(1, firstPosition, firstVelocity); SetEntityTestState(2, secondPosition, secondVelocity);
            _touching[0, 1] = _touching[1, 0] = (firstPosition - secondPosition).Length < _slots[0].Character.Motor.Radius + _slots[1].Character.Motor.Radius;
            _slots[0].ActiveWalls = _slots[1].ActiveWalls = 0;
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true; _collecting = false;
            for (int i = 0; i < _count; i++) _slots[_ordered[i]].Character.Motor.Release();
            if (_root != null) _root.SetActive(false);
            if (_scene.IsValid() && _scene.isLoaded) SceneManager.UnloadSceneAsync(_scene);
            if (_material != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_material);
                else UnityEngine.Object.DestroyImmediate(_material);
            }
        }
    }
}
