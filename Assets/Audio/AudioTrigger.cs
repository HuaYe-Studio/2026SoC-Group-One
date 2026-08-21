using UnityEngine;

/// <summary>
/// 交互音效触发器。挂在任意物体上，玩家触发时播放音效。
/// 支持被动（进入范围）和主动（按 E 键）两种触发方式。
/// </summary>
public class AudioTrigger : MonoBehaviour
{
    public enum TriggerMode
    {
        /// <summary>被动：玩家进入触发区域即播放（如告示牌提示音、环境音）</summary>
        OnEnter,
        /// <summary>被动：玩家离开触发区域时播放</summary>
        OnExit,
        /// <summary>主动：玩家在区域内按 E 键才播放（如开门、拉杆）</summary>
        OnInteract
    }

    [Header("音效")]
    [Tooltip("对应 AudioLibrary 的 sfxEntries 里的 key")]
    [SerializeField] private string sfxKey;

    [Header("触发方式")]
    [SerializeField] private TriggerMode mode = TriggerMode.OnEnter;

    [Header("触发设置")]
    [Tooltip("玩家层级名，默认 Player")]
    [SerializeField] private string playerLayer = "Player";

    [Tooltip("同一音效的最小播放间隔（秒），防止频繁触发。0 表示不限制")]
    [SerializeField] private float minInterval = 0.1f;

    [Tooltip("只播放一次（播放后禁用本组件）。用于一次性交互，如拾取道具")]
    [SerializeField] private bool playOnce;

    [Header("主动触发（mode = OnInteract 时生效）")]
    [Tooltip("是否只在玩家位于区域内时才能按 E 触发")]
    [SerializeField] private bool requireInRange = true;
    [Tooltip("主动触发的检测半径（世界单位），玩家在范围内才能触发")]
    [SerializeField] private float interactRadius = 2f;

    private int _playerLayerIndex;
    private float _lastPlayTime = -999f;
    private bool _playerInRange;
    private Transform _player;

    private void Awake()
    {
        _playerLayerIndex = LayerMask.NameToLayer(playerLayer);
    }

    private void OnEnable()
    {
        if (mode == TriggerMode.OnInteract)
        {
            if (PlayerInputReader.HasInstance)
                PlayerInputReader.Instance.OnInteract += HandleInteract;
        }
    }

    private void OnDisable()
    {
        if (PlayerInputReader.HasInstance)
            PlayerInputReader.Instance.OnInteract -= HandleInteract;
    }

    private void Update()
    {
        if (mode != TriggerMode.OnInteract) return;
        if (!requireInRange) return;

        UpdatePlayerInRange();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.layer != _playerLayerIndex) return;

        if (mode == TriggerMode.OnEnter)
            TryPlay();
        else if (mode == TriggerMode.OnInteract)
            _playerInRange = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.gameObject.layer != _playerLayerIndex) return;

        if (mode == TriggerMode.OnExit)
            TryPlay();
        else if (mode == TriggerMode.OnInteract)
            _playerInRange = false;
    }

    private void HandleInteract()
    {
        if (mode != TriggerMode.OnInteract) return;

        if (requireInRange)
        {
            UpdatePlayerInRange();
            if (!_playerInRange) return;
        }

        TryPlay();
    }

    private void UpdatePlayerInRange()
    {
        if (_player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            _player = go != null ? go.transform : null;
        }
        if (_player == null)
        {
            _playerInRange = false;
            return;
        }

        _playerInRange = Vector2.Distance(_player.position, transform.position) <= interactRadius;
    }

    private void TryPlay()
    {
        if (string.IsNullOrEmpty(sfxKey)) return;

        if (minInterval > 0f && Time.time - _lastPlayTime < minInterval)
            return;

        _lastPlayTime = Time.time;

        AudioManager.Instance.PlaySfxByKey(sfxKey);

        if (playOnce)
            enabled = false;
    }
}
