using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// View de tela de carregamento exibida enquanto o backend está acordando.
/// Expõe métodos simples de show/hide, fade e troca de mensagem para que o
/// <see cref="BackendWakeUpPresenter"/> controle toda a lógica sem tocar em Unity diretamente.
/// </summary>
public class BackendWakeUpView : MonoBehaviour
{
    [Header("References")]
    public CanvasGroup    canvasGroup;
    public GameObject     background;
    public RectTransform  creatureContainer;
    public RectTransform  creatureImage;
    public TMP_Text       messageText;
    public TMP_Text       dotsText;

    private Tween _fadeTween;
    private Tween _messageFadeTween;

    private void OnDestroy()
    {
        _fadeTween?.Kill();
        _messageFadeTween?.Kill();
    }

    public void Show()
    {
        canvasGroup.alpha = 1f;
        gameObject.SetActive(true);
    }

    public void Hide() => gameObject.SetActive(false);

    public void SetMessage(string message)
    {
        messageText.text  = message;
        messageText.alpha = 1f;
    }

    /// <summary>Troca a mensagem com fade out → troca de texto → fade in.</summary>
    public void FadeMessageTo(string nextMessage)
    {
        if (_messageFadeTween != null) _messageFadeTween.Kill();
        _messageFadeTween = messageText.DOFade(0f, 0.3f).OnComplete(() =>
        {
            messageText.text = nextMessage;
            messageText.DOFade(1f, 0.3f);
        });
    }

    public void SetDots(string dots)
    {
        if (dotsText != null) dotsText.text = dots;
    }

    /// <summary>
    /// Faz fade out do canvas e desativa o GameObject ao final.
    /// Invoca <paramref name="onComplete"/> após a animação — usado pelo presenter para
    /// liberar o fluxo principal e ativar o menu.
    /// </summary>
    public void PlayFadeOut(Action onComplete)
    {
        if (_fadeTween != null) _fadeTween.Kill();
        _fadeTween = canvasGroup.DOFade(0f, 0.5f).OnComplete(() =>
        {
            gameObject.SetActive(false);
            onComplete?.Invoke();
        });
    }
}
