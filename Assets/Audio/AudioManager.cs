using UnityEngine;

public class AudioManager : MonoBehaviour
{
    private const int PoolSize = 8;

    [Header("BGM")]
    [SerializeField] private AudioSource bgmSource;

    private AudioSource[] _sfxPool;
    private int _nextIndex;

    public static AudioManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureBgmSource();
        BuildPool();
    }

    private void EnsureBgmSource()
    {
        if (bgmSource != null) return;

        var go = new GameObject("BGM");
        go.transform.SetParent(transform);
        bgmSource = go.AddComponent<AudioSource>();
        bgmSource.playOnAwake = false;
    }

    private void BuildPool()
    {
        _sfxPool = new AudioSource[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            var go = new GameObject($"SFX_Pool_{i}");
            go.transform.SetParent(transform);
            _sfxPool[i] = go.AddComponent<AudioSource>();
            _sfxPool[i].playOnAwake = false;
        }
    }

    public void PlaySfx(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;

        var source = _sfxPool[_nextIndex];
        _nextIndex = (_nextIndex + 1) % PoolSize;

        source.Stop();
        source.clip = clip;
        source.volume = volume;
        source.Play();
    }

    public void PlayBGM(AudioClip clip, bool loop = true)
    {
        if (bgmSource == null || clip == null) return;

        if (bgmSource.clip == clip) return;

        bgmSource.clip = clip;
        bgmSource.loop = loop;
        bgmSource.Play();
    }
}
