using UnityEngine;
using SlotEngine;

public class SlotMachinePresenter : MonoBehaviour
{
    [SerializeField] private HauntedReelsView  _view;
    [SerializeField] private SlotMachineConfig _config;

    private SlotStateMachine _stateMachine;
    private SlotGameContext  _ctx;
    private SessionModel     _model;

    private void Start()
    {
        var sp = SessionPresenter.Instance;
        if (sp == null)
        {
            Debug.LogError("[SlotMachinePresenter] SessionPresenter.Instance é null.");
            return;
        }

        if (_config == null)
        {
            Debug.LogError("[SlotMachinePresenter] SlotMachineConfig não atribuído.");
            return;
        }

        _model = sp.Model;
        var model = _model;

        _stateMachine = new SlotStateMachine();
        // paylineCount=1: BetPerLine no contexto representa a aposta total visível ao jogador
        // o backend recebe esse valor e divide por nº de paylines internamente
        _ctx = new SlotGameContext(
            _view, sp, model, _stateMachine,
            _config.minBet, _config.maxBet, 1, _config.minSpinDuration);

        _ctx.BetPerLine = Mathf.Clamp(model.BetPerLine, _config.minBet, _config.maxBet);

        _view.OnSpinRequested   += OnSpinRequested;
        _view.OnAutoSpinToggled += OnAutoSpinToggled;
        _view.OnBetSetRequested += OnBetSet;
        model.OnCoinsChanged    += OnCoinsChanged;

        _view.UpdateCoinsFloat(_model.CoinsFloat);
        _view.UpdateFreeSpins(model.FreeSpinsRemaining);
        _view.UpdateBetPerLine(_ctx.BetPerLine, _config.minBet, _config.maxBet, _ctx.TotalBet);

        _stateMachine.Transition(new IdleState(_ctx));
    }

    private void OnDestroy()
    {
        if (_view != null)
        {
            _view.OnSpinRequested   -= OnSpinRequested;
            _view.OnAutoSpinToggled -= OnAutoSpinToggled;
            _view.OnBetSetRequested -= OnBetSet;
        }

        var model = SessionPresenter.Instance != null ? SessionPresenter.Instance.Model : null;
        if (model != null) model.OnCoinsChanged -= OnCoinsChanged;
    }

    private void OnSpinRequested()
    {
        if (_stateMachine.IsIn<IdleState>())
            _stateMachine.Transition(new SpinningState(_ctx));
    }

    private void OnAutoSpinToggled()
    {
        _ctx.IsAutoSpinActive = !_ctx.IsAutoSpinActive;
        _view.SetAutoSpinActive(_ctx.IsAutoSpinActive);

        if (_ctx.IsAutoSpinActive && _stateMachine.IsIn<IdleState>() && _ctx.CanSpin)
            _stateMachine.Transition(new SpinningState(_ctx));
    }

    private void OnBetSet(int newBet)
    {
        // só altera aposta quando idle
        if (!_stateMachine.IsIn<IdleState>()) return;
        _ctx.BetPerLine = newBet; // já vem clampado da view
        _view.UpdateBetPerLine(_ctx.BetPerLine, _config.minBet, _config.maxBet, _ctx.TotalBet);
        _view.SetSpinInteractable(_ctx.CanSpin);
    }

    private void OnCoinsChanged(int coins)
    {
        if (_stateMachine.IsIn<IdleState>())
        {
            _view.UpdateCoinsFloat(_model.CoinsFloat);
            _view.SetSpinInteractable(_ctx.CanSpin);
        }
    }
}
