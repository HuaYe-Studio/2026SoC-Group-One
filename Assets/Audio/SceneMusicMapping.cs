using System;
using UnityEngine;

/// <summary>
/// 场景 → BGM 映射。监听 UIEventCenter.OnSceneChanged，
/// 场景切换时自动按映射表切换背景音乐。
/// 挂在和 AudioManager 同一个 GameObject 上（DontDestroyOnLoad 保证全局生效）。
///
/// 这是「跨场景换 BGM」的入口，与 SceneMusicZone（同场景内区域换 BGM）互补。
/// </summary>
public class SceneMusicMapping : MonoBehaviour
{
    [Serializable]
    public class SceneBgmPair
    {
        [Tooltip("场景名（与 Build Settings 里的场景文件名一致，不含 .unity）")]
        public string sceneName;

        [Tooltip("对应 AudioLibrary 的 bgmEntries 里的 key")]
        public string bgmKey;
    }

    [Header("场景 → BGM 映射")]
    [SerializeField] private SceneBgmPair[] mapping;

    [Tooltip("切换的交叉淡化时长（秒），0 表示立即切换")]
    [SerializeField] private float fadeTime = 2f;

    private void OnEnable()
    {
        UIEventCenter.OnSceneChanged += HandleSceneChanged;
    }

    private void OnDisable()
    {
        UIEventCenter.OnSceneChanged -= HandleSceneChanged;
    }

    private void HandleSceneChanged(string fromScene, string toScene)
    {
        string bgmKey = FindBgmKey(toScene);
        if (string.IsNullOrEmpty(bgmKey)) return;

        if (!AudioManager.HasInstance) return;

        if (fadeTime <= 0f)
            AudioManager.Instance.PlayBGMByKey(bgmKey);
        else
            AudioManager.Instance.CrossFadeBGM(bgmKey, fadeTime);
    }

    private string FindBgmKey(string sceneName)
    {
        if (mapping == null) return null;
        foreach (var pair in mapping)
        {
            if (pair != null && pair.sceneName == sceneName)
                return pair.bgmKey;
        }
        return null;
    }
}
