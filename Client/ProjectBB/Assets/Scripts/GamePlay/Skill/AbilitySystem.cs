using System;
using ProjectBB.LogicCore;
using UnityEngine;

namespace ProjectBB.GamePlay
{
    /// <summary>会话级范围、陷阱、环绕和贴附模块；查询与承伤分离，所有时间由会话推进。</summary>
    public sealed class AbilitySystem
    {
        public const int ObjectCapacity = 256;
        private readonly BattleSession _session;
        private readonly SkillObject[] _objects = new SkillObject[ObjectCapacity];
        private readonly DrainLink[] _links = new DrainLink[32];
        private long _nextId;
        private sealed class SkillObject
        {
            public bool Active, Retire;
            public long Id;
            public int SourceId, AxeIndex, TrapTarget;
            public SkillDefinition Skill;
            public PlanarVector Position;
            public double Age;
            public readonly int[] Targets = new int[BattleSession.EntityCapacity];
            public readonly double[] NextHit = new double[BattleSession.EntityCapacity];
            public readonly bool[] Pending = new bool[BattleSession.EntityCapacity];
        }
        private sealed class DrainLink
        {
            public long Id;
            public int First, Second;
            public double ExpiresAt;
            public DistanceJoint2D Joint;
            public SkillDefinition FirstSkill, SecondSkill;
            public double FirstNext, SecondNext;
            public long FirstImpulse, SecondImpulse;
        }
        public int ActiveCount { get; private set; }
        public int LinkCount { get; private set; }
        public long RejectedCount { get; private set; }
        public long HitCount { get; private set; }
        public long SweepBudgetExceededCount { get; private set; }
        public bool NeedsSweep => ActiveCount > 0;
        internal AbilitySystem(BattleSession session) { _session = session; }

        internal bool TryCast(BallCharacter source, BallCharacter target, SkillDefinition skill, PlanarVector position, out long id)
        {
            id = 0;
            if (skill.Kind == SkillKind.Area)
            {
                id = ++_nextId;
                for (int i = 0; i < _session.EntityCount && !_session.IsFinished; i++)
                {
                    BallCharacter candidate = _session.GetEntity(i);
                    double radius = skill.Radius + candidate.Motor.Radius;
                    if (CircleProjectileSystem.CanHit(source, candidate) && (candidate.Motor.Position - position).LengthSquared <= radius * radius)
                        Hit(id, source.EntityId, candidate.EntityId, skill, false);
                }
                return true;
            }
            if (skill.Kind == SkillKind.Drain) return Attach(source, target, skill, out id);
            int owned = 0;
            for (int i = 0; i < _objects.Length; i++)
                if (_objects[i] != null && _objects[i].Active && _objects[i].SourceId == source.EntityId && _objects[i].Skill.Id == skill.Id) owned++;
            int count = skill.Kind == SkillKind.Orbit ? 2 : 1;
            if (ActiveCount + count > ObjectCapacity || owned + count > skill.MaxPerOwner || (skill.Kind == SkillKind.Orbit && owned > 0))
            { RejectedCount++; return false; }
            for (int n = 0; n < count; n++)
            {
                int index = 0;
                while (_objects[index] != null && _objects[index].Active) index++;
                SkillObject item = _objects[index] ?? (_objects[index] = new SkillObject());
                item.Active = true; item.Retire = false; item.Id = ++_nextId; item.SourceId = source.EntityId;
                item.Skill = skill; item.Position = position; item.AxeIndex = n; item.Age = 0; item.TrapTarget = 0;
                Array.Clear(item.Targets, 0, item.Targets.Length);
                Array.Clear(item.NextHit, 0, item.NextHit.Length);
                Array.Clear(item.Pending, 0, item.Pending.Length);
                if (skill.Kind == SkillKind.Orbit) item.Position = OrbitPosition(source.Motor.Position, skill, 0, n);
                ActiveCount++; id = item.Id;
            }
            return true;
        }

        private bool Attach(BallCharacter source, BallCharacter target, SkillDefinition skill, out long id)
        {
            id = 0;
            for (int i = 0; i < _links.Length; i++)
            {
                DrainLink existing = _links[i];
                if (existing == null || !((existing.First == source.EntityId && existing.Second == target.EntityId) ||
                    (existing.Second == source.EntityId && existing.First == target.EntityId))) continue;
                bool first = existing.First == source.EntityId;
                if ((first ? existing.FirstSkill : existing.SecondSkill) != null) { RejectedCount++; return false; }
                if (first) { existing.FirstSkill = skill; existing.FirstNext = _session.ElapsedSeconds + skill.HitInterval; }
                else { existing.SecondSkill = skill; existing.SecondNext = _session.ElapsedSeconds + skill.HitInterval; }
                existing.ExpiresAt = Math.Min(existing.ExpiresAt, _session.ElapsedSeconds + skill.Lifetime);
                ApplySlow(existing);
                id = existing.Id; return true;
            }
            if (LinkCount == _links.Length) { RejectedCount++; return false; }
            DistanceJoint2D joint = _session.Collisions.Attach(source.EntityId, target.EntityId);
            if (joint == null) { RejectedCount++; return false; }
            int slot = 0; while (_links[slot] != null) slot++;
            bool sourceFirst = source.EntityId < target.EntityId;
            var link = new DrainLink { Id = ++_nextId, First = sourceFirst ? source.EntityId : target.EntityId,
                Second = sourceFirst ? target.EntityId : source.EntityId, Joint = joint,
                ExpiresAt = _session.ElapsedSeconds + skill.Lifetime,
                FirstSkill = sourceFirst ? skill : null, SecondSkill = sourceFirst ? null : skill,
                FirstNext = _session.ElapsedSeconds + skill.HitInterval, SecondNext = _session.ElapsedSeconds + skill.HitInterval };
            link.FirstImpulse = _session.FindBall(link.First).Motor.ImpulseRevision;
            link.SecondImpulse = _session.FindBall(link.Second).Motor.ImpulseRevision;
            _links[slot] = link; LinkCount++; ApplySlow(link); id = link.Id; return true;
        }
        private void ApplySlow(DrainLink link)
        {
            double multiplier = Math.Min(link.FirstSkill?.SpeedMultiplier ?? 1, link.SecondSkill?.SpeedMultiplier ?? 1);
            _session.FindBall(link.First).Combat.SpeedBuffs.Add(link.Id, multiplier);
            _session.FindBall(link.Second).Combat.SpeedBuffs.Add(link.Id, multiplier);
        }
        private static PlanarVector OrbitPosition(PlanarVector center, SkillDefinition skill, double age, int index)
        {
            double angle = (skill.AngularSpeed * age + index * 180) * Math.PI / 180;
            return center + new PlanarVector(Math.Cos(angle), Math.Sin(angle)) * skill.OrbitRadius;
        }

        internal void SweepSegment(double seconds)
        {
            for (int i = 0; i < _objects.Length; i++)
            {
                SkillObject item = _objects[i];
                if (item == null || !item.Active || item.Retire) continue;
                BallCharacter source = _session.FindBall(item.SourceId);
                if (source == null || source.Combat.IsDead) { item.Retire = true; continue; }
                double duration = Math.Min(seconds, item.Skill.Lifetime - item.Age);
                int pieces = item.Skill.Kind == SkillKind.Orbit ? Math.Max(1, (int)Math.Ceiling(Math.Abs(item.Skill.AngularSpeed) * duration / 2)) : 1;
                double firstHit = double.PositiveInfinity;
                for (int n = 0; n < pieces; n++)
                {
                    double startTime = duration * n / pieces, endTime = duration * (n + 1) / pieces;
                    double part = endTime - startTime;
                    if (part <= 0) continue;
                    PlanarVector centerStart = source.Motor.SegmentStart + (source.Motor.Position - source.Motor.SegmentStart) * (startTime / seconds);
                    PlanarVector centerEnd = source.Motor.SegmentStart + (source.Motor.Position - source.Motor.SegmentStart) * (endTime / seconds);
                    PlanarVector start = item.Skill.Kind == SkillKind.Orbit ? OrbitPosition(centerStart, item.Skill, item.Age + startTime, item.AxeIndex) : item.Position;
                    PlanarVector end = item.Skill.Kind == SkillKind.Orbit ? OrbitPosition(centerEnd, item.Skill, item.Age + endTime, item.AxeIndex) : item.Position;
                    double arc = Math.Abs(item.Skill.AngularSpeed) * part * Math.PI / 180;
                    double padding = item.Skill.Kind == SkillKind.Orbit ? item.Skill.OrbitRadius * (1 - Math.Cos(arc / 2)) : 0;
                    for (int e = 0; e < _session.EntityCount; e++)
                    {
                        BallCharacter target = _session.GetEntity(e);
                        if (!CircleProjectileSystem.CanHit(source, target)) continue;
                        int slot = TargetSlot(item, target.EntityId);
                        if (item.Pending[slot] || _session.ElapsedSeconds + 1e-12 < item.NextHit[slot]) continue;
                        PlanarVector velocity = (target.Motor.Position - target.Motor.SegmentStart) / seconds;
                        if (!ProjectileGeometry.Sweep(start, (end - start) / part, item.Skill.Radius + padding,
                            target.Motor.SegmentStart + velocity * startTime, velocity, target.Motor.Radius, part, out double hit)) continue;
                        if (item.Skill.Kind == SkillKind.Trap)
                        {
                            double time = startTime + hit;
                            if (time < firstHit || (time == firstHit && target.EntityId < item.TrapTarget))
                            { firstHit = time; item.TrapTarget = target.EntityId; }
                        }
                        else item.Pending[slot] = true;
                    }
                    item.Position = end;
                }
                item.Age += duration;
                if (item.TrapTarget != 0 || item.Age + 1e-12 >= item.Skill.Lifetime) item.Retire = true;
            }
        }
        private int TargetSlot(SkillObject item, int targetId)
        {
            for (int i = 0; i < item.Targets.Length; i++) if (item.Targets[i] == targetId) return i;
            for (int i = 0; i < item.Targets.Length; i++) if (item.Targets[i] == 0 || _session.FindBall(item.Targets[i]) == null)
            { item.Targets[i] = targetId; item.NextHit[i] = 0; item.Pending[i] = false; return i; }
            throw new InvalidOperationException("技能物命中记录超过实体容量。");
        }
        private bool Hit(long id, int source, int target, SkillDefinition skill, bool heal)
        {
            if (!_session.Settlement.TryResolveSkill(id, source, target, skill.Damage, heal ? skill.HealRatioBps : 0, out DamageResult result)) return false;
            HitCount++; _session.PublishProjectileHit(result, _session.ElapsedSeconds); return true;
        }

        internal void Resolve()
        {
            // 池槽位复用不作为排序依据，固定容量插入排序得到攻击身份顺序。
            int count = 0;
            for (int i = 0; i < _objects.Length; i++) if (_objects[i] != null && _objects[i].Active)
            {
                int at = count;
                while (at > 0 && _objects[_resolveOrder[at - 1]].Id > _objects[i].Id)
                { _resolveOrder[at] = _resolveOrder[at - 1]; at--; }
                _resolveOrder[at] = i; count++;
            }
            for (int k = 0; k < count; k++)
            {
                SkillObject item = _objects[_resolveOrder[k]];
                if (item.TrapTarget != 0) Hit(item.Id, item.SourceId, item.TrapTarget, item.Skill, false);
                for (int e = 0; e < _session.EntityCount && !_session.IsFinished; e++)
                {
                    int targetId = _session.GetEntity(e).EntityId;
                    for (int t = 0; t < item.Targets.Length; t++) if (item.Targets[t] == targetId && item.Pending[t])
                    {
                        Hit(item.Id, item.SourceId, targetId, item.Skill, false);
                        item.NextHit[t] = _session.ElapsedSeconds + item.Skill.HitInterval; item.Pending[t] = false;
                        break;
                    }
                }
                if (item.Retire || _session.FindBall(item.SourceId) == null || _session.FindBall(item.SourceId).Combat.IsDead) Retire(item);
            }
            for (int i = 0; i < _links.Length; i++)
            {
                DrainLink link = _links[i];
                if (link == null) continue;
                if (!LinkValid(link)) { RemoveLink(i); continue; }
                if (link.FirstSkill != null && _session.ElapsedSeconds + 1e-12 >= link.FirstNext)
                { Hit(link.Id, link.First, link.Second, link.FirstSkill, true); link.FirstNext += link.FirstSkill.HitInterval; }
                if (link.SecondSkill != null && _session.ElapsedSeconds + 1e-12 >= link.SecondNext)
                { Hit(link.Id, link.Second, link.First, link.SecondSkill, true); link.SecondNext += link.SecondSkill.HitInterval; }
                if (!LinkValid(link)) RemoveLink(i);
            }
            if (_session.IsFinished) Clear();
        }
        private readonly int[] _resolveOrder = new int[ObjectCapacity];
        private bool LinkValid(DrainLink link)
            => !_session.IsFinished && _session.ElapsedSeconds + 1e-12 < link.ExpiresAt &&
                CircleProjectileSystem.CanHit(_session.FindBall(link.First), _session.FindBall(link.Second)) && link.Joint != null &&
                _session.FindBall(link.First).Motor.ImpulseRevision == link.FirstImpulse &&
                _session.FindBall(link.Second).Motor.ImpulseRevision == link.SecondImpulse;
        internal void BeforeSimulation()
        {
            for (int i = 0; i < _links.Length; i++) if (_links[i] != null && !LinkValid(_links[i])) RemoveLink(i);
            for (int i = 0; i < _session.EntityCount; i++)
            { BallCharacter entity = _session.GetEntity(i); entity.Motor.SpeedMultiplier = entity.Combat.SpeedBuffs.Multiplier; }
        }
        private void RemoveLink(int index)
        {
            DrainLink link = _links[index];
            _session.FindBall(link.First)?.Combat.SpeedBuffs.Remove(link.Id);
            _session.FindBall(link.Second)?.Combat.SpeedBuffs.Remove(link.Id);
            if (link.Joint != null) { link.Joint.enabled = false; UnityEngine.Object.Destroy(link.Joint); }
            _links[index] = null; LinkCount--;
        }
        private void Retire(SkillObject item)
        { if (item.Active) { item.Active = false; item.Skill = null; ActiveCount--; } }
        internal void ExhaustSweepBudget()
        {
            SweepBudgetExceededCount++;
            // 已捕获的命中仍在边界结算；未查询路径不补猜。
            for (int i = 0; i < _objects.Length; i++) if (_objects[i] != null && _objects[i].Active) _objects[i].Retire = true;
        }
        internal void RemoveSkill(int sourceId, string skillId)
        {
            for (int i = 0; i < _objects.Length; i++)
                if (_objects[i] != null && _objects[i].Active && _objects[i].SourceId == sourceId && _objects[i].Skill.Id == skillId) Retire(_objects[i]);
            for (int i = 0; i < _links.Length; i++)
            {
                DrainLink link = _links[i];
                if (link == null) continue;
                if ((link.First == sourceId && link.FirstSkill?.Id == skillId) || (link.Second == sourceId && link.SecondSkill?.Id == skillId)) RemoveLink(i);
            }
        }
        internal void Clear()
        {
            for (int i = 0; i < _objects.Length; i++) if (_objects[i] != null) Retire(_objects[i]);
            for (int i = 0; i < _links.Length; i++) if (_links[i] != null) RemoveLink(i);
        }
        public bool TryGetSnapshot(int slot, out PlanarVector position, out double radius, out int sourceId, out SkillKind kind)
        {
            position = default; radius = 0; sourceId = 0; kind = default;
            if (slot < 0 || slot >= _objects.Length || _objects[slot] == null || !_objects[slot].Active) return false;
            SkillObject item = _objects[slot]; position = item.Position; radius = item.Skill.Radius; sourceId = item.SourceId; kind = item.Skill.Kind;
            return true;
        }
    }
}

