public interface ISessionView
{
    void ShowLoading(bool isLoading);
    void UpdateCoins(float coins);
    void UpdateFreeSpins(int freeSpinsRemaining);
    void UpdateServerSeedHash(string hash);
    void UpdateClientSeed(string seed);
    void ShowSpinResult(SpinResponse result);
    void ShowRotateResult(RotateResponse result);
    void ShowError(string message);
}
