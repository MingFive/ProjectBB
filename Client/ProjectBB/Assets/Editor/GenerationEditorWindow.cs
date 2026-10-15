using System;
using ProjectBB.GamePlay;
using ProjectBB.LogicCore;
using UnityEditor;
using UnityEngine;

namespace ProjectBB.Editor
{
    /// <summary>生成组制作入口：SO 只编辑时间/引用；JSON 只读，战斗预览复用独立正式会话。</summary>
    public sealed class GenerationEditorWindow : EditorWindow
    {
        private TextAsset _json;
        private GenerationSequenceAsset _sequence;
        private BattleContent _content;
        private BattleSession _preview;
        private SerializedObject _serialized;
        private Vector2 _scroll;
        private double _lastTime;
        private string _error;
        private float _cursor;
        [MenuItem("ProjectBB/技能/生成组编辑器")]
        private static void Open() { GetWindow<GenerationEditorWindow>("生成组编辑器"); }
        private void OnEnable()
        {
            _json = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Config/SummonBattle.json");
            EditorApplication.update += UpdatePreview;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
            ReloadContent();
        }
        private void OnDisable()
        {
            StopPreview();
            EditorApplication.update -= UpdatePreview;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= StopPreview;
        }
        private void OnPlayModeChanged(PlayModeStateChange state)
        { if (state != PlayModeStateChange.EnteredPlayMode) StopPreview(); }
        private void ReloadContent()
        {
            StopPreview();
            try { _content = _json == null ? null : BattleContent.Load(_json.text, AssetDatabase.GetAssetPath(_json)); _error = null; }
            catch (Exception exception) { _content = null; _error = exception.Message; }
        }
        private void OnGUI()
        {
            EditorGUILayout.LabelField("生成组 · 时间线与二维预览", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("JSON 提供技能数值、数量、扇形/圆环规则；SO 保存每组的时间与技能引用。多波生成使用多条时间记录。", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            _json = (TextAsset)EditorGUILayout.ObjectField("战斗配置（只读）", _json, typeof(TextAsset), false);
            if (EditorGUI.EndChangeCheck()) ReloadContent();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("重新校验配置")) ReloadContent();
            if (GUILayout.Button("定位 JSON") && _json != null) EditorGUIUtility.PingObject(_json);
            EditorGUILayout.EndHorizontal();
            EditorGUI.BeginChangeCheck();
            _sequence = (GenerationSequenceAsset)EditorGUILayout.ObjectField("生成组时间线", _sequence, typeof(GenerationSequenceAsset), false);
            if (EditorGUI.EndChangeCheck()) { StopPreview(); _serialized = null; }
            if (_sequence == null && GUILayout.Button("创建时间线 SO"))
            {
                string path = EditorUtility.SaveFilePanelInProject("创建生成组时间线", "GenerationSequence", "asset", "选择时间线保存位置");
                if (!string.IsNullOrEmpty(path))
                {
                    _sequence = CreateInstance<GenerationSequenceAsset>();
                    AssetDatabase.CreateAsset(_sequence, path); AssetDatabase.SaveAssets();
                }
            }
            if (_sequence != null)
            {
                if (_serialized == null || _serialized.targetObject != _sequence) _serialized = new SerializedObject(_sequence);
                _serialized.Update();
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(220));
                EditorGUILayout.PropertyField(_serialized.FindProperty("_cues"), new GUIContent("生成组（秒 / 主球 ID / 技能 ID）"), true);
                EditorGUILayout.EndScrollView();
                if (_serialized.ApplyModifiedProperties()) StopPreview();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("校验引用"))
                {
                    try { _sequence.Snapshot(_content); _error = null; }
                    catch (Exception exception) { _error = exception.Message; }
                }
                if (GUILayout.Button("保存 SO")) AssetDatabase.SaveAssets();
                EditorGUILayout.EndHorizontal();
            }
            if (_error != null) EditorGUILayout.HelpBox(_error, MessageType.Error);
            _cursor = EditorGUILayout.Slider("内容预览时间（秒）", _cursor, 0, 60);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || _content == null))
            {
                if (GUILayout.Button("重建并播放战斗预览")) StartPreview();
                if (GUILayout.Button(_preview != null && _preview.IsPaused ? "继续" : "暂停") && _preview != null) _preview.SetPaused(!_preview.IsPaused);
                if (GUILayout.Button("1× / 2×") && _preview != null) _preview.SetTimeScale(_preview.TimeScale == 1 ? 2 : 1);
            }
            if (GUILayout.Button("停止预览")) StopPreview();
            EditorGUILayout.EndHorizontal();
            if (!EditorApplication.isPlaying) EditorGUILayout.LabelField("战斗预览需进入 Play Mode；编辑模式只显示内容几何。");
            Rect canvas = GUILayoutUtility.GetRect(100, 340, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(canvas, new Color(0.035f, 0.05f, 0.075f));
            if (_content != null) DrawCanvas(canvas);
            if (_preview != null)
                EditorGUILayout.LabelField($"时间 {_preview.ElapsedSeconds:F2}s  实体 {_preview.EntityCount}  弹道 {_preview.Projectiles.ActiveCount}  技能物 {_preview.Abilities.ActiveCount}\n查询 {_preview.Projectiles.CandidateQueryCount} / 精查 {_preview.Projectiles.NarrowPhaseCount}  物理细段 {_preview.Collisions.PhysicsSliceCount}  拒绝 {_preview.SequenceRejectedCount}/{_preview.SummonRejectedCount}", GUILayout.Height(42));
            if (_content != null)
                foreach (SkillDefinition skill in _content.Skills)
                    EditorGUILayout.LabelField($"{skill.Id} · {skill.Kind} · 每次 {skill.Count} 个 · 展开 {skill.SpreadDegrees}° · 半径 {skill.Radius} · 寿命 {skill.Lifetime}s");
        }
        private void StartPreview()
        {
            StopPreview();
            try
            {
                _preview = new BattleSession(_content);
                if (_sequence != null) _preview.SetSequence(_sequence.Snapshot(_content));
                _lastTime = EditorApplication.timeSinceStartup; _error = null;
            }
            catch (Exception exception) { _error = exception.Message; StopPreview(); }
        }
        private void StopPreview() { _preview?.Dispose(); _preview = null; }
        private void UpdatePreview()
        {
            if (_preview == null) return;
            double now = EditorApplication.timeSinceStartup;
            try { _preview.Advance(Math.Min(0.1, now - _lastTime)); _lastTime = now; Repaint(); }
            catch (Exception exception) { _error = exception.Message; StopPreview(); }
        }
        private void DrawCanvas(Rect rect)
        {
            float scale = (float)Math.Min((rect.width - 20) / _content.Settings.Bounds.Width, (rect.height - 20) / _content.Settings.Bounds.Height);
            Vector3 Map(PlanarVector point) => new Vector3(rect.center.x + (float)point.X * scale, rect.center.y - (float)point.Y * scale, 0);
            void Circle(PlanarVector point, double radius, Color color)
            { Handles.color = color; Handles.DrawWireDisc(Map(point), Vector3.forward, (float)radius * scale); }
            Handles.BeginGUI();
            if (_preview != null)
            {
                for (int i = 0; i < _preview.EntityCount; i++)
                {
                    BallCharacter entity = _preview.GetEntity(i);
                    Circle(entity.Motor.Position, entity.Motor.Radius, entity.FactionId == 1 ? Color.red : Color.cyan);
                }
                for (int i = 0; i < _preview.Projectiles.ActiveCount; i++)
                { CircleProjectileSnapshot flight = _preview.Projectiles.GetSnapshot(i); Circle(flight.Position, flight.Radius, Color.yellow); }
                for (int i = 0; i < AbilitySystem.ObjectCapacity; i++)
                    if (_preview.Abilities.TryGetSnapshot(i, out PlanarVector p, out double radius, out _, out _)) Circle(p, radius, Color.green);
            }
            else if (_sequence != null)
            {
                try
                {
                    foreach (GenerationCue cue in _sequence.Snapshot(_content))
                    {
                        if (cue.Time > _cursor || _cursor - cue.Time > 1) continue;
                        _content.TryGetSkill(cue.SkillId, out SkillDefinition skill);
                        var arena = new ArenaManager(_content.Settings.Bounds);
                        PlanarVector caster = arena.GetSpawnPosition(cue.CasterId == 1, _content.Settings.Radius);
                        PlanarVector target = arena.GetSpawnPosition(cue.CasterId != 1, _content.Settings.Radius);
                        PlanarVector anchor = skill.Launch.SpawnAnchor == ProjectileSpawnAnchor.Caster ? caster :
                            skill.Launch.SpawnAnchor == ProjectileSpawnAnchor.Target ? target : default;
                        PlanarVector spawn = _content.Settings.Bounds.Clamp(anchor + skill.Launch.SpawnOffset, skill.Radius);
                        double angle = skill.Launch.DirectionAngleDegrees * Math.PI / 180;
                        PlanarVector forward = skill.Launch.DirectionMode == ProjectileDirectionMode.FixedAngle
                            ? new PlanarVector(Math.Cos(angle), Math.Sin(angle)) : (target - spawn).NormalizedOr(new PlanarVector(1, 0));
                        if (skill.Launch.DirectionMode == ProjectileDirectionMode.CasterMovement) forward = new PlanarVector(cue.CasterId == 1 ? 1 : -1, 0);
                        if (skill.Launch.RandomDirection)
                        {
                            Circle(spawn, 1.5, Color.gray);
                            Handles.Label(Map(spawn), "随机方向：实际种子轨迹见战斗预览");
                            continue;
                        }
                        for (int i = 0; i < skill.Count; i++)
                        {
                            PlanarVector direction = GenerationGeometry.Direction(forward, i, skill.Count, skill.SpreadDegrees);
                            PlanarVector start = skill.Kind == SkillKind.Summon && skill.Launch.SpawnAnchor == ProjectileSpawnAnchor.Caster && skill.Launch.SpawnOffset.LengthSquared < 1e-24
                                ? caster + direction * (_content.Settings.Radius + skill.Radius + 0.05) : spawn;
                            Circle(start, skill.Radius, Color.yellow);
                            Handles.DrawLine(Map(start), Map(start + direction * 1.5));
                        }
                    }
                }
                catch (Exception exception) { _error = exception.Message; }
            }
            Handles.EndGUI();
        }
    }
}

