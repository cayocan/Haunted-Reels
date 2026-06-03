using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using SlotEngine;

public enum AudioType { Music, SFX }

/// <summary>
/// Estende SlotEngine.AudioManager adicionando mute por AudioType e loop perfeito
/// (sem gap) via dual-buffer com AudioSource.PlayScheduled.
/// </summary>
public class HauntedAudioManager : AudioManager
{
    public static new HauntedAudioManager Instance { get; private set; }

    [Header("Mute por Tipo")]
    [Tooltip("Nomes das AudioEntries que são Música (o restante é SFX).")]
    [SerializeField] private string[] _musicEntryNames;

    private const string PrefMusicMuted = "audio_music_muted";
    private const string PrefSFXMuted   = "audio_sfx_muted";

    private bool _musicMuted;
    private bool _sfxMuted;

    // Dual-buffer seamless loop: um par de AudioSources por entrada em loop
    private struct LoopHandle
    {
        public AudioSource srcA;
        public AudioSource srcB;
        public Coroutine   coroutine;
    }
    private readonly Dictionary<string, LoopHandle> _loopHandles = new();

    // ── Ciclo de vida ─────────────────────────────────────────────────────

    private void Awake()
    {
        // Unity não chama automaticamente Awake() privada da base quando a subclasse
        // define a sua própria. Invocamos explicitamente para garantir BuildLookup/BuildPool.
        if (SlotEngine.AudioManager.Instance == null)
        {
            typeof(SlotEngine.AudioManager)
                .GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.Invoke(this, null);
        }

        if (Instance != null && Instance != this) return;
        Instance = this;

        _musicMuted = PlayerPrefs.GetInt(PrefMusicMuted, 0) == 1;
        _sfxMuted   = PlayerPrefs.GetInt(PrefSFXMuted,   0) == 1;
    }

    private void OnDestroy()
    {
        foreach (var key in new List<string>(_loopHandles.Keys))
            StopSeamlessLoop(key);
    }

    // ── API de mute ───────────────────────────────────────────────────────

    public bool IsMuted(AudioType type) =>
        type == AudioType.Music ? _musicMuted : _sfxMuted;

    public void SetMute(AudioType type, bool muted)
    {
        if (type == AudioType.Music)
        {
            _musicMuted = muted;
            PlayerPrefs.SetInt(PrefMusicMuted, muted ? 1 : 0);
            if (muted) StopAllMusic();
        }
        else
        {
            _sfxMuted = muted;
            PlayerPrefs.SetInt(PrefSFXMuted, muted ? 1 : 0);
        }
        PlayerPrefs.Save();
    }

    public void ToggleMute(AudioType type) => SetMute(type, !IsMuted(type));

    // ── Play com checagem de mute e loop perfeito ─────────────────────────

    public new void Play(string audioName, float pitch = 1f)
    {
        if (IsMuted(GetAudioType(audioName))) return;

        var entry = GetEntry(audioName);
        if (entry != null && entry.loop)
        {
            // Só inicia se ainda não está tocando
            if (!_loopHandles.ContainsKey(audioName))
                StartSeamlessLoop(audioName, entry);
            return;
        }

        base.Play(audioName, pitch);
    }

    public new void Stop(string audioName)
    {
        StopSeamlessLoop(audioName);
        base.Stop(audioName);
    }

    public new void StopAll()
    {
        foreach (var key in new List<string>(_loopHandles.Keys))
            StopSeamlessLoop(key);
        base.StopAll();
    }

    // ── Loop perfeito (dual-buffer) ───────────────────────────────────────

    private void StartSeamlessLoop(string audioName, AudioEntry entry)
    {
        var srcA = CreateLoopSource(audioName + "_A");
        var srcB = CreateLoopSource(audioName + "_B");
        var co   = StartCoroutine(SeamlessLoopRoutine(entry, srcA, srcB));
        _loopHandles[audioName] = new LoopHandle { srcA = srcA, srcB = srcB, coroutine = co };
    }

    private void StopSeamlessLoop(string audioName)
    {
        if (!_loopHandles.TryGetValue(audioName, out var h)) return;
        if (h.coroutine != null) StopCoroutine(h.coroutine);
        if (h.srcA != null) { h.srcA.Stop(); Destroy(h.srcA.gameObject); }
        if (h.srcB != null) { h.srcB.Stop(); Destroy(h.srcB.gameObject); }
        _loopHandles.Remove(audioName);
    }

    private AudioSource CreateLoopSource(string goName)
    {
        var go  = new GameObject(goName);
        go.transform.SetParent(transform);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        return src;
    }

    private IEnumerator SeamlessLoopRoutine(AudioEntry entry, AudioSource srcA, AudioSource srcB)
    {
        AudioClip clip        = entry.clip;
        float     volume      = entry.volume;
        double    clipLength  = (double)clip.samples / clip.frequency;
        const double kLookahead = 0.3; // segundos de antecedência para o PlayScheduled

        // Agenda a primeira reprodução
        double nextStartTime = AudioSettings.dspTime + kLookahead;
        srcA.clip   = clip;
        srcA.volume = volume;
        srcA.loop   = false;
        srcA.PlayScheduled(nextStartTime);

        AudioSource[] srcs   = { srcA, srcB };
        int           toggle = 0;

        while (true)
        {
            // Espera usando o próprio DSP clock (sem drift do relógio de parede)
            double wakeTarget = nextStartTime + clipLength - kLookahead;
            while (AudioSettings.dspTime < wakeTarget)
                yield return null;

            nextStartTime += clipLength;
            toggle         = 1 - toggle;
            srcs[toggle].clip   = clip;
            srcs[toggle].volume = volume;
            srcs[toggle].loop   = false;
            srcs[toggle].PlayScheduled(nextStartTime);
        }
    }

    // ── Internos ──────────────────────────────────────────────────────────

    private AudioType GetAudioType(string audioName)
    {
        if (_musicEntryNames == null) return AudioType.SFX;
        foreach (var name in _musicEntryNames)
            if (string.Equals(name, audioName, System.StringComparison.OrdinalIgnoreCase))
                return AudioType.Music;
        return AudioType.SFX;
    }

    private AudioEntry GetEntry(string audioName)
    {
        var field = typeof(SlotEngine.AudioManager)
            .GetField("_entries", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field?.GetValue(this) is not AudioEntry[] entries) return null;
        foreach (var e in entries)
            if (string.Equals(e.name, audioName, System.StringComparison.OrdinalIgnoreCase))
                return e;
        return null;
    }

    private void StopAllMusic()
    {
        if (_musicEntryNames == null) return;
        foreach (var name in _musicEntryNames)
            Stop(name);
    }
}
