using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using SlotEngine;

public class SessionPresenter : MonoBehaviour, ISpinProvider
{
    public static SessionPresenter Instance { get; private set; }

    private ISessionView _view;
    private ApiClient    _api;
    private SessionModel _model;

    public SessionModel Model => _model;

    private CancellationTokenSource _cts;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _api   = new ApiClient();
        _model = new SessionModel();
        _cts   = new CancellationTokenSource();

        _model.OnCoinsChanged  += _      => _view?.UpdateCoins(_model.CoinsFloat);
        _model.OnSpinCompleted += result => _view?.ShowSpinResult(result);
        _model.OnSeedRotated   += result => _view?.ShowRotateResult(result);
    }

    private void OnDestroy()
    {
        _cts.Cancel();
        _cts.Dispose();
        if (Instance == this) Instance = null;
    }

    public void SetView(ISessionView view)
    {
        _view = view;
        if (_model.HasSession) RefreshView();
    }

    public void ClearView() => _view = null;

    public async UniTask<bool> InitAsync(CancellationToken ct = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);

        _view?.ShowLoading(true);
        try
        {
            var sessionResponse = await _api.CreateSessionAsync(linked.Token);
            _model.Apply(sessionResponse);

            string seed = _model.GenerateClientSeed();
            var seedResponse = await _api.SetClientSeedAsync(_model.SessionId, seed, linked.Token);
            if (seedResponse.ok)
                _model.ApplyClientSeed(seed);
            else
                Debug.LogWarning($"[SessionPresenter] Client seed rejeitado: {seedResponse.error}");

            RefreshView();
            SlotBridge.BroadcastSession(_model);
            return true;
        }
        catch (ApiException ex)
        {
            HandleError(ex);
            return false;
        }
        finally
        {
            _view?.ShowLoading(false);
        }
    }

    public async UniTask<bool> SpinAsync(int betPerLine = 0, CancellationToken ct = default)
    {
        if (!_model.HasSession)
        {
            Debug.LogWarning("[SessionPresenter] Spin solicitado antes de a sessão ser criada.");
            return false;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);

        _view?.ShowLoading(true);
        try
        {
            var response = await _api.SpinAsync(_model.SessionId, betPerLine, linked.Token);
            _model.ApplySpin(response);
            SlotBridge.BroadcastSession(_model);
            return true;
        }
        catch (ApiException ex)
        {
            HandleError(ex);
            return false;
        }
        finally
        {
            _view?.ShowLoading(false);
        }
    }

    public async UniTask<bool> RequestSetClientSeedAsync(string seed, CancellationToken ct = default)
    {
        if (!_model.HasSession) return false;

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);

        _view?.ShowLoading(true);
        try
        {
            var response = await _api.SetClientSeedAsync(_model.SessionId, seed, linked.Token);
            if (response.ok)
            {
                _model.ApplyClientSeed(seed);
                _view?.UpdateClientSeed(seed);
            }
            else
            {
                _view?.ShowError(response.error ?? "Falha ao definir client seed.");
            }
            return response.ok;
        }
        catch (ApiException ex)
        {
            HandleError(ex);
            return false;
        }
        finally
        {
            _view?.ShowLoading(false);
        }
    }

    public async UniTask<bool> RequestRotateSeedAsync(CancellationToken ct = default)
    {
        if (!_model.HasSession) return false;

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);

        _view?.ShowLoading(true);
        try
        {
            var response = await _api.RotateSeedAsync(_model.SessionId, linked.Token);
            _model.ApplyRotate(response);
            _view?.UpdateServerSeedHash(_model.ServerSeedHash);
            return true;
        }
        catch (ApiException ex)
        {
            HandleError(ex);
            return false;
        }
        finally
        {
            _view?.ShowLoading(false);
        }
    }

    public async UniTask<bool> RequestRefreshSessionAsync(CancellationToken ct = default)
    {
        if (!_model.HasSession) return false;

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);

        _view?.ShowLoading(true);
        try
        {
            var response = await _api.GetSessionAsync(_model.SessionId, linked.Token);
            _model.Apply(response);
            RefreshView();
            return true;
        }
        catch (ApiException ex)
        {
            HandleError(ex);
            return false;
        }
        finally
        {
            _view?.ShowLoading(false);
        }
    }

    private void RefreshView()
    {
        if (_view == null) return;
        _view.UpdateCoins(_model.CoinsFloat);
        _view.UpdateFreeSpins(_model.FreeSpinsRemaining);
        _view.UpdateServerSeedHash(_model.ServerSeedHash);
        _view.UpdateClientSeed(_model.ClientSeed ?? string.Empty);
    }

    private void HandleError(ApiException ex)
    {
        Debug.LogError($"[SessionPresenter] Erro de API (HTTP {ex.StatusCode}): {ex.Message}");

        string friendly = ex.StatusCode switch
        {
            400    => "Dados inválidos enviados ao servidor.",
            401    => "Sessão expirada. Tente reiniciar o jogo.",
            404    => "Sessão não encontrada no servidor.",
            429    => "Muitas requisições. Aguarde um momento.",
            >= 500 => "Erro interno do servidor. Tente novamente.",
            0      => "Falha ao processar resposta do servidor.",
            _      => "Ocorreu um erro inesperado.",
        };

        _view?.ShowError(friendly);
    }
}
