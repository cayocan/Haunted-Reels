using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuView : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button   _playButton;
    [SerializeField] private TMP_Text _errorText;

    [Header("Navegação")]
    [Tooltip("Nome exato da cena do jogo registrada em Build Settings.")]
    [SerializeField] private string _gameSceneName = "GameScene";

    private CancellationTokenSource _cts;

    private void Awake()
    {
        _cts = new CancellationTokenSource();
        SetError(null);
    }

    private void Start()
    {
        _playButton.onClick.AddListener(OnPlayClicked);
    }

    private void OnDestroy()
    {
        _cts.Cancel();
        _cts.Dispose();
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
