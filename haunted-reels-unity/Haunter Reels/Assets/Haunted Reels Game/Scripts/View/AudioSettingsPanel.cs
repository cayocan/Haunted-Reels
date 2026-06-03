using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// View pura — não conhece AudioManager.
/// Expõe eventos para o Presenter e recebe chamadas de display.
/// </summary>
public class AudioSettingsPanel : MonoBehaviour
{
    [Header("Abertura / Fechamento")]
    [SerializeField] private Button     _openButton;
    [SerializeField] private Button     _closeButton;
    [SerializeField] private GameObject _panelRoot;

    [Header("Botões de Mute")]
    [SerializeField] private Button _musicToggleButton;
    [SerializeField] private Button _sfxToggleButton;

    [Header("Ícone — Música")]
    [SerializeField] private Image  _musicIcon;
    [SerializeField] private Sprite _musicOnSprite;
    [SerializeField] private Sprite _musicOffSprite;

    [Header("Ícone — SFX")]
    [SerializeField] private Image  _sfxIcon;
    [SerializeField] private Sprite _sfxOnSprite;
    [SerializeField] private Sprite _sfxOffSprite;

    // ── Eventos ───────────────────────────────────────────────────────────

    public event Action OnOpenRequested;
    public event Action OnCloseRequested;
    public event Action OnMusicToggleRequested;
    public event Action OnSFXToggleRequested;

    // ── Ciclo de vida ─────────────────────────────────────────────────────

    private void Awake()
    {
        if (_openButton        != null) _openButton.onClick.AddListener(() => OnOpenRequested?.Invoke());
        if (_closeButton       != null) _closeButton.onClick.AddListener(() => OnCloseRequested?.Invoke());
        if (_musicToggleButton != null) _musicToggleButton.onClick.AddListener(() => OnMusicToggleRequested?.Invoke());
        if (_sfxToggleButton   != null) _sfxToggleButton.onClick.AddListener(() => OnSFXToggleRequested?.Invoke());

        if (_panelRoot != null) _panelRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_openButton        != null) _openButton.onClick.RemoveAllListeners();
        if (_closeButton       != null) _closeButton.onClick.RemoveAllListeners();
        if (_musicToggleButton != null) _musicToggleButton.onClick.RemoveAllListeners();
        if (_sfxToggleButton   != null) _sfxToggleButton.onClick.RemoveAllListeners();
    }

    // ── API de display (chamada pelo Presenter) ───────────────────────────

    public void Open()
    {
        if (_panelRoot != null) _panelRoot.SetActive(true);
    }

    public void Close()
    {
        if (_panelRoot != null) _panelRoot.SetActive(false);
    }

    public void SetMusicMuted(bool muted)
    {
        if (_musicIcon == null) return;
        _musicIcon.sprite = muted ? _musicOffSprite : _musicOnSprite;
    }

    public void SetSFXMuted(bool muted)
    {
        if (_sfxIcon == null) return;
        _sfxIcon.sprite = muted ? _sfxOffSprite : _sfxOnSprite;
    }
}
