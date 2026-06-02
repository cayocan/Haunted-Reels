using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SessionView : MonoBehaviour, ISessionView
{
    [Header("Estado da Sessão")]
    [SerializeField] private TMP_Text _coinsText;
    [SerializeField] private TMP_Text _freeSpinsText;
    [SerializeField] private TMP_Text _serverSeedHashText;
    [SerializeField] private TMP_Text _clientSeedText;

    [Header("Feedback")]
    [SerializeField] private GameObject _loadingPanel;
    [SerializeField] private TMP_Text   _errorText;

    [Header("Botões")]
    [SerializeField] private Button _rotateSeedButton;

    private void Awake()
    {
        SetError(null);
        SetLoading(false);
    }

    private void Start()
    {
        SessionPresenter.Instance.SetView(this);
        _rotateSeedButton?.onClick.AddListener(OnRotateSeedClicked);
    }

    private void OnDestroy()
    {
        SessionPresenter.Instance?.ClearView();
        _rotateSeedButton?.onClick.RemoveListener(OnRotateSeedClicked);
    }

    private void OnRotateSeedClicked() => RotateSeedAsync().Forget();

    private async UniTaskVoid RotateSeedAsync()
    {
        await SessionPresenter.Instance.RequestRotateSeedAsync();
    }

    public void ShowLoading(bool isLoading)
    {
        SetLoading(isLoading);
        if (_rotateSeedButton != null) _rotateSeedButton.interactable = !isLoading;
    }

    public void UpdateCoins(int coins)
    {
        if (_coinsText != null)
            _coinsText.text = coins.ToString("N0");
    }

    public void UpdateFreeSpins(int freeSpinsRemaining)
    {
        if (_freeSpinsText != null)
        {
            _freeSpinsText.gameObject.SetActive(freeSpinsRemaining > 0);
            _freeSpinsText.text = $"Free Spins: {freeSpinsRemaining}";
        }
    }

    public void UpdateServerSeedHash(string hash)
    {
        if (_serverSeedHashText != null)
            _serverSeedHashText.text = TruncateHash(hash);
    }

    public void UpdateClientSeed(string seed)
    {
        if (_clientSeedText != null)
            _clientSeedText.text = TruncateHash(seed);
    }

    public void ShowSpinResult(SpinResponse result)
    {
        SetError(null);
        if (result?.spin == null) return;
        if (result.spin.totalWin > 0)
            ShowWinFeedback(result.spin.totalWin, result.spin.winLevel);
    }

    public void ShowRotateResult(RotateResponse result)
    {
        if (result?.revealed == null) return;
        Debug.Log($"[SessionView] Seed revelado: {result.revealed.serverSeed} | " +
                  $"Nonces: {result.revealed.nonceRange[0]}–{result.revealed.nonceRange[1]}");
    }

    public void ShowError(string message) => SetError(message);

    private void SetLoading(bool isLoading)
    {
        if (_loadingPanel != null) _loadingPanel.SetActive(isLoading);
    }

    private void SetError(string message)
    {
        if (_errorText == null) return;
        bool hasError = !string.IsNullOrEmpty(message);
        _errorText.gameObject.SetActive(hasError);
        if (hasError) _errorText.text = message;
    }

    private void ShowWinFeedback(int totalWin, string winLevel)
    {
        Debug.Log($"[SessionView] Vitória! +{totalWin} coins | level: {winLevel}");
    }

    private static string TruncateHash(string hash)
    {
        if (string.IsNullOrEmpty(hash) || hash.Length <= 16) return hash ?? string.Empty;
        return $"{hash[..6]}...{hash[^6..]}";
    }
}
