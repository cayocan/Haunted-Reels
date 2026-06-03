using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuView : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button        _playButton;
    [SerializeField] private TMP_Text      _errorText;

    [Header("Animações de Entrada")]
    [SerializeField] private RectTransform _logo;
    [SerializeField] private float         _logoSlideDistance = 800f;
    [SerializeField] private float         _logoSlideDuration = 0.7f;
    [SerializeField] private float         _buttonZoomDuration = 0.4f;

    [Header("Breathing")]
    [SerializeField] private float _breathScale    = 1.04f;
    [SerializeField] private float _breathDuration = 2f;

    [Header("Navegação")]
    [Tooltip("Nome exato da cena do jogo registrada em Build Settings.")]
    [SerializeField] private string _gameSceneName = "GameScene";

    private CancellationTokenSource _cts;
    private Vector2                 _logoStartPos;
    private Tween                   _breathTween;

    private void Awake()
    {
        _cts = new CancellationTokenSource();
        SetError(null);

        if (_logo != null)
            _logoStartPos = _logo.anchoredPosition;

        // Esconde botão antes da animação de entrada
        if (_playButton != null)
            _playButton.transform.localScale = Vector3.zero;
    }

    private void Start()
    {
        _playButton.onClick.AddListener(OnPlayClicked);
        PlayEntranceAsync(_cts.Token).Forget();
    }

    private async UniTaskVoid PlayEntranceAsync(CancellationToken ct)
    {
        // Logo: entra de cima para baixo
        if (_logo != null)
        {
            _logo.anchoredPosition = _logoStartPos + Vector2.up * _logoSlideDistance;
            var tcs = new UniTaskCompletionSource();
            _logo.DOAnchorPos(_logoStartPos, _logoSlideDuration)
                 .SetEase(Ease.OutCubic)
                 .OnComplete(() => tcs.TrySetResult())
                 .OnKill(() => tcs.TrySetResult());
            await tcs.Task;
            if (ct.IsCancellationRequested) return;
            StartBreathing();
        }

        // Botão: zoom in após a logo assentar
        if (_playButton != null)
        {
            var tcs = new UniTaskCompletionSource();
            _playButton.transform
                       .DOScale(Vector3.one, _buttonZoomDuration)
                       .SetEase(Ease.OutBack)
                       .OnComplete(() => tcs.TrySetResult())
                       .OnKill(() => tcs.TrySetResult());
            await tcs.Task;
        }
    }

    private void StartBreathing()
    {
        if (_logo == null) return;
        _breathTween?.Kill();
        _breathTween = _logo.DOScale(Vector3.one * _breathScale, _breathDuration)
                            .SetEase(Ease.InOutSine)
                            .SetLoops(-1, LoopType.Yoyo);
    }

    private void OnDestroy()
    {
        _cts.Cancel();
        _cts.Dispose();
        _breathTween?.Kill();
        if (_playButton != null)
            _playButton.onClick.RemoveListener(OnPlayClicked);
    }

    private void OnPlayClicked()
    {
        HandlePlayAsync(_cts.Token).Forget();
    }

    private async UniTaskVoid HandlePlayAsync(CancellationToken ct)
    {
        SetError(null);
        _playButton.interactable = false;

        bool ok = await SessionPresenter.Instance.InitAsync(ct);

        if (ct.IsCancellationRequested) return;

        if (ok)
        {
            SceneManager.LoadScene(_gameSceneName);
        }
        else
        {
            SetError("Não foi possível conectar ao servidor.\nVerifique sua conexão e tente novamente.");
            _playButton.interactable = true;
        }
    }

    private void SetError(string message)
    {
        if (_errorText == null) return;
        bool hasError = !string.IsNullOrEmpty(message);
        _errorText.gameObject.SetActive(hasError);
        if (hasError) _errorText.text = message;
    }
}
