using System;
using ProjectBB.GamePlay;
using ProjectBB.View;
using UnityEngine;

namespace ProjectBB.SceneInit
{
    /// <summary>
    /// 场景级装配入口，创建双球战斗会话及表现，统一推进并在停用时释放。
    /// 默认加载配置快照；Inspector 参数仅在显式开启原型回归模式时使用。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProjectBootstrap : MonoBehaviour
    {
        [SerializeField] private Camera _battleCamera;
        [Header("配置驱动战斗")]
        [SerializeField] private string _contentResourcePath = "Config/AutoProjectileBattle";
        [SerializeField] private bool _usePrototypeSettings;
        [SerializeField] private GenerationSequenceAsset _generationSequence;
        [Header("运动验证参数（格、秒）")]
        [SerializeField, Min(0.1f)] private float _arenaWidth = 10;
        [SerializeField, Min(0.1f)] private float _arenaHeight = 10;
        [SerializeField, Min(0.01f)] private float _ballRadius = 0.5f;
        [SerializeField, Min(0.01f)] private float _standardSpeed = 3.535534f;
        [SerializeField, Min(0)] private float _speedIndex = 100;
        [SerializeField] private int _randomSeed = 20261010;
        [Header("红方战斗参数")]
        [SerializeField, Min(1)] private int _firstMaxHp = 100;
        [SerializeField, Min(0)] private int _firstCollisionDamage = 10;
        [SerializeField] private bool _firstDamageImmune;
        [Header("蓝方战斗参数")]
        [SerializeField, Min(1)] private int _secondMaxHp = 100;
        [SerializeField, Min(0)] private int _secondCollisionDamage = 10;
        [SerializeField] private bool _secondDamageImmune;
        [Header("结算参数（秒）")]
        [SerializeField, Min(0.01f)] private float _maxBattleSeconds = 180;
        private BattleSession _session;
        private BattlePrototypeView _view;
        private bool _applicationPaused;
        private bool _hasFocus = true;
        private bool _skipNextFrame;

        private void OnEnable()
        {
            // 桌面 vSync 会覆盖 targetFrameRate；正式平台仍须按设备实测 60 FPS。
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            _hasFocus = Application.isFocused;
            RestartBattle();
            if (_session != null) Debug.Log("[ProjectBB] 启动入口已启用。", this);
        }

        private void RestartBattle()
        {
            ReleaseBattle();
            try
            {
                if (_battleCamera == null) throw new InvalidOperationException("请为 ProjectBootstrap 配置 Battle Camera。");
                if (_usePrototypeSettings)
                {
                    var settings = new BattleSettings(_arenaWidth, _arenaHeight, _ballRadius, _standardSpeed, _speedIndex, _randomSeed,
                        firstCombat: new CombatDefinition(_firstMaxHp, _firstCollisionDamage, _firstDamageImmune),
                        secondCombat: new CombatDefinition(_secondMaxHp, _secondCollisionDamage, _secondDamageImmune),
                        maxBattleSeconds: _maxBattleSeconds);
                    _session = new BattleSession(settings);
                }
                else
                {
                    TextAsset asset = Resources.Load<TextAsset>(_contentResourcePath);
                    if (asset == null) throw new InvalidOperationException($"Resources/{_contentResourcePath}.json: 缺失战斗配置。");
                    _session = new BattleSession(BattleContent.Load(asset.text, $"Resources/{_contentResourcePath}.json"));
                    if (_generationSequence != null) _session.SetSequence(_generationSequence.Snapshot(_session.Content));
                }
                _session.SetSuspended(_applicationPaused || !_hasFocus);
                _view = new BattlePrototypeView(_session, _battleCamera, transform, RestartBattle);
                _skipNextFrame = true;
            }
            catch (Exception exception)
            {
                ReleaseBattle();
                Debug.LogException(exception, this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (_session == null || _view == null) return;
            if (_skipNextFrame) _skipNextFrame = false;
            else _session.Advance(Time.unscaledDeltaTime);
            _view.Render();
        }

        private void OnGUI() { _view?.DrawControls(); }

        private void OnApplicationPause(bool paused)
        {
            _applicationPaused = paused;
            ApplySuspension();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            _hasFocus = hasFocus;
            ApplySuspension();
        }

        private void ApplySuspension()
        {
            _session?.SetSuspended(_applicationPaused || !_hasFocus);
            // 恢复后的第一帧时长可能包含后台间隔，直接舍弃。
            _skipNextFrame = true;
        }

        private void OnDisable()
        {
            ReleaseBattle();
            Debug.Log("[ProjectBB] 启动入口已停用。", this);
        }

        private void ReleaseBattle()
        {
            _session?.Dispose();
            _view?.Dispose();
            _view = null;
            _session = null;
        }
    }
}
