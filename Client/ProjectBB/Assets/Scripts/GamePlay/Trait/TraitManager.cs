using System.Collections.Generic;
using ProjectBB.LogicCore;

namespace ProjectBB.GamePlay
{
    /// <summary>角色级周期能力实例，只由会话结算边界推进；停止或移除不影响共享定义。</summary>
    public sealed class TraitManager
    {
        private readonly BallCharacter _owner;
        private readonly BattleSession _session;
        private readonly PeriodicTraitDefinition[] _definitions;
        private readonly long[] _nextOccurrence;
        private readonly bool[] _removed;
        private readonly double[] _nextEventTime;
        public bool IsStopped { get; private set; }
        public long TriggerCount { get; private set; }
        public long SuccessfulCastCount { get; private set; }
        public long RejectedCastCount { get; private set; }

        internal TraitManager(BallCharacter owner, BattleSession session, IReadOnlyList<PeriodicTraitDefinition> definitions)
        {
            _owner = owner; _session = session;
            int count = definitions == null ? 0 : definitions.Count;
            _definitions = new PeriodicTraitDefinition[count];
            _nextOccurrence = new long[count];
            _removed = new bool[count];
            _nextEventTime = new double[count];
            for (int i = 0; i < count; i++)
            { _definitions[i] = definitions[i]; _nextOccurrence[i] = definitions[i].FireOnStart ? 0 : 1; }
        }

        internal void Tick(double elapsedSeconds)
        {
            if (IsStopped || _session.IsDisposed || _session.IsFinished || _session.IsPaused || _session.IsSuspended) return;
            if (_owner.Combat.IsDead || _owner.Combat.IsPendingDeath) { Stop(); return; }
            for (int i = 0; i < _definitions.Length; i++)
            {
                PeriodicTraitDefinition definition = _definitions[i];
                if (definition.Trigger != "periodic" && definition.Trigger != "onStart") continue;
                if (definition.Trigger == "onStart")
                {
                    if (!_removed[i] && _nextOccurrence[i] != long.MaxValue) { _nextOccurrence[i] = long.MaxValue; Cast(definition, 0, null); }
                    continue;
                }
                if (_removed[i] || elapsedSeconds + 1e-12 < _nextOccurrence[i] * definition.Period) continue;
                // 使用绝对周期序号，子步取整误差不累积为长期漂移；拒绝也消耗本轮，不在下一帧突发重试。
                _nextOccurrence[i]++;
                Cast(definition, 0, null);
            }
        }

        internal void Notify(string trigger, int targetId = 0, PlanarVector? anchor = null)
        {
            if (IsStopped || _owner.Combat.IsDead || _owner.Combat.IsPendingDeath || _session.IsFinished) return;
            for (int i = 0; i < _definitions.Length; i++)
            {
                PeriodicTraitDefinition definition = _definitions[i];
                if (_removed[i] || definition.Trigger != trigger || _session.ElapsedSeconds + 1e-12 < _nextEventTime[i]) continue;
                _nextEventTime[i] = _session.ElapsedSeconds + definition.Period;
                Cast(definition, targetId, anchor);
            }
        }

        private void Cast(PeriodicTraitDefinition definition, int targetId, PlanarVector? anchor)
        {
            TriggerCount++;
            if (_owner.Skills.TryCast(new SkillCastRequest(_session.SessionId, definition.Skill.Id, targetId, anchor), out _)) SuccessfulCastCount++;
            else RejectedCastCount++;
        }

        public bool Remove(string traitId)
        {
            for (int i = 0; i < _definitions.Length; i++)
                if (_definitions[i].Id == traitId && !_removed[i])
                {
                    _removed[i] = true;
                    _session.Abilities.RemoveSkill(_owner.EntityId, _definitions[i].Skill.Id);
                    return true;
                }
            return false;
        }

        internal void Stop() { IsStopped = true; }
    }
}
