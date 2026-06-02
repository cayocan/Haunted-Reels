using SlotEngine;

/// <summary>
/// View concreta do Haunted Reels.
/// Estende GenericSlotMachineView informando como extrair o grid e o total ganho
/// de um SpinResponse do haunted-reels-backend.
/// </summary>
public class HauntedReelsView : GenericSlotMachineView
{
    protected override int[][] GetSpinGrid(ISpinResult result)
    {
        if (result is SpinResponse sr)
            return sr.spin?.grid;
        return null;
    }

    protected override int GetTotalWin(ISpinResult result)
    {
        if (result is SpinResponse sr)
            return sr.spin?.totalWin ?? 0;
        return 0;
    }
}
