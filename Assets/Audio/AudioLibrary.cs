using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 音频资源集中管理表。以 key（字符串）索引 AudioClip，
/// 避免音频散落在各脚本的 Inspector 里难以维护。
///
/// 使用方式：Project 右键 → Create → Audio → Audio Library，
/// 在 Inspector 里填入 key 和对应的 wav。
/// </summary>
[CreateAssetMenu(fileName = "AudioLibrary", menuName = "Audio/Audio Library")]
public class AudioLibrary : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("播放时使用的 key，例如 jump / land / devour")]
        public string key;

        public AudioClip clip;

        [Tooltip("优先级：0 = 最重要，256 = 最不重要。音效过多时 Unity 会自动丢弃低优先级（数值大）的音效。落地/受伤等反馈音效应设较小值")]
        [Range(0, 256)]
        public int priority = 128;
    }

    [Header("音效（一次性播放）")]
    public List<Entry> sfxEntries = new List<Entry>();

    [Header("背景音乐（循环播放）")]
    public List<Entry> bgmEntries = new List<Entry>();

    [Header("UI 音效（走 Mixer 的 UI 总线）")]
    public List<Entry> uiEntries = new List<Entry>();

    /// <summary>按 key 取音效条目，找不到返回 null。</summary>
    public Entry GetSfxEntry(string key) => FindEntry(sfxEntries, key);

    /// <summary>按 key 取背景音乐条目，找不到返回 null。</summary>
    public Entry GetBgmEntry(string key) => FindEntry(bgmEntries, key);

    /// <summary>按 key 取 UI 音效条目，找不到返回 null。</summary>
    public Entry GetUiEntry(string key) => FindEntry(uiEntries, key);

    /// <summary>按 key 取音效，找不到返回 null。</summary>
    public AudioClip GetSfx(string key) => GetSfxEntry(key)?.clip;

    /// <summary>按 key 取背景音乐，找不到返回 null。</summary>
    public AudioClip GetBgm(string key) => GetBgmEntry(key)?.clip;

    /// <summary>按 key 取 UI 音效，找不到返回 null。</summary>
    public AudioClip GetUiSfx(string key) => GetUiEntry(key)?.clip;

    private Entry FindEntry(List<Entry> list, string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        foreach (var e in list)
        {
            if (e != null && e.key == key)
                return e;
        }
        return null;
    }
}
