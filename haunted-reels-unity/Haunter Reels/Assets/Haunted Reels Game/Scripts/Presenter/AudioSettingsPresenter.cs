using UnityEngine;

public class AudioSettingsPresenter : MonoBehaviour
{
    [SerializeField] private AudioSettingsPanel _view;

    private void Start()
    {
        if (_view == null)
        {
            Debug.LogError("[AudioSettingsPresenter] AudioSettingsPanel não atribuído.");
            return;
        }

        _view.OnOpenRequested        += OnOpen;
        _view.OnCloseRequested       += OnClose;
        _view.OnMusicToggleRequested += OnMusicToggle;
        _view.OnSFXToggleRequested   += OnSFXToggle;

        // Sincroniza visuais com estado salvo
        SyncVisuals();
    }

    private void OnDestroy()
    {
        if (_view == null) return;
        _view.OnOpenRequested        -= OnOpen;
        _view.OnCloseRequested       -= OnClose;
        _view.OnMusicToggleRequested -= OnMusicToggle;
        _view.OnSFXToggleRequested   -= OnSFXToggle;
    }

    // ── Handlers ──────────────────────────────────────────────────────────

    private void OnOpen()
    {
        SyncVisuals();
        _view.Open();
    }

    private void OnClose() => _view.Close();

    private void OnMusicToggle()
    {
        if (HauntedAudioManager.Instance == null) return;
        HauntedAudioManager.Instance.ToggleMute(AudioType.Music);
        _view.SetMusicMuted(HauntedAudioManager.Instance.IsMuted(AudioType.Music));
    }

    private void OnSFXToggle()
    {
        if (HauntedAudioManager.Instance == null) return;
        HauntedAudioManager.Instance.ToggleMute(AudioType.SFX);
        _view.SetSFXMuted(HauntedAudioManager.Instance.IsMuted(AudioType.SFX));
    }

    // ── Internos ──────────────────────────────────────────────────────────

    private void SyncVisuals()
    {
        if (HauntedAudioManager.Instance == null) return;
        _view.SetMusicMuted(HauntedAudioManager.Instance.IsMuted(AudioType.Music));
        _view.SetSFXMuted(HauntedAudioManager.Instance.IsMuted(AudioType.SFX));
    }
}
