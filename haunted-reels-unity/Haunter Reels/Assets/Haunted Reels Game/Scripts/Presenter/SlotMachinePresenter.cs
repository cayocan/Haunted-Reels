using UnityEngine;
using SlotEngine;

public class SlotMachinePresenter : MonoBehaviour
{
    [SerializeField] private HauntedReelsView  _view;
    [SerializeField] private SlotMachineConfig _config;

    private SlotStateMachine _stateMachine;
    private SlotGameContext  _ctx;

    private void Start()
    {
        var sp = SessionPresenter.Instance;
        if (sp == null)
        {
            Debug.LogError("[SlotMachinePresenter] SessionPresenter.Instance é null. " +
                "Certifique-se de que a MenuScene foi carregada antes da GameScene.");
            return;
        }

        if (_config == null)
        {
            Debug.LogError("[SlotMachinePresenter] SlotMachineConfig não atribuído no Inspector.");
            return;
        }

        var model = sp.Model;

        _stateMachine = new SlotStateMachine();
        _ctx = new SlotGameContext(
            _view, sp, model, _stateMachine,
            _config.minBet, _config.maxBet, _config.paylineCount, _config.minSpinDuration);

        _ctx.BetPerLine = Mathf.Clamp(model.BetPerLine, _config.minBet, _config.maxBet);

        _view.OnSpinRequested        += OnSpinRequested;
        _view.OnAutoSpinToggled      += OnAutoSpinToggled;
        _view.OnBetIncreaseRequested += OnBetIncrease;
        _view.OnBetDecreaseRequested += OnBetDecrease;

        model.OnCoinsChanged += OnCoinsChanged;

        _view.UpdateCoins(_ctx.Model.Coins);
        _view.UpdateFreeSpins(model.FreeSpinsRemaining);
        _view.UpdateBetPerLine(_ctx.BetPerLine, _config.minBet, _config.maxBet, _ctx.TotalBet);

        _stateMachine.Transition(new IdleState(_ctx));
    }

    private void OnDestroy()
    {
        if (_view != null)
        {
            _view.OnSpinRequested        -= OnSpinRequested;
            _view.OnAutoSpinToggled      -= OnAutoSpinToggled;
            _view.OnBetIncreaseRequested -= OnBetIncrease;
            _view.OnBetDecreaseRequested -= OnBetDecrease;
        }

        var model = SessionPresenter.Instance?.Model;
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

    private void OnBetIncrease()
    {
        if (!_stateMachine.IsIn<IdleState>()) return;
        _ctx.BetPerLine = Mathf.Min(_ctx.BetPerLine + 1, _config.maxBet);
        _view.UpdateBetPerLine(_ctx.BetPerLine, _config.minBet, _config.maxBet, _ctx.TotalBet);
        _view.SetSpinInteractable(_ctx.CanSpin);
    }

    private void OnBetDecrease()
    {
        if (!_stateMachine.IsIn<IdleState>()) return;
        _ctx.BetPerLine = Mathf.Max(_ctx.BetPerLine - 1, _config.minBet);
        _view.UpdateBetPerLine(_ctx.BetPerLine, _config.minBet, _config.maxBet, _ctx.TotalBet);
        _view.SetSpinInteractable(_ctx.CanSpin);
    }

    private void OnCoinsChanged(int coins)
    {
        if (_stateMachine.IsIn<IdleState>())
        {
            _view.UpdateCoins(coins);
            _view.SetSpinInteractable(_ctx.CanSpin);
        }
    }
}
