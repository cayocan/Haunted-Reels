using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
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

    [Header("Addressables")]
    [Tooltip("Mapeie cada AudioEntry pelo nome à sua AssetReference no grupo Addressable.")]
    [SerializeField] private AddressableAudioEntry[] _addressableEntries;

    private const string PrefMusicMuted = "audio_music_muted";
    private const string PrefSFXMuted   = "audio_sfx_muted";

    private bool _musicMuted;
    private bool _sfxMuted;

    // Addressables
    private bool                                    _addressablesReady;
    private readonly List<AsyncOperationHandle>     _handles      = new();
    private readonly Queue<(string name, float pitch)> _pendingPlays = new();

    // Dual-buffer seamless loop: um par de AudioSources por entrada em loop
    private struct LoopHandle
    {
        public AudioSource srcA;
        public AudioSource srcB;
        public Coroutine   coroutine;
    }
    private readonly Dictionary<string, LoopHandle> _loopHandles = new();
    private readonly Dictionary<string, (AudioSource src, Coroutine co)> _sweepHandles = new();

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

    private void Start()
    {
        StartCoroutine(LoadAddressablesRoutine());
    }

    private void OnDestroy()
    {
        foreach (var key in new List<string>(_sweepHandles.Keys))
            StopSweep(key);

        foreach (var key in new List<string>(_loopHandles.Keys))
            StopSeamlessLoop(key);

        foreach (var h in _handles)
            if (h.IsValid()) Addressables.Release(h);
        _handles.Clear();
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

    // ── Sweep de pitch (Shepard-lite) ─────────────────────────────────────

    /// <summary>
    /// Toca o clip em startPitch e faz Lerp até endPitch ao longo de duration segundos.
    /// Respects mute de SFX. Cancela automaticamente qualquer sweep anterior do mesmo nome.
    /// </summary>
    public void PlayWithPitchSweep(string audioName, float startPitch, float endPitch, float duration)
    {
        if (!_addressablesReady) return;
        if (IsMuted(GetAudioType(audioName))) return;

        StopSweep(audioName);

        var entry = GetEntry(audioName);
        if (entry?.clip == null)
        {
            Debug.LogWarning($"[HauntedAudioManager] PlayWithPitchSweep: entry '{audioName}' não encontrada ou sem clip.");
            return;
        }

        var src    = CreateLoopSource(audioName + "_sweep");
        src.clip   = entry.clip;
        src.volume = entry.volume;
        src.pitch  = startPitch;
        src.loop   = false;
        src.Play();

        var co = StartCoroutine(PitchSweepRoutine(src, startPitch, endPitch, duration, audioName));
        _sweepHandles[audioName] = (src, co);
    }

    private IEnumerator PitchSweepRoutine(AudioSource src, float startPitch, float endPitch, float duration, string audioName)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (src == null) { _sweepHandles.Remove(audioName); yield break; }
            src.pitch = Mathf.Lerp(startPitch, endPitch, elapsed / duration);
            yield return null;
        }

        if (src != null)
        {
            src.Stop();
            Destroy(src.gameObject);
        }
        _sweepHandles.Remove(audioName);
    }

    private void StopSweep(string audioName)
    {
        if (!_sweepHandles.TryGetValue(audioName, out var h)) return;
        if (h.co  != null) StopCoroutine(h.co);
        if (h.src != null) { h.src.Stop(); Destroy(h.src.gameObject); }
        _sweepHandles.Remove(audioName);
    }

    // ── Play com checagem de mute e loop perfeito ─────────────────────────

    public new void Play(string audioName, float pitch = 1f)
    {
        if (!_addressablesReady)
        {
            _pendingPlays.Enqueue((audioName, pitch));
            return;
        }

        if (IsMuted(GetAudioType(audioName))) return;

        var entry = GetEntry(audioName);
        if (entry != null && entry.loop)
        {
            if (!_loopHandles.ContainsKey(audioName))
                StartSeamlessLoop(audioName, entry);
            return;
        }

        base.Play(audioName, pitch);
    }

    public new void Stop(string audioName)
    {
        StopSweep(audioName);
        StopSeamlessLoop(audioName);
        base.Stop(audioName);
    }

    public new void StopAll()
    {
        foreach (var key in new List<string>(_sweepHandles.Keys))
            StopSweep(key);
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

    // ── Addressables load ─────────────────────────────────────────────────

    private IEnumerator LoadAddressablesRoutine()
    {
        if (_addressableEntries == null || _addressableEntries.Length == 0)
        {
            _addressablesReady = true;
            yield break;
        }

        // Dispara todos os loads em paralelo
        var handles = new AsyncOperationHandle<AudioClip>[_addressableEntries.Length];
        for (int i = 0; i < _addressableEntries.Length; i++)
            handles[i] = _addressableEntries[i].clipRef.LoadAssetAsync<AudioClip>();

        // Aguarda todos finalizarem
        foreach (var h in handles)
            yield return h;

        // Injeta os clips nas AudioEntries da base (AudioEntry é classe — referência compartilhada com _lookup)
        var field = typeof(SlotEngine.AudioManager)
            .GetField("_entries", BindingFlags.NonPublic | BindingFlags.Instance);

        if (field?.GetValue(this) is AudioEntry[] entries)
        {
            for (int i = 0; i < _addressableEntries.Length; i++)
            {
                if (handles[i].Status != AsyncOperationStatus.Succeeded)
                {
                    Debug.LogWarning($"[HauntedAudioManager] Falhou ao carregar: '{_addressableEntries[i].name}'");
                    continue;
                }

                var clip     = handles[i].Result;
                var entryName = _addressableEntries[i].name;

                foreach (var e in entries)
                {
                    if (string.Equals(e.name, entryName, StringComparison.OrdinalIgnoreCase))
                    {
                        e.clip = clip;
                        _handles.Add(handles[i]);
                        break;
                    }
                }
            }
        }

        _addressablesReady = true;
        Debug.Log($"[HauntedAudioManager] Addressables prontos ({_addressableEntries.Length} clips).");

        // Executa plays que chegaram antes do load terminar
        while (_pendingPlays.Count > 0)
        {
            var (name, pitch) = _pendingPlays.Dequeue();
            Play(name, pitch);
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

[Serializable]
public struct AddressableAudioEntry
{
    [Tooltip("Deve coincidir exatamente com o campo 'name' da AudioEntry correspondente.")]
    public string                     name;
    public AssetReferenceT<AudioClip> clipRef;
}
