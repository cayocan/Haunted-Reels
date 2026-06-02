using System;
using SlotEngine;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HauntedReelsView : GenericSlotMachineView
{
    [Header("Aposta")]
    [SerializeField] private TMP_Text       _betDisplayText;  // exibe aposta total fora do painel
    [SerializeField] private Button         _openPanelButton; // abre o painel
    [SerializeField] private GameObject     _betPanel;
    [SerializeField] private TMP_InputField _betInput;
    [SerializeField] private Button         _confirmButton;
    [SerializeField] private Button         _cancelButton;
    [SerializeField] private TMP_Text       _betErrorText;    // opcional: motivo da invalidade

    public event Action<int> OnBetSetRequested;

    private int _currentBet, _minBet, _maxBet, _paylineCount, _coins;

    private void Start()
    {
        if (_betPanel        != null) _betPanel.SetActive(false);
        if (_openPanelButton != null) _openPanelButton.onClick.AddListener(OpenPanel);
        if (_confirmButton   != null) _confirmButton.onClick.AddListener(OnConfirm);
        if (_cancelButton    != null) _cancelButton.onClick.AddListener(ClosePanel);
        if (_betInput        != null) _betInput.onValueChanged.AddListener(ValidateInput);
        if (_betErrorText    != null) _betErrorText.gameObject.SetActive(false);
    }

    // shadowing: mantém base + armazena dados para validação
    public new void UpdateBetPerLine(int bet, int minBet, int maxBet, int totalBet)
    {
        base.UpdateBetPerLine(bet, minBet, maxBet, totalBet);
        _currentBet   = bet;
        _minBet       = minBet;
        _maxBet       = maxBet;
        _paylineCount = bet > 0 ? totalBet / bet : 1;
        if (_betDisplayText != null) _betDisplayText.text = totalBet.ToString();
    }

    // shadowing: armazena moedas e re-valida se o painel estiver aberto
    public new void UpdateCoins(int coins)
    {
        base.UpdateCoins(coins);
        _coins = coins;
        if (_betPanel != null && _betPanel.activeSelf)
            ValidateInput(_betInput != null ? _betInput.text : "");
    }

    // bloqueia abertura do painel enquanto o reel está girando
    public new void SetSpinInteractable(bool interactable)
    {
        base.SetSpinInteractable(interactable);
        if (_openPanelButton != null) _openPanelButton.interactable = interactable;
        // fecha o painel se um spin começar com ele aberto
        if (!interactable && _betPanel != null) _betPanel.SetActive(false);
    }

    private void OpenPanel()
    {
        // pré-preenche com aposta total (o que o usuário entende como sua aposta)
        if (_betInput != null) _betInput.text = (_currentBet * _paylineCount).ToString();
        if (_betPanel != null) _betPanel.SetActive(true);
        ValidateInput(_betInput != null ? _betInput.text : "");
    }

    private void ClosePanel()
    {
        if (_betPanel     != null) _betPanel.SetActive(false);
        if (_betErrorText != null) _betErrorText.gameObject.SetActive(false);
    }

    private void OnConfirm()
    {
        if (_betInput != null && int.TryParse(_betInput.text, out int totalBetValue) && _paylineCount > 0)
        {
            // converte aposta total → betPerLine antes de enviar ao contexto
            int betPerLine = Mathf.Clamp(totalBetValue / _paylineCount, _minBet, _maxBet);
            OnBetSetRequested?.Invoke(betPerLine);
        }
        ClosePanel();
    }

    // valida em tempo real enquanto o usuário digita
    private void ValidateInput(string text)
    {
        bool parsed = int.TryParse(text, out int value);
        string error = null;

        if (!parsed || value <= 0)
            error = "Valor inválido.";
        else if (value < _minBet)
            error = $"Mínimo: {_minBet}";
        else if (value > _maxBet)
            error = $"Máximo: {_maxBet}";
        else if (value > _coins)
            error = "Saldo insuficiente.";

        bool valid = error == null;
        if (_confirmButton != null) _confirmButton.interactable = valid;
        if (_betErrorText  != null)
        {
            _betErrorText.gameObject.SetActive(!valid && text.Length > 0);
            if (!valid && text.Length > 0) _betErrorText.text = error;
        }
    }

    protected override int[][] GetSpinGrid(ISpinResult result)
    {
        if (result is SpinResponse sr) return sr.spin?.grid;
        return null;
    }

    protected override int GetTotalWin(ISpinResult result)
    {
        if (result is SpinResponse sr && sr.spin != null)
            return Mathf.FloorToInt(sr.spin.totalWin);
        return 0;
    }
}
