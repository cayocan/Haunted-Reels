using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Presenter responsável pelo fluxo de "wake-up" do backend.
/// Exibe a tela de espera enquanto faz ping no endpoint <c>/health</c> até o servidor responder,
/// rotacionando mensagens temáticas e animando os três pontos de carregamento.
/// </summary>
/// <remarks>
/// Não é MonoBehaviour — recebe um <paramref name="coroutineRunner"/> externo para rodar coroutines.
/// Isso o mantém testável e desacoplado do Unity.
/// </remarks>
public class BackendWakeUpPresenter
{
    private readonly BackendWakeUpView    _view;
    private readonly MonoBehaviour        _coroutineRunner;
    public BackendHealthService HealthService { get; }

    private readonly string[] _messages =
    {
        "Waking up the backend",
        "Summoning the spirits of the server",
        "Communicating with the other side",
        "Checking the health of the haunted machine",
        "Aligning the reels with the astral plane",
        "Performing a ritual to awaken the backend",
        "Consulting the ancient logs of the server",
        "Invoking the spirits of uptime",
        "Sending a ping to the netherworld",
        "Channeling the energy of the backend",
    };

    private int      _messageIndex = 0;
    private Coroutine _messageCoroutine;
    private Coroutine _dotsCoroutine;
    private Coroutine _pingCoroutine;
    private bool     _isRunning = false;
    private Action   _onComplete;

    public BackendWakeUpPresenter(BackendWakeUpView view, BackendHealthService healthService, MonoBehaviour coroutineRunner)
    {
        _view            = view            ?? throw new ArgumentNullException(nameof(view));
        HealthService    = healthService   ?? throw new ArgumentNullException(nameof(healthService));
        _coroutineRunner = coroutineRunner ?? throw new ArgumentNullException(nameof(coroutineRunner));
    }

    /// <summary>
    /// Inicia o fluxo de wake-up: exibe a view, começa a rotação de mensagens e o ping loop.
    /// Não faz nada se já estiver em execução.
    /// </summary>
    /// <param name="onComplete">Callback invocado após o backend responder com sucesso.</param>
    public void StartWakeUp(Action onComplete)
    {
        if (_isRunning) return;
        _isRunning = true;
        _onComplete = onComplete;

        _view?.Show();
        StartMessageRotation();

        _pingCoroutine = _coroutineRunner.StartCoroutine(
            HealthService.PingLoop(OnPingSuccess, _ => { }));
    }

    /// <summary>Para imediatamente o ping loop, as animações e esconde a view.</summary>
    public void StopWakeUp()
    {
        if (!_isRunning) return;
        _isRunning = false;

        if (_pingCoroutine != null)
        {
            try { _coroutineRunner.StopCoroutine(_pingCoroutine); } catch { }
            _pingCoroutine = null;
        }

        StopMessageRotation();
        _view?.Hide();
    }

    private void StartMessageRotation()
    {
        if (_messageCoroutine == null)
            _messageCoroutine = _coroutineRunner.StartCoroutine(MessageRotationCoroutine());
        StartDotsAnimation();
    }

    private void StopMessageRotation()
    {
        if (_messageCoroutine != null)
        {
            try { _coroutineRunner.StopCoroutine(_messageCoroutine); } catch { }
            _messageCoroutine = null;
        }
        StopDotsAnimation();
    }

    private void StartDotsAnimation()
    {
        if (_dotsCoroutine == null)
            _dotsCoroutine = _coroutineRunner.StartCoroutine(DotsAnimationCoroutine());
    }

    private void StopDotsAnimation()
    {
        if (_dotsCoroutine != null)
        {
            try { _coroutineRunner.StopCoroutine(_dotsCoroutine); } catch { }
            _dotsCoroutine = null;
            _view?.SetDots("");
        }
    }

    private void OnPingSuccess()
    {
        if (!_isRunning) return;
        _isRunning = false;
        _coroutineRunner.StartCoroutine(SuccessSequenceCoroutine());
    }

    private IEnumerator SuccessSequenceCoroutine()
    {
        StopMessageRotation();
        _view?.SetMessage("Que os reels girem!");
        yield return new WaitForSeconds(0.6f);
        yield return new WaitForSeconds(0.4f);
        _view?.PlayFadeOut(_onComplete);
    }

    private IEnumerator MessageRotationCoroutine()
    {
        while (true)
        {
            _view?.FadeMessageTo(_messages[_messageIndex]);
            _messageIndex = (_messageIndex + 1) % _messages.Length;
            yield return new WaitForSeconds(4f);
        }
    }

    private IEnumerator DotsAnimationCoroutine()
    {
        string[] cycle = { ".", "..", "..." };
        int idx = 0;
        while (true)
        {
            _view?.SetDots(cycle[idx]);
            idx = (idx + 1) % cycle.Length;
            yield return new WaitForSeconds(0.5f);
        }
    }
}
