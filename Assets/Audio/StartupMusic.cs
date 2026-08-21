using UnityEngine;

/// <summary>
/// 场景启动时自动播放 BGM。
/// 挂在一个 GameObject 上（通常和 AudioManager 同物体），
/// 场景加载完成后自动播指定 BGM key。
/// 用于主菜单、关卡等需要固定背景音乐的场景。
/// </summary>
public class StartupMusic : MonoBehaviour
{
    [Header("自动播放的 BGM")]
    [Tooltip("对应 AudioLibrary 的 bgmEntries 里的 key")]
    [SerializeField] private string bgmKey = "wetland";

    [Tooltip("播放前的延迟（秒），0 表示立即播放")]
    [SerializeField] private float delay = 0f;

    private void Start()
    {
        if (string.IsNullOrEmpty(bgmKey)) return;

        if (delay > 0f)
            Invoke(nameof(PlayNow), delay);
        else
            PlayNow();
    }

    private void PlayNow()
    {
        if (!AudioManager.HasInstance)
        {
            Debug.LogWarning("[StartupMusic] AudioManager 未初始化，无法播放 BGM");
            return;
        }

        AudioManager.Instance.PlayBGMByKey(bgmKey);
    }
}
