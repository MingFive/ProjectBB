using System;
using System.Collections.Generic;
using ProjectBB.GamePlay;
using ProjectBB.LogicCore;
using UnityEngine;

namespace ProjectBB.View
{
    /// <summary>基础战斗的临时表现，只消费位置、承伤和结算事实，释放时完整回收。</summary>
    public sealed class BattlePrototypeView : IDisposable
    {
        private readonly BattleSession _session;
        private readonly Camera _camera;
        private readonly Action _restart;
        private readonly Vector3 _originalPosition;
        private readonly Quaternion _originalRotation;
        private readonly bool _originalOrthographic;
        private readonly float _originalSize;
        private readonly Color _originalBackground;
        private readonly CameraClearFlags _originalClearFlags;
        private GameObject _root;
        private Transform _firstBall;
        private Transform _secondBall;
        private Texture2D _circleTexture;
        private Sprite _circleSprite;
        private Sprite _squareSprite;
        private bool _disposed;
        private GUIStyle _statusStyle;
        private GUIStyle _damageStyle;
        private readonly List<DamagePopup> _popups = new List<DamagePopup>(32);
        private SpriteRenderer _firstRenderer;
        private SpriteRenderer _secondRenderer;
        private readonly List<SpriteRenderer> _projectileRenderers = new List<SpriteRenderer>(CircleProjectileSystem.Capacity);
        private readonly List<SpriteRenderer> _summonRenderers = new List<SpriteRenderer>(BattleSession.EntityCapacity);
        private readonly List<SpriteRenderer> _abilityRenderers = new List<SpriteRenderer>(AbilitySystem.ObjectCapacity);
        private double _firstHitTime = double.NegativeInfinity;
        private double _secondHitTime = double.NegativeInfinity;
        private static readonly Color _firstColor = new Color(0.96f, 0.28f, 0.35f);
        private static readonly Color _secondColor = new Color(0.22f, 0.59f, 1f);

        // 有界短生命周期反馈；不保存角色引用，不写回承伤状态。
        private readonly struct DamagePopup
        {
            public readonly string Text;
            public readonly Vector3 Position;
            public readonly double StartedAt;
            public DamagePopup(int amount, PlanarVector position, double startedAt)
            {
                Text = "-" + amount;
                Position = new Vector3((float)position.X - 0.7f, (float)position.Y + 0.7f, 0);
                StartedAt = startedAt;
            }
        }

        public BattlePrototypeView(BattleSession session, Camera camera, Transform parent, Action restart)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            _camera = camera;
            _restart = restart;
            _originalPosition = camera.transform.position;
            _originalRotation = camera.transform.rotation;
            _originalOrthographic = camera.orthographic;
            _originalSize = camera.orthographicSize;
            _originalBackground = camera.backgroundColor;
            _originalClearFlags = camera.clearFlags;
            try
            {
                _root = new GameObject("BattleView");
                _root.transform.SetParent(parent, false);
                CreateSprites();
                CreateArena();
                _firstBall = CreateSprite("Ball_A", _circleSprite, _firstColor, 10);
                _secondBall = CreateSprite("Ball_B", _circleSprite, _secondColor, 10);
                _firstRenderer = _firstBall.GetComponent<SpriteRenderer>();
                _secondRenderer = _secondBall.GetComponent<SpriteRenderer>();
                _firstBall.localScale = Vector3.one * (float)(session.FirstBall.Motor.Radius * 2);
                _secondBall.localScale = Vector3.one * (float)(session.SecondBall.Motor.Radius * 2);
                camera.orthographic = true;
                camera.transform.SetPositionAndRotation(new Vector3(0, 0, -10), Quaternion.identity);
                camera.backgroundColor = new Color(0.035f, 0.05f, 0.075f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                Render();
            }
            catch { Dispose(); throw; }
        }

        private void CreateSprites()
        {
            const int size = 96;
            _circleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            _circleTexture.name = "BattleCircle_Runtime";
            _circleTexture.filterMode = FilterMode.Bilinear;
            _circleTexture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f).magnitude;
                byte alpha = (byte)(Mathf.Clamp01(size * 0.5f - 0.5f - distance) * 255);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
            _circleTexture.SetPixels32(pixels);
            _circleTexture.Apply(false, true);
            _circleSprite = Sprite.Create(_circleTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
            _circleSprite.name = "BattleCircle_Runtime";
            // 白纹理由 Unity 持有，仅释放本次创建的 Sprite。
            Texture2D white = Texture2D.whiteTexture;
            _squareSprite = Sprite.Create(white, new Rect(0, 0, white.width, white.height), new Vector2(0.5f, 0.5f), white.width);
            _squareSprite.name = "BattleSquare_Runtime";
        }

        private Transform CreateSprite(string name, Sprite sprite, Color color, int order)
        {
            var item = new GameObject(name);
            item.transform.SetParent(_root.transform, false);
            var renderer = item.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return item.transform;
        }

        private void CreateRectangle(string name, float x, float y, float width, float height, Color color, int order)
        {
            Transform item = CreateSprite(name, _squareSprite, color, order);
            item.localPosition = new Vector3(x, y, 0);
            item.localScale = new Vector3(width, height, 1);
        }

        private void CreateArena()
        {
            float width = (float)_session.Arena.Bounds.Width;
            float height = (float)_session.Arena.Bounds.Height;
            CreateRectangle("ArenaFloor", 0, 0, width, height, new Color(0.075f, 0.1f, 0.15f), 0);
            Color grid = new Color(0.24f, 0.31f, 0.4f, 0.32f);
            // 表现网格有数量预算，大场地不按尺寸无限生成对象。
            int columns = Mathf.Min(32, Mathf.CeilToInt(width));
            int rows = Mathf.Min(32, Mathf.CeilToInt(height));
            for (int i = 1; i < columns; i++) CreateRectangle("GridColumn", -width * 0.5f + width * i / columns, 0, 0.012f, height, grid, 1);
            for (int i = 1; i < rows; i++) CreateRectangle("GridRow", 0, -height * 0.5f + height * i / rows, width, 0.012f, grid, 1);
            Color border = new Color(0.42f, 0.54f, 0.7f);
            CreateRectangle("WallLeft", -width * 0.5f, 0, 0.055f, height, border, 2);
            CreateRectangle("WallRight", width * 0.5f, 0, 0.055f, height, border, 2);
            CreateRectangle("WallBottom", 0, -height * 0.5f, width, 0.055f, border, 2);
            CreateRectangle("WallTop", 0, height * 0.5f, width, 0.055f, border, 2);
        }

        public void Render()
        {
            if (_disposed) return;
            if (_camera != null)
            {
                float height = (float)_session.Arena.Bounds.Height + 3f;
                float width = (float)_session.Arena.Bounds.Width + 2f;
                _camera.orthographicSize = Mathf.Max(height * 0.5f, width * 0.5f / Mathf.Max(0.01f, _camera.aspect));
            }
            ProjectPosition(_firstBall, _session.FirstBall.Motor);
            ProjectPosition(_secondBall, _session.SecondBall.Motor);
            while (_session.TryDequeueFeedback(out CollisionBatchResult batch))
            {
                AddFeedback(batch.FirstToSecond, batch.TimeSeconds);
                AddFeedback(batch.SecondToFirst, batch.TimeSeconds);
            }
            while (_session.TryDequeueProjectileFeedback(out ProjectileHitResult hit)) AddFeedback(hit.Damage, hit.TimeSeconds);
            RenderProjectiles();
            RenderSummonsAndSkills();
            for (int i = _popups.Count - 1; i >= 0; i--)
                if (_session.PresentationSeconds - _popups[i].StartedAt >= 0.8) _popups.RemoveAt(i);
            RenderHit(_firstRenderer, _firstColor, _firstHitTime, _session.FirstBall.Combat.IsDead);
            RenderHit(_secondRenderer, _secondColor, _secondHitTime, _session.SecondBall.Combat.IsDead);
        }

        private void RenderProjectiles()
        {
            int count = _session.Projectiles.ActiveCount;
            while (_projectileRenderers.Count < count)
                _projectileRenderers.Add(CreateSprite("CircleProjectile", _circleSprite, Color.white, 12).GetComponent<SpriteRenderer>());
            for (int i = 0; i < _projectileRenderers.Count; i++)
            {
                SpriteRenderer renderer = _projectileRenderers[i];
                renderer.gameObject.SetActive(i < count);
                if (i >= count) continue;
                CircleProjectileSnapshot flight = _session.Projectiles.GetSnapshot(i);
                renderer.color = _session.FindBall(flight.SourceId)?.FactionId == _session.FirstBall.FactionId ? _firstColor : _secondColor;
                renderer.transform.position = new Vector3((float)flight.Position.X, (float)flight.Position.Y, 0);
                renderer.transform.localScale = Vector3.one * (float)(flight.Radius * 2);
            }
        }

        private void RenderSummonsAndSkills()
        {
            int count = Math.Max(0, _session.EntityCount - 2);
            while (_summonRenderers.Count < count)
                _summonRenderers.Add(CreateSprite("Summon", _circleSprite, Color.white, 10).GetComponent<SpriteRenderer>());
            for (int i = 0; i < _summonRenderers.Count; i++)
            {
                SpriteRenderer renderer = _summonRenderers[i]; renderer.gameObject.SetActive(i < count);
                if (i >= count) continue;
                BallCharacter entity = _session.GetEntity(i + 2);
                renderer.color = Color.Lerp(entity.FactionId == _session.FirstBall.FactionId ? _firstColor : _secondColor, Color.white, 0.4f);
                renderer.transform.localScale = Vector3.one * (float)(entity.Motor.Radius * 2);
                ProjectPosition(renderer.transform, entity.Motor);
            }
            int shown = 0;
            for (int i = 0; i < AbilitySystem.ObjectCapacity; i++)
            {
                if (!_session.Abilities.TryGetSnapshot(i, out PlanarVector position, out double radius, out int source, out SkillKind kind)) continue;
                if (_abilityRenderers.Count == shown) _abilityRenderers.Add(CreateSprite("SkillObject", _circleSprite, Color.white, 11).GetComponent<SpriteRenderer>());
                SpriteRenderer renderer = _abilityRenderers[shown++]; renderer.gameObject.SetActive(true);
                Color faction = _session.FindBall(source)?.FactionId == _session.FirstBall.FactionId ? _firstColor : _secondColor;
                renderer.color = kind == SkillKind.Trap ? Color.Lerp(faction, Color.green, 0.6f) : Color.Lerp(faction, Color.yellow, 0.5f);
                renderer.transform.position = new Vector3((float)position.X, (float)position.Y, 0);
                renderer.transform.localScale = Vector3.one * (float)(radius * 2);
            }
            for (int i = shown; i < _abilityRenderers.Count; i++) _abilityRenderers[i].gameObject.SetActive(false);
        }

        private void Fire(BallCharacter source, BallCharacter target)
        {
            if (_session.Content != null)
            {
                BallContentDefinition content = source.EntityId == _session.FirstBall.EntityId ? _session.Content.FirstBall : _session.Content.SecondBall;
                if (content.Traits.Count > 0) source.Skills.TryCast(new SkillCastRequest(_session.SessionId, content.Traits[0].Skill.Id), out _);
                return;
            }
            PlanarVector direction = (target.Motor.Position - source.Motor.Position).NormalizedOr(source.Motor.Direction);
            _session.TryLaunchProjectile(new CircleProjectileRequest(_session.SessionId, source.EntityId,
                source.Motor.Position, direction * 80), out _);
        }

        private void AddFeedback(DamageResult result, double timeSeconds)
        {
            if (!result.Accepted || result.HpLoss <= 0) return;
            if (result.Request.TargetId == _session.FirstBall.EntityId) _firstHitTime = timeSeconds;
            else if (result.Request.TargetId == _session.SecondBall.EntityId) _secondHitTime = timeSeconds;
            if (_popups.Count == 32) _popups.RemoveAt(0);
            _popups.Add(new DamagePopup(result.HpLoss, result.TargetPosition, timeSeconds));
        }

        private void RenderHit(SpriteRenderer renderer, Color baseColor, double hitTime, bool dead)
        {
            if (renderer == null) return;
            float flash = Mathf.Clamp01(1 - (float)(_session.PresentationSeconds - hitTime) / 0.12f);
            renderer.color = Color.Lerp(dead ? baseColor * 0.45f : baseColor, Color.white, flash);
        }

        private void ProjectPosition(Transform target, BallMotor motor)
        {
            if (target == null) return;
            PlanarVector position = motor.PreviousPosition + (motor.Position - motor.PreviousPosition) * _session.InterpolationAlpha;
            target.position = new Vector3((float)position.X, (float)position.Y, 0);
        }

        /// <summary>临时验证控件由装配层 OnGUI 调用，操作只进入会话明确入口。</summary>
        public void DrawControls()
        {
            if (_disposed) return;
            if (_statusStyle == null) _statusStyle = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            if (_damageStyle == null)
            {
                _damageStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                _damageStyle.normal.textColor = Color.white;
            }
            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && !_session.IsFinished;
            if (GUI.Button(new Rect(16, 16, 84, 32), _session.IsPaused ? "继续" : "暂停")) _session.SetPaused(!_session.IsPaused);
            if (GUI.Button(new Rect(200, 16, 74, 32), _session.TimeScale == 1 ? "1×" : "2×")) _session.SetTimeScale(_session.TimeScale == 1 ? 2 : 1);
            GUI.enabled = wasEnabled && !_session.IsFinished && !_session.IsPaused && !_session.IsSuspended;
            if (GUI.Button(new Rect(282, 16, 100, 32), "红方发射")) Fire(_session.FirstBall, _session.SecondBall);
            if (GUI.Button(new Rect(390, 16, 100, 32), "蓝方发射")) Fire(_session.SecondBall, _session.FirstBall);
            GUI.enabled = wasEnabled;
            if (GUI.Button(new Rect(108, 16, 84, 32), "重新开始")) { _restart?.Invoke(); return; }
            GUI.Label(new Rect(16, 56, 600, 28), $"基础对战  ·  {_session.ElapsedSeconds:F1}/{_session.MaxBattleSeconds:F1}s  ·  接触 {_session.Collisions.ContactRound}", _statusStyle);
            if (_session.IsFinished)
            {
                string result = _session.Settlement.Outcome == BattleOutcome.Draw ? "平局" :
                    _session.Settlement.WinnerEntityId == _session.FirstBall.EntityId ? "红方胜利" : "蓝方胜利";
                string reason = _session.Settlement.EndReason == BattleEndReason.Timeout ? "超时：按剩余生命比例" : "生命耗尽";
                GUI.Label(new Rect(16, 84, 600, 28), $"{result}  ·  {reason}", _statusStyle);
            }
            if (_session.CatchUpLimitCount > 0)
                GUI.Label(new Rect(16, 112, 600, 28), $"补步预算触发 {_session.CatchUpLimitCount} 次，已舍弃积压时间。", _statusStyle);
            GUI.Label(new Rect(16, 140, 900, 28), $"弹道 {_session.Projectiles.ActiveCount}/{_session.Projectiles.Limit}  ·  召唤 {_session.EntityCount - 2}  ·  技能物 {_session.Abilities.ActiveCount}  ·  贴附 {_session.Abilities.LinkCount}  ·  容量拒绝 {_session.Projectiles.CapacityRejectedCount}/{_session.SummonRejectedCount}", _statusStyle);
            if (_session.Content != null)
                GUI.Label(new Rect(16, 168, 700, 28), $"自动释放 红 {_session.FirstBall.Traits.SuccessfulCastCount} / 蓝 {_session.SecondBall.Traits.SuccessfulCastCount}  ·  重开读取配置", _statusStyle);
            DrawHealth(_firstBall, _session.FirstBall.Combat, _firstColor);
            DrawHealth(_secondBall, _session.SecondBall.Combat, _secondColor);
            for (int i = 2; i < _session.EntityCount; i++)
                DrawHealth(_summonRenderers[i - 2].transform, _session.GetEntity(i).Combat, _session.GetEntity(i).FactionId == _session.FirstBall.FactionId ? _firstColor : _secondColor);
            DrawDamage();
        }

        private void DrawHealth(Transform target, CombatManager combat, Color color)
        {
            if (_camera == null || target == null) return;
            Vector3 screen = _camera.WorldToScreenPoint(target.position + Vector3.up * 0.85f);
            if (screen.z <= 0) return;
            var bar = new Rect(screen.x - 40, Screen.height - screen.y, 80, 8);
            Color saved = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(bar.x + 1, bar.y + 1, 78f * combat.CurrentHp / combat.MaxHp, 6), Texture2D.whiteTexture);
            GUI.color = saved;
            GUI.Label(new Rect(bar.x, bar.y - 23, 120, 24), $"{combat.CurrentHp}/{combat.MaxHp}", _statusStyle);
        }

        private void DrawDamage()
        {
            if (_camera == null) return;
            Color saved = GUI.color;
            foreach (DamagePopup popup in _popups)
            {
                float age = (float)(_session.PresentationSeconds - popup.StartedAt);
                Vector3 screen = _camera.WorldToScreenPoint(popup.Position + Vector3.up * age);
                if (screen.z <= 0) continue;
                GUI.color = new Color(1, 1, 1, Mathf.Clamp01(1 - age / 0.8f));
                GUI.Label(new Rect(screen.x - 40, Screen.height - screen.y - 20, 80, 32), popup.Text, _damageStyle);
            }
            GUI.color = saved;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _popups.Clear();
            _projectileRenderers.Clear();
            if (_root != null) { _root.SetActive(false); UnityEngine.Object.Destroy(_root); }
            if (_circleSprite != null) UnityEngine.Object.Destroy(_circleSprite);
            if (_squareSprite != null) UnityEngine.Object.Destroy(_squareSprite);
            if (_circleTexture != null) UnityEngine.Object.Destroy(_circleTexture);
            if (_camera != null)
            {
                _camera.transform.SetPositionAndRotation(_originalPosition, _originalRotation);
                _camera.orthographic = _originalOrthographic;
                _camera.orthographicSize = _originalSize;
                _camera.backgroundColor = _originalBackground;
                _camera.clearFlags = _originalClearFlags;
            }
        }
    }
}
