using UnityEngine;

/// <summary>
/// Player 死亡音效：监听 MockEventCenter.OnPlayerDeath，死亡时播放音效。
/// 挂在 Player 根节点（PlayerController / PlayerHP 同一物体）上。
/// </summary>
public class PlayerDeathSound : MonoBehaviour
{
    [Header("死亡音效")]
    [Tooltip("对应 AudioLibrary 的 sfxEntries 里的 key")]
    [SerializeField] private string deathSfxKey = "player_death";

    private void OnEnable()
    {
        MockEventCenter.OnPlayerDeath += HandlePlayerDeath;
    }

    private void OnDisable()
    {
        MockEventCenter.OnPlayerDeath -= HandlePlayerDeath;
    }

    private void HandlePlayerDeath()
    {
        if (AudioManager.HasInstance && !string.IsNullOrEmpty(deathSfxKey))
            AudioManager.Instance.PlaySfxByKey(deathSfxKey);
    }
}
