using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using SlotEngine;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// View principal da máquina caça-níquel. Implementa <see cref="ISlotMachineView"/> do SlotEngine
/// e controla toda a apresentação visual do jogo: reels, painel de aposta, painel de vitória,
/// animações de paylines vencedoras e o efeito de trovão do Scatter.
/// </summary>
/// <remarks>
/// Fluxo de spin:
/// <list type="number">
///   <item><see cref="StartSpinVisual"/> — inicia a animação de giro em todos os reels.</item>
///   <item><see cref="StopSpinVisualAsync"/> — para os reels em cascata com delay,
///         anima os símbolos vencedores, renderiza as paylines e exibe o painel de ganho.</item>
/// </list>
/// Efeitos visuais:
/// <list type="bullet">
///   <item>Símbolos H1-H3 usam animações Spine ("die"/"idle").</item>
///   <item>Símbolos L1-L3 e Wild usam sequências DOTween (scale + rotation).</item>
///   <item>Scatter dispara o efeito de flash de trovão via <see cref="PlayThunderFlash"/>.</item>
/// </list>
/// </remarks>
public class HauntedReelsView : MonoBehaviour, ISlotMachineView
{
    // ── Reels ─────────────────────────────────────────────────────────────────

    [Header("Grid (5 Reels)")]
    [SerializeField] private ReelStrip[]    _reels;
    [SerializeField] private SymbolLibrary  _symbolLibrary;

    // ── Sessão ────────────────────────────────────────────────────────────────

    [Header("Estado da Sessão")]
    [SerializeField] private TMP_Text   _coinsText;
    [SerializeField] private TMP_Text   _freeSpinsText;
    [SerializeField] private GameObject _freeSpinPanel;

    // ── Painel de vitória ─────────────────────────────────────────────────────

    [Header("Info de Vitória")]
    [SerializeField] private GameObject _winInfoPanel;
    [SerializeField] private TMP_Text   _totalWinText;
    [SerializeField] private GameObject _winHeaderSmall;
    [SerializeField] private GameObject _winHeaderBig;
    [SerializeField] private GameObject _winHeaderMega;
    [SerializeField] private GameObject _winHeaderJackpot;

    [Header("Contador de Ganho")]
    [SerializeField] private TMP_Text _runningWinText; // multiplicador da linha atual
    [SerializeField] private TMP_Text _prizeText;      // prêmio acumulado crescendo

    // ── Botões ────────────────────────────────────────────────────────────────

    [Header("Botões")]
    [SerializeField] private Button     _spinButton;
    [SerializeField] private Button     _autoSpinButton;
    [SerializeField] private GameObject _autoSpinActiveIndicator;

    // ── Painel de aposta ──────────────────────────────────────────────────────

    [Header("Aposta")]
    [SerializeField] private TMP_Text       _betDisplayText;
    [SerializeField] private Button         _openPanelButton;
    [SerializeField] private GameObject     _betPanel;
    [SerializeField] private TMP_InputField _betInput;
    [SerializeField] private Button         _confirmButton;
    [SerializeField] private Button         _cancelButton;
    [SerializeField] private TMP_Text       _betErrorText;

    // ── Efeitos ───────────────────────────────────────────────────────────────

    [Header("Animações Spine — H1/H2/H3")]
    [Tooltip("Nome da animação de morte no skeleton Spine dos símbolos altos.")]
    [SerializeField] private string _spineAnimDie  = "die";
    [Tooltip("Nome da animação idle no skeleton Spine dos símbolos altos.")]
    [SerializeField] private string _spineAnimIdle = "idle";

    [Header("Efeitos")]
    [SerializeField] private ParticleSystem _multiplierParticles;

    [Header("Payline Renderer")]
    [SerializeField] private UIPaylineRenderer _paylineRenderer;
    [SerializeField] private Canvas            _canvas;

    [Header("Efeito Trovão (Scatter)")]
    [SerializeField] private Image          _thunderFlashImage;
    [SerializeField] private AnimationCurve _thunderAlphaCurve;
    [SerializeField] private float          _thunderDuration = 1f;

    private Coroutine _thunderCoroutine;

    [Header("Animação de Giro")]
    [SerializeField] private int _reelStopDelayMs = 300;

    [Header("Debug")]
    [SerializeField] private bool _debugLogGrid;

    // ── Eventos (consumidos pelo SlotMachinePresenter) ────────────────────────

    public event Action     OnSpinRequested;
    public event Action     OnAutoSpinToggled;
    public event Action<int> OnBetSetRequested;

    // ── Estado interno do painel de aposta ────────────────────────────────────

    private int   _currentBet, _minBet, _maxBet, _paylineCount;
    private float _coins, _coinsFloat;
    private bool  _floatCoinsInitialized;

    // ═════════════════════════════════════════════════════════════════════════
    //  Ciclo de vida
    // ═════════════════════════════════════════════════════════════════════════

    private void Reset()
    {
        _thunderAlphaCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 0f);
    }

    private void Awake()
    {
        if (_reels != null && _symbolLibrary != null)
            foreach (var reel in _reels)
                if (reel != null) reel.Init(_symbolLibrary);

        if (_spinButton     != null) _spinButton.onClick.AddListener(() => { HauntedAudioManager.Instance?.Play("ConfirmClick"); OnSpinRequested?.Invoke(); });
        if (_autoSpinButton != null) _autoSpinButton.onClick.AddListener(() => { HauntedAudioManager.Instance?.Play("ConfirmClick"); OnAutoSpinToggled?.Invoke(); });
    }

    private void Start()
    {
        if (_betPanel        != null) _betPanel.SetActive(false);
        if (_openPanelButton != null) _openPanelButton.onClick.AddListener(OpenPanel);
        if (_confirmButton   != null) _confirmButton.onClick.AddListener(OnConfirm);
        if (_cancelButton    != null) _cancelButton.onClick.AddListener(ClosePanel);
        if (_betInput        != null) _betInput.onValueChanged.AddListener(ValidateInput);
        if (_betErrorText    != null) _betErrorText.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_reels != null)
            foreach (var reel in _reels)
                if (reel != null) reel.KillSpin();

        if (_spinButton     != null) _spinButton.onClick.RemoveAllListeners();
        if (_autoSpinButton != null) _autoSpinButton.onClick.RemoveAllListeners();
        if (_openPanelButton != null) _openPanelButton.onClick.RemoveListener(OpenPanel);
        if (_confirmButton   != null) _confirmButton.onClick.RemoveListener(OnConfirm);
        if (_cancelButton    != null) _cancelButton.onClick.RemoveListener(ClosePanel);
        if (_betInput        != null) _betInput.onValueChanged.RemoveListener(ValidateInput);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  ISlotMachineView
    // ═════════════════════════════════════════════════════════════════════════

    public void SetSpinInteractable(bool interactable)
    {
        if (_spinButton != null) _spinButton.interactable = interactable;
        if (_openPanelButton != null) _openPanelButton.interactable = interactable;
        if (!interactable && _betPanel != null) _betPanel.SetActive(false);
    }

    public void UpdateCoins(int coins)
    {
        // O SlotEngine chama este método com model.Coins (int truncado).
        // Após a primeira chamada a UpdateCoinsFloat, o display passa a ser
        // controlado exclusivamente por UpdateCoinsFloat/StopSpinVisualAsync
        // para nunca perder centavos.
        if (!_floatCoinsInitialized)
        {
            _coins      = coins;
            _coinsFloat = coins;
            if (_coinsText != null) _coinsText.text = FormatCoins(coins);
        }
        if (_betPanel != null && _betPanel.activeSelf)
            ValidateInput(_betInput != null ? _betInput.text : "");
    }

    public void UpdateCoinsFloat(float coins)
    {
        _floatCoinsInitialized = true;
        _coins      = coins;
        _coinsFloat = coins;
        if (_coinsText != null) _coinsText.text = FormatCoins(coins);
        if (_betPanel != null && _betPanel.activeSelf)
            ValidateInput(_betInput != null ? _betInput.text : "");
    }

    private static string FormatCoins(float coins) => $"${coins:N2}";

    public void UpdateFreeSpins(int remaining)
    {
        if (_freeSpinsText != null) _freeSpinsText.text = $"{remaining}";

        // Partículas acompanham o estado do free spin
        if (_multiplierParticles != null)
        {
            if (remaining > 0)
            {
                if (!_multiplierParticles.isPlaying)
                    _multiplierParticles.Play();
            }
            else
            {
                _multiplierParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        if (_freeSpinPanel == null) return;

        if (remaining > 0)
        {
            if (!_freeSpinPanel.activeSelf)
            {
                _freeSpinPanel.transform.localScale = Vector3.zero;
                _freeSpinPanel.SetActive(true);
                _freeSpinPanel.transform.DOKill();
                DOTween.Sequence()
                    .Append(_freeSpinPanel.transform.DOScale(Vector3.one * 1.15f, 0.22f).SetEase(Ease.OutQuad))
                    .Append(_freeSpinPanel.transform.DOScale(Vector3.one,          0.12f).SetEase(Ease.InQuad));
            }
        }
        else if (_freeSpinPanel.activeSelf)
        {
            _freeSpinPanel.transform.DOKill();
            DOTween.Sequence()
                .Append(_freeSpinPanel.transform.DOScale(Vector3.one * 1.15f, 0.12f).SetEase(Ease.OutQuad))
                .Append(_freeSpinPanel.transform.DOScale(Vector3.zero,         0.22f).SetEase(Ease.InQuad))
                .OnComplete(() => _freeSpinPanel.SetActive(false));
        }
    }

    public void SetAutoSpinActive(bool active)
    {
        if (_autoSpinActiveIndicator != null) _autoSpinActiveIndicator.SetActive(active);
    }

    public void StartSpinVisual()
    {
        SetWinPanelVisible(false);

        if (_runningWinText != null) _runningWinText.gameObject.SetActive(false);
        if (_prizeText      != null) _prizeText.text = "";
        // Partículas só param aqui se não estivermos em free spins (controladas por UpdateFreeSpins)

        if (_reels == null) return;
        foreach (var reel in _reels)
            if (reel != null) reel.StartSpin();
    }

    /// <summary>
    /// Para os reels em cascata (col 0 → 4 com <see cref="_reelStopDelayMs"/> entre cada),
    /// anima os símbolos vencedores por grupo de payline, renderiza as linhas no canvas
    /// e exibe o painel de ganho. O saldo só é atualizado na UI após todas as animações.
    /// </summary>
    /// <param name="result">Resultado do spin retornado pelo backend, convertido para ISpinResult.</param>
    public async UniTask StopSpinVisualAsync(ISpinResult result)
    {
        var response = result as SpinResponse;
        if (_reels == null) return;

        if (response?.spin?.grid == null)
        {
            var abortTasks = new UniTask[_reels.Length];
            for (int i = 0; i < _reels.Length; i++)
                abortTasks[i] = _reels[i] != null ? _reels[i].StopSpinAsync(null) : UniTask.CompletedTask;
            await UniTask.WhenAll(abortTasks);
            return;
        }

        var tasks = new UniTask[_reels.Length];
        for (int col = 0; col < _reels.Length; col++)
        {
            var reel    = _reels[col];
            var symbols = col < response.spin.grid.Length ? response.spin.grid[col] : new int[3];
            tasks[col]  = StopReelAsync(reel, symbols, col * _reelStopDelayMs);
        }
        await UniTask.WhenAll(tasks);

        if (_debugLogGrid) LogGrid(response.spin.grid);

        await AnimateWinnersAsync(response);

        LogSpinResult(response.spin);
        ShowWinInfo(response.spin.totalWin, response.spin.winLevel);

        if (response.spin.totalWin > 0)
            await UniTask.Delay(750);

        // Atualiza saldo somente após exibir o ganho
        if (response?.session != null)
            UpdateCoinsFloat(response.session.coins);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Painel de aposta
    // ═════════════════════════════════════════════════════════════════════════

    // shadowing do método não-virtual da base (não herdado aqui, mas compatível com Presenter)
    public void UpdateBetPerLine(int bet, int minBet, int maxBet, int totalBet)
    {
        _currentBet   = bet;
        _minBet       = minBet;
        _maxBet       = maxBet;
        _paylineCount = bet > 0 ? totalBet / bet : 1;
        if (_betDisplayText != null) _betDisplayText.text = $"${totalBet:N2}";
    }

    private void OpenPanel()
    {
        HauntedAudioManager.Instance?.Play("ConfirmClick");
        if (_betInput != null) _betInput.text = (_currentBet * _paylineCount).ToString();
        if (_betPanel != null) _betPanel.SetActive(true);
        ValidateInput(_betInput != null ? _betInput.text : "");
    }

    private void ClosePanel()
    {
        HauntedAudioManager.Instance?.Play("CancelClick");
        if (_betPanel     != null) _betPanel.SetActive(false);
        if (_betErrorText != null) _betErrorText.gameObject.SetActive(false);
    }

    private void OnConfirm()
    {
        HauntedAudioManager.Instance?.Play("ConfirmClick");
        if (_betInput != null && int.TryParse(_betInput.text, out int totalBetValue) && _paylineCount > 0)
        {
            int betPerLine = Mathf.Clamp(totalBetValue / _paylineCount, _minBet, _maxBet);
            OnBetSetRequested?.Invoke(betPerLine);
        }
        ClosePanel();
    }

    private void ValidateInput(string text)
    {
        bool parsed = int.TryParse(text, out int value);
        string error = null;

        if (!parsed || value <= 0)        error = "Valor inválido.";
        else if (value < _minBet)         error = $"Mínimo: {_minBet}";
        else if (value > _maxBet)         error = $"Máximo: {_maxBet}";
        else if (value > _coins)          error = "Saldo insuficiente.";

        bool valid = error == null;
        if (_confirmButton != null) _confirmButton.interactable = valid;
        if (_betErrorText  != null)
        {
            _betErrorText.gameObject.SetActive(!valid && text.Length > 0);
            if (!valid && text.Length > 0) _betErrorText.text = error;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Animação de vencedores
    // ═════════════════════════════════════════════════════════════════════════

    private async UniTask AnimateWinnersAsync(SpinResponse result)
    {
        bool hasLineWins    = result.spin.lineWins != null && result.spin.lineWins.Length > 0;
        bool hasScatterWin  = result.spin.scatterCount >= 3;
        if (result?.spin == null || (result.spin.totalWin <= 0 && !hasLineWins && !hasScatterWin)) return;

        var winGroups = new List<(List<(int col, int row)> cells, float coins, float multiplier, int symbolId, int count, int[][] linePath)>();

        if (result.spin.lineWins != null)
            foreach (var win in result.spin.lineWins)
            {
                var group = new List<(int col, int row)>();
                if (win.cells != null)
                    foreach (var cell in win.cells)
                        if (cell.Length >= 2) group.Add((cell[0], cell[1]));
                else
                    for (int col = 0; col < win.count; col++)
                        group.Add((col, 1));
                if (group.Count > 0)
                    winGroups.Add((group, win.coins, win.multiplier, win.symbolId, win.count, win.linePath));
            }

        if (result.spin.scatterCount >= 3 && result.spin.scatterPositions != null && result.spin.scatterPositions.Length > 0)
        {
            var sc = new List<(int col, int row)>();
            foreach (var pos in result.spin.scatterPositions)
                if (pos.Length >= 2) sc.Add((pos[0], pos[1]));
            if (sc.Count > 0)
                winGroups.Add((sc, result.spin.scatterCoins, 0f, SymbolId.Scatter, 0, null));
        }

        if (winGroups.Count == 0) return;

        // Mapeia todos os slots para seus CanvasGroups (controle de alpha por payline)
        var allSlots = new Dictionary<(int col, int row), CanvasGroup>();
        if (_reels != null)
            for (int col = 0; col < _reels.Length; col++)
            {
                if (_reels[col] == null) continue;
                for (int row = 0; row < 3; row++)
                {
                    var inst = _reels[col].GetResultInstance(row);
                    if (inst == null) continue;
                    var cg = inst.GetComponent<CanvasGroup>();
                    if (cg == null)
                    {
                        Debug.LogWarning($"[HauntedReelsView] CanvasGroup ausente em '{inst.name}' — adicione ao prefab.");
                        continue;
                    }
                    allSlots[(col, row)] = cg;
                }
            }


        float accumulated = 0f;

        if (_runningWinText != null)
        {
            _runningWinText.transform.localScale = Vector3.zero;
            _runningWinText.text = string.Empty;
            _runningWinText.gameObject.SetActive(false);
        }
        if (_prizeText != null) _prizeText.text = "$0.00";

        for (int i = 0; i < winGroups.Count; i++)
        {
            var (cells, coins, multiplier, symbolId, count, linePath) = winGroups[i];
            var groupSet = new HashSet<(int col, int row)>(cells);

            ShowPayline(linePath, count);

            // Slots da payline atual = alpha 1, demais = 0.3
            foreach (var kvp in allSlots)
                kvp.Value.DOFade(groupSet.Contains(kvp.Key) ? 1f : 0.3f, 0.2f);

            if (symbolId == SymbolId.Scatter)
            {
                HauntedAudioManager.Instance?.Play("Thunder");
                PlayThunderFlash();
            }

            if (cells.Count > 0)
                await AnimateCellGroupAsync(cells, symbolId);

            if (_runningWinText != null)
            {
                if (multiplier > 0)
                {
                    _runningWinText.text = $"×{multiplier}";
                    if (!_runningWinText.gameObject.activeSelf)
                    {
                        _runningWinText.transform.localScale = Vector3.zero;
                        _runningWinText.gameObject.SetActive(true);
                    }
                    _runningWinText.transform.DOKill();
                    DOTween.Sequence()
                        .Append(_runningWinText.transform.DOScale(Vector3.one * 1.15f, 0.22f).SetEase(Ease.OutQuad))
                        .Append(_runningWinText.transform.DOScale(Vector3.one,          0.12f).SetEase(Ease.InQuad));
                }
                else if (_runningWinText.gameObject.activeSelf)
                {
                    _runningWinText.transform.DOKill();
                    DOTween.Sequence()
                        .Append(_runningWinText.transform.DOScale(Vector3.one * 1.15f, 0.12f).SetEase(Ease.OutQuad))
                        .Append(_runningWinText.transform.DOScale(Vector3.zero,         0.22f).SetEase(Ease.InQuad))
                        .OnComplete(() => _runningWinText.gameObject.SetActive(false));
                }
            }

            accumulated += coins;
            if (_prizeText != null)
                _prizeText.text = $"+${accumulated:N2}";

            if (i == winGroups.Count - 1)
                await UniTask.Delay(300);
        }

        // Restaura opacidade de todos os slots e esconde a payline
        foreach (var cg in allSlots.Values)
            if (cg != null) cg.DOFade(1f, 0.25f);
        if (_paylineRenderer != null) _paylineRenderer.Clear();
    }

    private async UniTask AnimateCellGroupAsync(List<(int col, int row)> group, int symbolId)
    {
        bool isHighSymbol = symbolId is SymbolId.Witch or SymbolId.Pumpkin or SymbolId.Skull;
        var pending = new List<UniTaskCompletionSource>();

        foreach (var (col, row) in group)
        {
            if (col < 0 || col >= _reels.Length)
            {
                Debug.LogWarning($"[HauntedReelsView] Célula fora do intervalo: col={col} (reels={_reels?.Length})");
                continue;
            }
            if (_reels[col] == null)
            {
                Debug.LogWarning($"[HauntedReelsView] _reels[{col}] é null — atribua todos os 5 reels no Inspector.");
                continue;
            }
            var instance = _reels[col].GetResultInstance(row);
            if (instance == null)
            {
                Debug.LogWarning($"[HauntedReelsView] GetResultInstance retornou null para col={col} row={row} (symbolId={symbolId})");
                continue;
            }

            var tcs = new UniTaskCompletionSource();
            pending.Add(tcs);

            // Tenta Spine apenas se for símbolo alto E tiver SkeletonGraphic (Wild é sprite puro)
            var skeletonForCell = isHighSymbol
                ? (instance.GetComponent<SkeletonGraphic>() ?? instance.GetComponentInChildren<SkeletonGraphic>())
                : null;

            if (skeletonForCell != null)
            {
                AnimateHighSymbolAsync(instance.transform, skeletonForCell, tcs).Forget();
            }
            else
            {
                // DOTween: sprites puros (L1-L3, Wild) e fallback de H1-H3 sem Spine
                var tr = instance.transform;
                DOTween.Sequence()
                    .Append(tr.DOScale(Vector3.one * 1.30f, 0.15f).SetEase(Ease.OutBack))
                    .Append(tr.DOLocalRotate(new Vector3(0f, 0f, -12f), 0.12f).SetEase(Ease.OutQuad))
                    .Append(tr.DOLocalRotate(new Vector3(0f, 0f,  12f), 0.24f).SetEase(Ease.InOutQuad))
                    .Append(tr.DOLocalRotate(Vector3.zero,              0.12f).SetEase(Ease.InQuad))
                    .Join  (tr.DOScale(Vector3.one,                     0.12f).SetEase(Ease.InQuad))
                    .SetLoops(2)
                    .OnComplete(() => tcs.TrySetResult())
                    .OnKill   (() => tcs.TrySetResult());
            }
        }

        if (pending.Count > 0)
        {
            var waitAll = new UniTask[pending.Count];
            for (int i = 0; i < pending.Count; i++) waitAll[i] = pending[i].Task;
            await UniTask.WhenAll(waitAll);
        }
    }

    // zoom in → "die" → zoom out → "die" reverso → "idle" loop
    private async UniTaskVoid AnimateHighSymbolAsync(Transform tr, SkeletonGraphic skeleton, UniTaskCompletionSource tcs)
    {
        try
        {
            tr.DOScale(Vector3.one * 1.15f, 0.12f).SetEase(Ease.OutQuad);

            if (skeleton != null)
            {
                // valida se a animação existe antes de tentar tocar
                var dieAnim = skeleton.Skeleton.Data.FindAnimation(_spineAnimDie);
                if (dieAnim == null)
                {
                    Debug.LogError($"[HauntedReelsView] Animação '{_spineAnimDie}' não encontrada no skeleton '{skeleton.Skeleton.Data.Name}'. " +
                                   $"Animações disponíveis: {string.Join(", ", System.Linq.Enumerable.Select(skeleton.Skeleton.Data.Animations, a => a.Name))}");
                    goto fallback;
                }

                float dieDuration = dieAnim.Duration;

                var dieEntry = skeleton.AnimationState.SetAnimation(0, _spineAnimDie, false);
                await UniTask.Delay(Mathf.Max(50, (int)(dieDuration * 1000)));

                tr.DOScale(Vector3.one, 0.12f).SetEase(Ease.InQuad);

                // "die" ao contrário: começa do fim e vai para o início
                var reverseEntry = skeleton.AnimationState.SetAnimation(0, _spineAnimDie, false);
                reverseEntry.TrackTime = dieDuration;
                reverseEntry.TimeScale = -1f;

                await UniTask.Delay(Mathf.Max(50, (int)(dieDuration * 1000)));

                skeleton.AnimationState.SetAnimation(0, _spineAnimIdle, true);
                goto done;
            }

            fallback:
            // Wild ou sprite sem Spine na linha de um símbolo alto: animação DOTween visível
            tr.DOKill();
            DOTween.Sequence()
                .Append(tr.DOScale(Vector3.one * 1.30f, 0.15f).SetEase(Ease.OutBack))
                .Append(tr.DOLocalRotate(new Vector3(0f, 0f, -12f), 0.12f).SetEase(Ease.OutQuad))
                .Append(tr.DOLocalRotate(new Vector3(0f, 0f,  12f), 0.24f).SetEase(Ease.InOutQuad))
                .Append(tr.DOLocalRotate(Vector3.zero,              0.12f).SetEase(Ease.InQuad))
                .Join  (tr.DOScale(Vector3.one,                     0.12f).SetEase(Ease.InQuad))
                .SetLoops(2)
                .OnComplete(() => tcs.TrySetResult())
                .OnKill   (() => tcs.TrySetResult());
            return;

            done:
            tcs.TrySetResult();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[HauntedReelsView] AnimateHighSymbolAsync falhou: {ex.Message}");
            tcs.TrySetResult();
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Painel de ganho
    // ═════════════════════════════════════════════════════════════════════════

    private void ShowWinInfo(float totalWin, string winLevel)
    {
        bool hasWin = totalWin > 0;
        SetWinPanelVisible(hasWin);

        if (_totalWinText != null)
            _totalWinText.text = hasWin ? $"+{totalWin:N2}" : string.Empty;

        if (_winHeaderSmall   != null) _winHeaderSmall.SetActive(hasWin && winLevel == WinLevel.Small);
        if (_winHeaderBig     != null) _winHeaderBig.SetActive(hasWin && winLevel == WinLevel.Big);
        if (_winHeaderMega    != null) _winHeaderMega.SetActive(hasWin && winLevel == WinLevel.Mega);
        if (_winHeaderJackpot != null) _winHeaderJackpot.SetActive(hasWin && winLevel == WinLevel.Jackpot);
    }

    private void SetWinPanelVisible(bool visible)
    {
        if (_winInfoPanel == null) return;

        if (visible)
        {
            if (!_winInfoPanel.activeSelf)
            {
                _winInfoPanel.transform.localScale = Vector3.zero;
                _winInfoPanel.SetActive(true);
                HauntedAudioManager.Instance?.Play("WinFunfair");
            }
            _winInfoPanel.transform.DOKill();
            DOTween.Sequence()
                .Append(_winInfoPanel.transform.DOScale(Vector3.one * 1.15f, 0.22f).SetEase(Ease.OutQuad))
                .Append(_winInfoPanel.transform.DOScale(Vector3.one,          0.12f).SetEase(Ease.InQuad));
        }
        else if (_winInfoPanel.activeSelf)
        {
            _winInfoPanel.transform.DOKill();
            DOTween.Sequence()
                .Append(_winInfoPanel.transform.DOScale(Vector3.one * 1.15f, 0.12f).SetEase(Ease.OutQuad))
                .Append(_winInfoPanel.transform.DOScale(Vector3.zero,         0.22f).SetEase(Ease.InQuad))
                .OnComplete(() => _winInfoPanel.SetActive(false));
        }

        if (visible && _runningWinText != null)
            _runningWinText.gameObject.SetActive(false);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Payline Renderer
    // ═════════════════════════════════════════════════════════════════════════

    private void ShowPayline(int[][] linePath, int litCount)
    {
        if (_paylineRenderer == null || linePath == null || linePath.Length < 2) return;

        var cam    = _canvas != null ? _canvas.worldCamera : null;
        var points = new List<Vector2>(linePath.Length);

        foreach (var pos in linePath)
        {
            if (pos.Length < 2) continue;
            int col = pos[0], row = pos[1];
            if (col < 0 || col >= _reels.Length || _reels[col] == null) continue;

            var inst = _reels[col].GetResultInstance(row);
            if (inst == null) continue;

            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, inst.transform.position);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _paylineRenderer.rectTransform, screen, cam, out Vector2 local))
                points.Add(local);
        }

        if (points.Count >= 2)
            _paylineRenderer.SetLine(points, litCount);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Efeito Trovão
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Dispara o efeito de flash de trovão (Scatter). Avalia a <see cref="_thunderAlphaCurve"/>
    /// ao longo de <see cref="_thunderDuration"/> segundos para animar a opacidade da <see cref="_thunderFlashImage"/>.
    /// Se já estiver em execução, reinicia do início.
    /// </summary>
    public void PlayThunderFlash()
    {
        if (_thunderFlashImage == null) return;
        if (_thunderCoroutine != null) StopCoroutine(_thunderCoroutine);
        _thunderCoroutine = StartCoroutine(ThunderFlashRoutine());
    }

    private IEnumerator ThunderFlashRoutine()
    {
        float elapsed = 0f;
        var color = _thunderFlashImage.color;

        while (elapsed < _thunderDuration)
        {
            elapsed += Time.deltaTime;
            color.a = _thunderAlphaCurve.Evaluate(Mathf.Clamp01(elapsed / _thunderDuration));
            _thunderFlashImage.color = color;
            yield return null;
        }

        color.a = _thunderAlphaCurve.Evaluate(1f);
        _thunderFlashImage.color = color;
        _thunderCoroutine = null;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Helpers
    // ═════════════════════════════════════════════════════════════════════════

    private static async UniTask StopReelAsync(ReelStrip reel, int[] symbols, int delayMs)
    {
        if (reel == null) return;
        if (delayMs > 0) await UniTask.Delay(delayMs);
        await reel.StopSpinAsync(symbols);
    }

    private static void LogSpinResult(SpinData spin)
    {
        var sb = new StringBuilder("[HauntedReelsView] ");

        bool hasWin = spin.totalWin > 0
            || (spin.lineWins != null && spin.lineWins.Length > 0)
            || spin.scatterCount >= 3;
        if (hasWin)
            sb.Append($"Resultado: +{spin.totalWin:N2} moedas ({spin.winLevel})");
        else
            sb.Append("Resultado: sem ganho");

        if (spin.lineWins != null && spin.lineWins.Length > 0)
        {
            sb.AppendLine();
            sb.Append("  Paylines ganhadoras:");
            foreach (var w in spin.lineWins)
            {
                string name = !string.IsNullOrEmpty(w.lineName) ? w.lineName : $"Payline {w.lineId}";
                sb.AppendLine();
                sb.Append($"    • [{w.lineId}] {name} — {SymbolName(w.symbolId)} ×{w.count} → mult {w.multiplier} = {w.coins:N2} moedas");
            }
        }

        if (spin.scatterCount >= 3)
        {
            sb.AppendLine();
            sb.Append($"  Scatter: {spin.scatterCount}× cauldrons → {spin.scatterCoins} moedas");
        }

        Debug.Log(sb.ToString());
    }

    private static string SymbolName(int id) => id switch
    {
        SymbolId.Witch   => "Witch",
        SymbolId.Pumpkin => "Pumpkin",
        SymbolId.Skull   => "Skull",
        SymbolId.Bat     => "Bat",
        SymbolId.Spider  => "Spider",
        SymbolId.Potion  => "Potion",
        SymbolId.Wild    => "Wild",
        SymbolId.Scatter => "Scatter",
        _                => $"#{id}"
    };

    private static void LogGrid(int[][] grid)
    {
        var sb = new StringBuilder("[HauntedReelsView] Grid (col→ / row↓):\n");
        for (int row = 0; row < grid[0].Length; row++)
        {
            sb.Append($"  row {row}:");
            for (int col = 0; col < grid.Length; col++)
                sb.Append($"  {grid[col][row],2}");
            sb.AppendLine();
        }
        Debug.Log(sb.ToString());
    }
}
