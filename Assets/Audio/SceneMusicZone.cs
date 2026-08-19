using UnityEngine;

/// <summary>
/// 区域背景音乐触发器。
/// 挂在一个带 BoxCollider2D（勾选 Is Trigger）的空物体上，
/// 玩家进入区域就切换到指定 BGM，离开时可切回或保持。
/// 用于同一场景内不同地块的氛围音乐（如湿地 / 洞穴 / Boss 区域）。
/// </summary>
public class SceneMusicZone : MonoBehaviour
{
    [Header("进入区域播放的 BGM")]
    [Tooltip("对应 AudioLibrary 的 bgmEntries 里的 key")]
    [SerializeField] private string bgmKey;

    [Tooltip("切换的交叉淡化时长（秒），0 表示立即切换")]
    [SerializeField] private float fadeTime = 2f;

    [Header("离开区域时")]
    [Tooltip("离开时是否切回某个 BGM；留空则保持当前 BGM 不变")]
    [SerializeField] private bool switchOnExit;
    [Tooltip("离开时切回的 BGM key（switchOnExit 为 true 时生效）")]
    [SerializeField] private string exitBgmKey;

    [Header("触发")]
    [Tooltip("检测玩家用的层级名，默认 Player")]
    [SerializeField] private string playerLayer = "Player";

    private int _playerLayerIndex;

    private void Awake()
    {
        _playerLayerIndex = LayerMask.NameToLayer(playerLayer);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.layer != _playerLayerIndex) return;

        if (string.IsNullOrEmpty(bgmKey)) return;

        if (fadeTime <= 0f)
            AudioManager.Instance.PlayBGMByKey(bgmKey);
        else
            AudioManager.Instance.CrossFadeBGM(bgmKey, fadeTime);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!switchOnExit) return;
        if (other.gameObject.layer != _playerLayerIndex) return;
        if (string.IsNullOrEmpty(exitBgmKey)) return;

        if (fadeTime <= 0f)
            AudioManager.Instance.PlayBGMByKey(exitBgmKey);
        else
            AudioManager.Instance.CrossFadeBGM(exitBgmKey, fadeTime);
    }
}
