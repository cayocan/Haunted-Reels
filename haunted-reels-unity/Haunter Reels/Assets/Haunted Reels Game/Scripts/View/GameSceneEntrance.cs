using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Orquestra as animações de entrada da GameScene, rodando todas em paralelo via UniTask.WhenAll.
/// </summary>
/// <remarks>
/// Grupos de animação:
/// <list type="bullet">
///   <item><b>Painéis UI</b> — zoom in com bounce e stagger configurável.</item>
///   <item><b>Slot machine</b> — cai de cima com efeito OutBounce.</item>
///   <item><b>Botões direita</b> — chegam da direita com OutBack e stagger.</item>
///   <item><b>Painel info</b> — sobe de baixo com OutBack.</item>
///   <item><b>BG slot machine</b> — fade in após todas as animações completarem.</item>
/// </list>
/// As posições originais são capturadas no <c>Awake</c> antes de tudo ser deslocado para
/// fora da tela, garantindo que o layout do Editor seja preservado como posição final.
/// </remarks>
public class GameSceneEntrance : MonoBehaviour
{
    [Header("Painéis — zoom in/out (atribua todos os painéis de UI)")]
    [SerializeField] private RectTransform[] _panels;
    [SerializeField] private float           _panelZoomDuration = 0.35f;
    [SerializeField] private float           _panelStagger      = 0.06f;

    [Header("Slot Machine — cai de cima")]
    [SerializeField] private RectTransform _slotMachine;
    [SerializeField] private float         _slotSlideDistance = 1200f;
    [SerializeField] private float         _slotSlideDuration = 0.7f;

    [Header("Botões direita — vêm da direita (com stagger)")]
    [SerializeField] private RectTransform[] _rightButtons;
    [SerializeField] private float           _rightSlideDistance = 500f;
    [SerializeField] private float           _rightSlideDuration = 0.5f;
    [SerializeField] private float           _rightStagger       = 0.07f;

    [Header("Painel info — vem de baixo")]
    [SerializeField] private RectTransform _infoPanel;
    [SerializeField] private float         _infoPanelSlideDistance = 350f;
    [SerializeField] private float         _infoPanelSlideDuration = 0.5f;

    [Header("BG Slot Machine — fade in (após todas as animações)")]
    [SerializeField] private CanvasGroup _slotMachineBg;
    [SerializeField] private float       _bgFadeDuration  = 0.5f;
    [SerializeField] [Range(0f, 1f)] private float _bgTargetAlpha = 1f;

    // posições originais (capturadas antes de mover para fora da tela)
    private Vector2   _slotOrigPos;
    private Vector2[] _rightButtonsOrigPos;
    private Vector2   _infoPanelOrigPos;

    private CancellationTokenSource _cts;

    // ── Ciclo de vida ─────────────────────────────────────────────────────

    private void Awake()
    {
        _cts = new CancellationTokenSource();

        if (_slotMachine != null) _slotOrigPos = _slotMachine.anchoredPosition;
        if (_infoPanel   != null) _infoPanelOrigPos = _infoPanel.anchoredPosition;

        if (_rightButtons != null)
        {
            _rightButtonsOrigPos = new Vector2[_rightButtons.Length];
            for (int i = 0; i < _rightButtons.Length; i++)
                if (_rightButtons[i] != null)
                    _rightButtonsOrigPos[i] = _rightButtons[i].anchoredPosition;
        }

        HideAll();
    }

    private void Start()
    {
        PlayEntranceAsync(_cts.Token).Forget();
    }

    private void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }

    // ── Setup inicial (fora da tela) ──────────────────────────────────────

    private void HideAll()
    {
        if (_panels != null)
            foreach (var p in _panels)
                if (p != null) p.localScale = Vector3.zero;

        if (_slotMachineBg != null) _slotMachineBg.alpha = 0f;

        if (_slotMachine  != null)
            _slotMachine.anchoredPosition = _slotOrigPos + Vector2.up * _slotSlideDistance;

        if (_rightButtons != null)
            for (int i = 0; i < _rightButtons.Length; i++)
                if (_rightButtons[i] != null)
                    _rightButtons[i].anchoredPosition = _rightButtonsOrigPos[i] + Vector2.right * _rightSlideDistance;

        if (_infoPanel != null)
            _infoPanel.anchoredPosition = _infoPanelOrigPos + Vector2.down * _infoPanelSlideDistance;
    }

    // ── Animações de entrada ──────────────────────────────────────────────

    private async UniTaskVoid PlayEntranceAsync(CancellationToken ct)
    {
        var tasks = new List<UniTask>();

        // Painéis: zoom in/out com stagger
        if (_panels != null)
            for (int i = 0; i < _panels.Length; i++)
                if (_panels[i] != null)
                    tasks.Add(AnimatePanelZoomAsync(_panels[i], i * _panelStagger, ct));

        // Slot machine: cai de cima com leve bounce
        if (_slotMachine != null)
            tasks.Add(AnimateSlideAsync(_slotMachine, _slotOrigPos, _slotSlideDuration, Ease.OutBounce, 0f, ct));

        // Botões direita: chegam da direita com stagger
        if (_rightButtons != null)
            for (int i = 0; i < _rightButtons.Length; i++)
                if (_rightButtons[i] != null)
                    tasks.Add(AnimateSlideAsync(_rightButtons[i], _rightButtonsOrigPos[i], _rightSlideDuration, Ease.OutBack, 0.15f + i * _rightStagger, ct));

        // Painel info: sobe de baixo
        if (_infoPanel != null)
            tasks.Add(AnimateSlideAsync(_infoPanel, _infoPanelOrigPos, _infoPanelSlideDuration, Ease.OutBack, 0.1f, ct));

        await UniTask.WhenAll(tasks);

        if (_slotMachineBg != null && !ct.IsCancellationRequested)
        {
            var tcs = new UniTaskCompletionSource();
            _slotMachineBg.DOFade(_bgTargetAlpha, _bgFadeDuration)
                .OnComplete(() => tcs.TrySetResult())
                .OnKill   (() => tcs.TrySetResult());
            await tcs.Task;
        }
    }

    private async UniTask AnimatePanelZoomAsync(RectTransform rt, float delay, CancellationToken ct)
    {
        if (delay > 0f)
            await UniTask.Delay((int)(delay * 1000), cancellationToken: ct);

        var tcs = new UniTaskCompletionSource();
        DOTween.Sequence()
            .Append(rt.DOScale(Vector3.one * 1.12f, _panelZoomDuration * 0.7f).SetEase(Ease.OutQuad))
            .Append(rt.DOScale(Vector3.one,          _panelZoomDuration * 0.3f).SetEase(Ease.InQuad))
            .OnComplete(() => tcs.TrySetResult())
            .OnKill   (() => tcs.TrySetResult());
        await tcs.Task;
    }

    private async UniTask AnimateSlideAsync(RectTransform rt, Vector2 target, float duration, Ease ease, float delay, CancellationToken ct)
    {
        if (delay > 0f)
            await UniTask.Delay((int)(delay * 1000), cancellationToken: ct);

        var tcs = new UniTaskCompletionSource();
        rt.DOAnchorPos(target, duration)
          .SetEase(ease)
          .OnComplete(() => tcs.TrySetResult())
          .OnKill   (() => tcs.TrySetResult());
        await tcs.Task;
    }
}
