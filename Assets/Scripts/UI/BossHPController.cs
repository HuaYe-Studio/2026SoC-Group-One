using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Boss 三段血条 UI：只读取 BossController 的 RemainingSegments / CurrentSegmentHP / SegmentMax。
/// 不读取 _finalHPRemaining，不推算最终残余血量。
/// </summary>
public class BossHPController : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("Boss 血条 Canvas（初始隐藏，首次 HP 刷新后显示）")]
    [SerializeField] private Canvas _bossCanvas;

    [Tooltip("整个 HP 容器 RectTransform（受伤震动作用对象）")]
    [SerializeField] private RectTransform _hpContainer;

    [Tooltip("三段 Fill Image，顺序固定：FillArea_1/Fill、FillArea_2/Fill、FillArea_3/Fill")]
    [SerializeField] private Image[] _segmentFillImages;

    [Header("受伤震动")]
    [Tooltip("小幅震动持续时间（秒）")]
    [SerializeField] private float _shakeDuration = 0.15f;

    [Tooltip("小幅震动强度（锚点位移幅度）")]
    [SerializeField] private float _shakeStrength = 4f;

    private BossController _boss;
    private int _lastCurrentSegmentHP;
    private Vector2 _initialAnchoredPosition;
    private Tween _shakeTween;
    private bool _warningLogged;

    private void Awake()
    {
        if (_bossCanvas != null)
            _bossCanvas.enabled = false;

        _initialAnchoredPosition = _hpContainer != null ? _hpContainer.anchoredPosition : Vector2.zero;
    }

    private void OnEnable()
    {
        TryFindBossAndSubscribe();
    }

    private void OnDisable()
    {
        UnsubscribeBoss();
        KillShake();
    }

    private void OnDestroy()
    {
        UnsubscribeBoss();
        KillShake();
    }

    private void TryFindBossAndSubscribe()
    {
        if (_boss != null)
            return;

        _boss = FindObjectOfType<BossController>(true);
        if (_boss == null)
        {
            if (!_warningLogged)
            {
                Debug.LogWarning("[BossHPController] 未找到 BossController，Boss 血条 UI 保持隐藏。", this);
                _warningLogged = true;
            }
            return;
        }

        _boss.OnHPChanged += HandleHPChanged;
        _boss.OnPhaseChanged += HandlePhaseChanged;
        _boss.OnDefeated += HandleDefeated;

        _lastCurrentSegmentHP = _boss.CurrentSegmentHP;
        RefreshSegments();
    }

    private void UnsubscribeBoss()
    {
        if (_boss == null)
            return;

        _boss.OnHPChanged -= HandleHPChanged;
        _boss.OnPhaseChanged -= HandlePhaseChanged;
        _boss.OnDefeated -= HandleDefeated;
        _boss = null;
    }

    private void HandleHPChanged()
    {
        if (_boss == null)
            return;

        if (_boss.CurrentPhase != BossPhase.Defeated && _bossCanvas != null)
            _bossCanvas.enabled = true;

        RefreshSegments();

        if (_boss.CurrentSegmentHP < _lastCurrentSegmentHP)
            Shake();

        _lastCurrentSegmentHP = _boss.CurrentSegmentHP;
    }

    private void HandlePhaseChanged(BossPhase phase)
    {
        if (_boss == null)
            return;

        if (phase != BossPhase.Defeated && _bossCanvas != null)
            _bossCanvas.enabled = true;

        RefreshSegments();
        _lastCurrentSegmentHP = _boss.CurrentSegmentHP;
    }

    private void HandleDefeated()
    {
        if (_bossCanvas != null)
            _bossCanvas.enabled = false;
    }

    private void RefreshSegments()
    {
        if (_segmentFillImages == null || _boss == null)
            return;

        int remaining = Mathf.Clamp(_boss.RemainingSegments, 0, 3);
        float current = Mathf.Max(0f, _boss.CurrentSegmentHP);
        float max = Mathf.Max(1f, _boss.SegmentMax);
        float ratio = Mathf.Clamp01(current / max);

        for (int i = 0; i < _segmentFillImages.Length; i++)
        {
            if (_segmentFillImages[i] == null)
                continue;

            float fill = i switch
            {
                0 when remaining >= 3 => 1f,
                0 when remaining == 2 => 1f,
                0 when remaining == 1 => ratio,
                0 => 0f,

                1 when remaining >= 3 => 1f,
                1 when remaining == 2 => ratio,
                1 => 0f,

                2 when remaining == 3 => ratio,
                2 => 0f,

                _ => 0f,
            };

            _segmentFillImages[i].fillAmount = Mathf.Clamp01(fill);
        }
    }

    private void Shake()
    {
        if (_hpContainer == null)
            return;

        KillShake();
        _hpContainer.anchoredPosition = _initialAnchoredPosition;

        _shakeTween = _hpContainer
            .DOShakeAnchorPos(_shakeDuration, _shakeStrength, 10, 90f, false, true)
            .OnComplete(() =>
            {
                _hpContainer.anchoredPosition = _initialAnchoredPosition;
                _shakeTween = null;
            });
    }

    private void KillShake()
    {
        if (_shakeTween != null)
        {
            _shakeTween.Kill();
            _shakeTween = null;
        }

        if (_hpContainer != null)
            _hpContainer.anchoredPosition = _initialAnchoredPosition;
    }
}
