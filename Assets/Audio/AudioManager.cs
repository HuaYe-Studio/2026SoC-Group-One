using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    private const int PoolSize = 8;

    [Header("BGM 源")]
    [Tooltip("场景里挂好的主 BGM AudioSource；为空则运行时自动创建")]
    [SerializeField] private AudioSource bgmSource;
    [Tooltip("第二 BGM 源，用于交叉淡化（Boss 战切换）。为空则运行时自动创建")]
    [SerializeField] private AudioSource bgmSourceSecondary;

    [Header("音频资源表（集中管理）")]
    [Tooltip("把创建的 AudioLibrary 资产拖到这里；留空则只能用直接传 clip 的接口")]
    [SerializeField] private AudioLibrary library;

    [Header("Audio Mixer")]
    [Tooltip("若为空，则音量回退到直接控制 AudioSource.volume")]
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private AudioMixerGroup bgmGroup;
    [SerializeField] private AudioMixerGroup sfxGroup;
    [SerializeField] private AudioMixerGroup uiGroup;

    [Header("音量（0~1）")]
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float uiVolume = 1f;

    [Header("空间定位")]
    [Tooltip("超过此距离（世界单位）的音效不播放")]
    [SerializeField] private float defaultMaxDistance = 18f;
    [Tooltip("距离衰减曲线：横轴 = 归一化距离(0~1)，纵轴 = 音量系数(0~1)。默认线性")]
    [SerializeField] private AnimationCurve falloffCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [Header("Mixer 暴露参数名")]
    [SerializeField] private string bgmVolumeParam = "BGMVolume";
    [SerializeField] private string sfxVolumeParam = "SFXVolume";
    [SerializeField] private string uiVolumeParam = "UIVolume";

    // SFX 池：普通一次性音效
    private AudioSource[] _sfxPool;
    private int _nextIndex;

    // UI 音效池：独立于 SFX，走 UI 总线（不随距离衰减，始终 2D）
    private AudioSource[] _uiPool;
    private int _nextUiIndex;

    // 循环音源：静态环境音（水流/风），持续跟踪玩家位置更新衰减和声像
    private readonly Dictionary<string, AudioSource> _loopingSources = new Dictionary<string, AudioSource>();

    private Coroutine _bgmFadeRoutine;
    private Coroutine _crossFadeRoutine;

    // 当前活跃的 BGM 源（交叉淡化时轮流切换）
    private AudioSource _activeBgm;
    private AudioSource _inactiveBgm;

    public static AudioManager Instance { get; private set; }

    public float BgmVolume => bgmVolume;
    public float SfxVolume => sfxVolume;
    public float UiVolume => uiVolume;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureBgmSources();
        BuildPools();
        ApplyVolumes();
    }

    private void Update()
    {
        // 每帧更新循环音源的衰减和声像（跟随玩家移动）
        UpdateLoopingSources();
    }

    private void EnsureBgmSources()
    {
        if (bgmSource == null)
        {
            var go = new GameObject("BGM");
            go.transform.SetParent(transform);
            bgmSource = go.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
        }

        if (bgmSourceSecondary == null)
        {
            var go = new GameObject("BGM_Secondary");
            go.transform.SetParent(transform);
            bgmSourceSecondary = go.AddComponent<AudioSource>();
            bgmSourceSecondary.playOnAwake = false;
        }

        _activeBgm = bgmSource;
        _inactiveBgm = bgmSourceSecondary;
    }

    private void BuildPools()
    {
        _sfxPool = new AudioSource[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            var go = new GameObject($"SFX_Pool_{i}");
            go.transform.SetParent(transform);
            _sfxPool[i] = go.AddComponent<AudioSource>();
            _sfxPool[i].playOnAwake = false;
            _sfxPool[i].spatialBlend = 0f;
            _sfxPool[i].outputAudioMixerGroup = sfxGroup;
        }

        _uiPool = new AudioSource[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            var go = new GameObject($"UI_Pool_{i}");
            go.transform.SetParent(transform);
            _uiPool[i] = go.AddComponent<AudioSource>();
            _uiPool[i].playOnAwake = false;
            _uiPool[i].spatialBlend = 0f;
            _uiPool[i].outputAudioMixerGroup = uiGroup;
        }

        bgmSource.outputAudioMixerGroup = bgmGroup;
        bgmSourceSecondary.outputAudioMixerGroup = bgmGroup;
    }

    // ---------- SFX：普通游戏音效 ----------

    /// <summary>播放 2D 音效（不带空间定位）。池满时轮询覆盖最旧的音效。</summary>
    public void PlaySfx(AudioClip clip, float volume = 1f, int priority = 128)
    {
        if (clip == null) return;

        var source = _sfxPool[_nextIndex];
        _nextIndex = (_nextIndex + 1) % PoolSize;

        source.Stop();
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume) * sfxVolume;
        source.priority = priority;
        source.panStereo = 0f;
        source.Play();
    }

    /// <summary>
    /// 在指定世界坐标播放空间定位音效（一次性）。
    /// 只计算一次，适合瞬时音效（落地、射击等）。持续环境音请用 PlayLoopingSfx。
    /// </summary>
    public void PlaySfxAt(Vector2 worldPos, AudioClip clip, float maxDistance = -1f, float volume = 1f, int priority = 128)
    {
        if (clip == null) return;
        if (maxDistance <= 0f) maxDistance = defaultMaxDistance;

        Vector2 listenerPos = GetListenerPosition();
        float distance = Vector2.Distance(worldPos, listenerPos);
        if (distance > maxDistance) return;

        var source = _sfxPool[_nextIndex];
        _nextIndex = (_nextIndex + 1) % PoolSize;

        ApplySpatial(source, worldPos, listenerPos, maxDistance, volume, priority);

        source.clip = clip;
        source.Play();
    }

    // ---------- UI 音效 ----------

    /// <summary>播放 UI 音效（走 UI 总线，2D，不随距离衰减）。</summary>
    public void PlayUiSfx(AudioClip clip, float volume = 1f, int priority = 128)
    {
        if (clip == null) return;

        var source = _uiPool[_nextUiIndex];
        _nextUiIndex = (_nextUiIndex + 1) % PoolSize;

        source.Stop();
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume) * uiVolume;
        source.priority = priority;
        source.panStereo = 0f;
        source.Play();
    }

    /// <summary>按 key 播放 UI 音效（key 对应 AudioLibrary 的 uiEntries）。</summary>
    public void PlayUiSfxByKey(string key, float volume = 1f)
    {
        var entry = library != null ? library.GetUiEntry(key) : null;
        if (entry == null)
        {
            Debug.LogWarning($"[AudioManager] 未找到 UI 音效 key: {key}");
            return;
        }
        PlayUiSfx(entry.clip, volume, entry.priority);
    }

    // ---------- 循环音源（静态环境音） ----------

    /// <summary>
    /// 播放一个持续循环的空间定位音效（如水流、风声）。
    /// 会在 Update 中持续跟踪玩家位置，实时更新音量衰减和左右声像。
    /// 同 key 重复调用会忽略；用 StopLoopingSfx 停止。
    /// </summary>
    public void PlayLoopingSfx(string key, Vector2 worldPos, float maxDistance = -1f, float volume = 1f)
    {
        if (library == null) return;
        var entry = library.GetSfxEntry(key);
        if (entry == null)
        {
            Debug.LogWarning($"[AudioManager] 未找到循环音效 key: {key}");
            return;
        }
        if (_loopingSources.ContainsKey(key)) return;

        if (maxDistance <= 0f) maxDistance = defaultMaxDistance;

        var go = new GameObject($"LoopingSfx_{key}");
        go.transform.SetParent(transform);
        go.transform.position = worldPos;

        var source = go.AddComponent<AudioSource>();
        source.clip = entry.clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.outputAudioMixerGroup = sfxGroup;
        source.priority = entry.priority;

        var holder = go.AddComponent<LoopingSfxHolder>();
        holder.worldPos = worldPos;
        holder.maxDistance = maxDistance;
        holder.volume = volume;
        holder.priority = entry.priority;

        source.Play();
        _loopingSources[key] = source;
    }

    /// <summary>停止指定 key 的循环音效。</summary>
    public void StopLoopingSfx(string key)
    {
        if (_loopingSources.TryGetValue(key, out var source))
        {
            if (source != null) Destroy(source.gameObject);
            _loopingSources.Remove(key);
        }
    }

    /// <summary>停止所有循环音效。</summary>
    public void StopAllLoopingSfx()
    {
        foreach (var kv in _loopingSources)
        {
            if (kv.Value != null) Destroy(kv.Value.gameObject);
        }
        _loopingSources.Clear();
    }

    private void UpdateLoopingSources()
    {
        if (_loopingSources.Count == 0) return;

        Vector2 listenerPos = GetListenerPosition();
        foreach (var kv in _loopingSources)
        {
            var source = kv.Value;
            if (source == null) continue;
            var holder = source.GetComponent<LoopingSfxHolder>();
            if (holder == null) continue;

            ApplySpatial(source, holder.worldPos, listenerPos, holder.maxDistance, holder.volume, holder.priority);
        }
    }

    private void ApplySpatial(AudioSource source, Vector2 worldPos, Vector2 listenerPos, float maxDistance, float volume, int priority)
    {
        float distance = Vector2.Distance(worldPos, listenerPos);
        float normalized = Mathf.Clamp01(distance / maxDistance);
        float attenuation = falloffCurve.Evaluate(normalized);

        float pan = Mathf.Clamp((worldPos.x - listenerPos.x) / maxDistance, -1f, 1f);

        source.volume = Mathf.Clamp01(volume) * sfxVolume * attenuation;
        source.priority = priority;
        source.panStereo = pan;
    }

    // ---------- SFX：通过 AudioLibrary 的 key 播放 ----------

    /// <summary>按 key 播放音效（key 对应 AudioLibrary 的 sfxEntries）。</summary>
    public void PlaySfxByKey(string key, float volume = 1f)
    {
        var entry = library != null ? library.GetSfxEntry(key) : null;
        if (entry == null)
        {
            Debug.LogWarning($"[AudioManager] 未找到音效 key: {key}");
            return;
        }
        PlaySfx(entry.clip, volume, entry.priority);
    }

    /// <summary>按 key 在指定位置播放空间定位音效（一次性）。</summary>
    public void PlaySfxAtKey(Vector2 worldPos, string key, float maxDistance = -1f, float volume = 1f)
    {
        var entry = library != null ? library.GetSfxEntry(key) : null;
        if (entry == null)
        {
            Debug.LogWarning($"[AudioManager] 未找到音效 key: {key}");
            return;
        }
        PlaySfxAt(worldPos, entry.clip, maxDistance, volume, entry.priority);
    }

    // ---------- BGM ----------

    /// <summary>播放背景音乐。相同 clip 重复调用会被忽略。</summary>
    public void PlayBGM(AudioClip clip, bool loop = true)
    {
        if (clip == null) return;

        StopBgmFade();
        StopCrossFade();

        _inactiveBgm.Stop();
        _activeBgm.clip = clip;
        _activeBgm.loop = loop;
        _activeBgm.volume = bgmVolume;
        _activeBgm.Play();
    }

    /// <summary>按 key 播放背景音乐（key 对应 AudioLibrary 的 bgmEntries）。</summary>
    public void PlayBGMByKey(string key, bool loop = true)
    {
        var clip = library != null ? library.GetBgm(key) : null;
        if (clip == null)
        {
            Debug.LogWarning($"[AudioManager] 未找到 BGM key: {key}");
            return;
        }
        PlayBGM(clip, loop);
    }

    /// <summary>停止背景音乐。</summary>
    public void StopBGM()
    {
        StopBgmFade();
        StopCrossFade();
        _activeBgm.Stop();
        _inactiveBgm.Stop();
    }

    /// <summary>淡入/淡出背景音乐到目标音量（0~1，相对于 BGM 总音量）。</summary>
    public void FadeBGM(float targetVolume, float duration)
    {
        StopBgmFade();
        StopCrossFade();
        _bgmFadeRoutine = StartCoroutine(FadeBgmRoutine(targetVolume, duration));
    }

    /// <summary>
    /// 交叉淡化切换到新 BGM（按 key）。真正的双源实现：
    /// 旧 BGM 在 _activeBgm 上淡出，新 BGM 在 _inactiveBgm 上同时淡入，无缝衔接。
    /// 适合 Boss 战等场景的音乐切换。
    /// </summary>
    public void CrossFadeBGM(string newKey, float fadeTime)
    {
        if (library == null) return;
        var clip = library.GetBgm(newKey);
        if (clip == null)
        {
            Debug.LogWarning($"[AudioManager] 未找到 BGM key: {newKey}");
            return;
        }

        StopBgmFade();
        StopCrossFade();
        _crossFadeRoutine = StartCoroutine(CrossFadeRoutine(clip, fadeTime));
    }

    private IEnumerator CrossFadeRoutine(AudioClip newClip, float fadeTime)
    {
        var fadingOut = _activeBgm;
        var fadingIn = _inactiveBgm;

        // 新 BGM 从静音开始，与旧 BGM 同时播放
        fadingIn.clip = newClip;
        fadingIn.loop = true;
        fadingIn.volume = 0f;
        fadingIn.Play();

        float t = 0f;
        float fadeOutStart = fadingOut.isPlaying ? fadingOut.volume : 0f;

        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / fadeTime);

            fadingIn.volume = Mathf.Lerp(0f, bgmVolume, k);
            if (fadingOut.isPlaying)
                fadingOut.volume = Mathf.Lerp(fadeOutStart, 0f, k);

            yield return null;
        }

        fadingIn.volume = bgmVolume;
        fadingOut.Stop();

        // 交换 active/inactive，保证下次交叉淡化正确
        _activeBgm = fadingIn;
        _inactiveBgm = fadingOut;

        _crossFadeRoutine = null;
    }

    private IEnumerator FadeBgmRoutine(float targetVolume, float duration)
    {
        float start = _activeBgm.volume;
        float target = Mathf.Clamp01(targetVolume) * bgmVolume;
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            _activeBgm.volume = Mathf.Lerp(start, target, Mathf.Clamp01(t / duration));
            yield return null;
        }

        _activeBgm.volume = target;
        _bgmFadeRoutine = null;
    }

    private void StopBgmFade()
    {
        if (_bgmFadeRoutine != null)
        {
            StopCoroutine(_bgmFadeRoutine);
            _bgmFadeRoutine = null;
        }
    }

    private void StopCrossFade()
    {
        if (_crossFadeRoutine != null)
        {
            StopCoroutine(_crossFadeRoutine);
            _crossFadeRoutine = null;
        }
    }

    // ---------- 音量 ----------

    /// <summary>设置 BGM 音量（0~1），立即生效。</summary>
    public void SetBgmVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        ApplyBgmVolume();
    }

    /// <summary>设置 SFX 音量（0~1），立即生效。</summary>
    public void SetSfxVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        ApplySfxVolume();
    }

    /// <summary>设置 UI 音效音量（0~1），立即生效。</summary>
    public void SetUiVolume(float volume)
    {
        uiVolume = Mathf.Clamp01(volume);
        ApplyUiVolume();
    }

    private void ApplyVolumes()
    {
        ApplyBgmVolume();
        ApplySfxVolume();
        ApplyUiVolume();
    }

    private void ApplyBgmVolume()
    {
        if (mixer != null && !string.IsNullOrEmpty(bgmVolumeParam))
            mixer.SetFloat(bgmVolumeParam, LinearToDb(bgmVolume));
        else
        {
            _activeBgm.volume = bgmVolume;
            _inactiveBgm.volume = bgmVolume;
        }
    }

    private void ApplySfxVolume()
    {
        if (mixer != null && !string.IsNullOrEmpty(sfxVolumeParam))
            mixer.SetFloat(sfxVolumeParam, LinearToDb(sfxVolume));
    }

    private void ApplyUiVolume()
    {
        if (mixer != null && !string.IsNullOrEmpty(uiVolumeParam))
            mixer.SetFloat(uiVolumeParam, LinearToDb(uiVolume));
    }

    /// <summary>线性音量(0~1) 转分贝。0 映射到 -80dB（近似静音），1 映射到 0dB。</summary>
    private static float LinearToDb(float linear)
    {
        if (linear <= 0.0001f) return -80f;
        return Mathf.Log10(linear) * 20f;
    }

    // ---------- 内部工具 ----------

    private Vector2 GetListenerPosition()
    {
        var cam = Camera.main;
        return cam != null ? (Vector2)cam.transform.position : Vector2.zero;
    }

    /// <summary>循环音源的空间定位参数载体。</summary>
    private class LoopingSfxHolder : MonoBehaviour
    {
        public Vector2 worldPos;
        public float maxDistance;
        public float volume;
        public int priority;
    }
}
