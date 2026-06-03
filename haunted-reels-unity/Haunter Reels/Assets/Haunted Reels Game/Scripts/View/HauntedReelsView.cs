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

    private int   _currentBet, _minBet, _maxBet, _paylineCount, _coins;
    private float _coinsFloat;

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
        _coins = coins;
        if (_coinsText != null) _coinsText.text = FormatCoins(coins);
        if (_betPanel != null && _betPanel.activeSelf)
            ValidateInput(_betInput != null ? _betInput.text : "");
    }

    public void UpdateCoinsFloat(float coins)
    {
        _coinsFloat = coins;
        _coins = (int)coins;
        if (_coinsText != null)
            _coinsText.text = FormatCoins(coins);
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

        if (response.spin.totalWin > 0)
            await UniTask.Delay(750);

        LogSpinResult(response.spin);
        ShowWinInfo(response.spin.totalWin, response.spin.winLevel);
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

        var winGroups = new List<(List<(int col, int row)> cells, float coins, float multiplier, int symbolId)>();

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
                    winGroups.Add((group, win.coins, win.multiplier, win.symbolId));
            }

        if (result.spin.scatterCount >= 3 && result.spin.scatterPositions != null && result.spin.scatterPositions.Length > 0)
        {
            var sc = new List<(int col, int row)>();
            foreach (var pos in result.spin.scatterPositions)
                if (pos.Length >= 2) sc.Add((pos[0], pos[1]));
            if (sc.Count > 0)
                winGroups.Add((sc, result.spin.scatterCoins, 0f, SymbolId.Scatter));
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
            var (cells, coins, multiplier, symbolId) = winGroups[i];
            var groupSet = new HashSet<(int col, int row)>(cells);

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

        // Restaura opacidade de todos os slots
        foreach (var cg in allSlots.Values)
            if (cg != null) cg.DOFade(1f, 0.25f);
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
    //  Efeito Trovão
    // ═════════════════════════════════════════════════════════════════════════

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
