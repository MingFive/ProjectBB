using System;
using ProjectBB.LogicCore;

namespace ProjectBB.GamePlay
{
    /// <summary>当前圆形技能的结构化释放请求；角色由 SkillManager 固定，旧会话请求不能跨重开使用。</summary>
    public readonly struct SkillCastRequest
    {
        public Guid SessionId { get; }
        public string SkillId { get; }
        public int TargetId { get; }
        public PlanarVector? Anchor { get; }
        public SkillCastRequest(Guid sessionId, string skillId, int targetId = 0, PlanarVector? anchor = null)
        { SessionId = sessionId; SkillId = skillId; TargetId = targetId; Anchor = anchor; }
    }
}
