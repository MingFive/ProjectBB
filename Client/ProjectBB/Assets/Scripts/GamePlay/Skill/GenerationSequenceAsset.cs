using System;
using System.Collections.Generic;
using ProjectBB.LogicCore;
using UnityEngine;

namespace ProjectBB.GamePlay
{
    /// <summary>仅保存生成组的时间和技能引用，数值继续由 JSON 提供；每个会话复制不可变快照。</summary>
    [CreateAssetMenu(menuName = "ProjectBB/生成组时间线", fileName = "GenerationSequence")]
    public sealed class GenerationSequenceAsset : ScriptableObject
    {
        [Serializable]
        public sealed class Cue
        {
            [Min(0)] public float timeSeconds;
            [Range(1, 2)] public int casterId = 1;
            public string skillId;
        }
        [SerializeField] private List<Cue> _cues = new List<Cue>();
        public GenerationCue[] Snapshot(BattleContent content)
        {
            if (content == null || _cues.Count > 128) throw new ArgumentException("时间线要求有效配置且最多 128 个生成组。");
            var result = new GenerationCue[_cues.Count];
            for (int i = 0; i < result.Length; i++)
            {
                Cue cue = _cues[i];
                if (cue == null || !PlanarVector.IsFiniteNumber(cue.timeSeconds) || cue.timeSeconds < 0 || cue.timeSeconds > 600 ||
                    (cue.casterId != 1 && cue.casterId != 2) || !content.TryGetSkill(cue.skillId, out _))
                    throw new ArgumentException($"生成组 [{i}] 时间、角色或技能引用无效。");
                result[i] = new GenerationCue(cue.timeSeconds, cue.casterId, cue.skillId);
            }
            // 稳定插入排序：同刻保持制作顺序。
            for (int i = 1; i < result.Length; i++)
            {
                GenerationCue value = result[i]; int at = i;
                while (at > 0 && result[at - 1].Time > value.Time) { result[at] = result[at - 1]; at--; }
                result[at] = value;
            }
            return result;
        }
    }

    /// <summary>生成组的运行快照，实例进度归会话，不修改 SO。</summary>
    public readonly struct GenerationCue
    {
        public double Time { get; }
        public int CasterId { get; }
        public string SkillId { get; }
        public GenerationCue(double time, int casterId, string skillId) { Time = time; CasterId = casterId; SkillId = skillId; }
    }
}
