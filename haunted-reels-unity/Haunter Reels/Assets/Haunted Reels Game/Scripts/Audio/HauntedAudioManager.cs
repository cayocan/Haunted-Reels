using UnityEngine;
using SlotEngine;

public enum AudioType { Music, SFX }

/// <summary>
/// Estende SlotEngine.AudioManager adicionando mute por AudioType.
/// No Inspector, preencha _musicEntryNames com os nomes das entradas de música
/// cadastradas em _entries (ex: "bgm", "menu_music"). Tudo o mais é tratado como SFX.
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

    // ── Ciclo de vida ─────────────────────────────────────────────────────

    private void Awake()
    {
        // A Awake() privada da base (SlotEngine.AudioManager) é chamada
        // separadamente pelo Unity via reflection — não precisa de base.Awake()
        if (Instance != null && Instance != this) return;
        Instance = this;

        _musicMuted = PlayerPrefs.GetInt(PrefMusicMuted, 0) == 1;
        _sfxMuted   = PlayerPrefs.GetInt(PrefSFXMuted,   0) == 1;
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

    // ── Play com checagem de mute ─────────────────────────────────────────

    public new void Play(string audioName, float pitch = 1f)
    {
        if (IsMuted(GetAudioType(audioName))) return;
        base.Play(audioName, pitch);
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

    private void StopAllMusic()
    {
        if (_musicEntryNames == null) return;
        foreach (var name in _musicEntryNames)
            Stop(name);
    }
}
