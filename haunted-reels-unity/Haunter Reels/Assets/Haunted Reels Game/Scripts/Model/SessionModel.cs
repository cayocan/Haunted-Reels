using System;
using System.Security.Cryptography;
using SlotEngine;

public class SessionModel : IGameModel
{
    public string SessionId          { get; private set; }
    public string ServerSeedHash     { get; private set; }
    public string ClientSeed         { get; private set; }
    public int    Coins              { get; private set; }
    public int    BetPerLine         { get; private set; }
    public int    Nonce              { get; private set; }
    public int    FreeSpinsRemaining { get; private set; }

    public SpinResponse LastSpin     { get; private set; }
    ISpinResult IGameModel.LastSpin  => LastSpin;

    public RotateRevealedData LastRevealedSeed { get; private set; }

    public bool HasSession    => !string.IsNullOrEmpty(SessionId);
    public bool HasClientSeed => !string.IsNullOrEmpty(ClientSeed);

    public event Action OnChanged;
    public event Action<int> OnCoinsChanged;
    public event Action<SpinResponse> OnSpinCompleted;
    public event Action<RotateResponse> OnSeedRotated;

    public void Apply(SessionStateResponse r)
    {
        if (r == null) throw new ArgumentNullException(nameof(r));

        SessionId          = r.sessionId;
        ServerSeedHash     = r.serverSeedHash;
        ClientSeed         = r.clientSeed;
        Coins              = r.coins;
        BetPerLine         = r.betPerLine;
        Nonce              = r.nonce;
        FreeSpinsRemaining = r.freeSpinsRemaining;

        OnChanged?.Invoke();
    }

    public void ApplySpin(SpinResponse r)
    {
        if (r == null) throw new ArgumentNullException(nameof(r));

        LastSpin = r;

        if (r.session != null)
        {
            int previousCoins = Coins;

            Coins              = r.session.coins;
            Nonce              = r.session.nonce;
            FreeSpinsRemaining = r.session.freeSpinsRemaining;
            ServerSeedHash     = r.session.serverSeedHash;

            if (Coins != previousCoins)
                OnCoinsChanged?.Invoke(Coins);
        }

        OnSpinCompleted?.Invoke(r);
        OnChanged?.Invoke();
    }

    public void ApplyClientSeed(string seed)
    {
        ClientSeed = seed;
        OnChanged?.Invoke();
    }

    public void ApplyRotate(RotateResponse r)
    {
        if (r == null) throw new ArgumentNullException(nameof(r));

        LastRevealedSeed = r.revealed;
        ServerSeedHash   = r.newServerSeedHash;

        OnSeedRotated?.Invoke(r);
        OnChanged?.Invoke();
    }

    public string GenerateClientSeed()
    {
        byte[] bytes = new byte[16];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);

        ClientSeed = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        OnChanged?.Invoke();
        return ClientSeed;
    }

    public string GetClientSeed() => ClientSeed;

    public void Reset()
    {
        SessionId          = null;
        ServerSeedHash     = null;
        ClientSeed         = null;
        Coins              = 0;
        BetPerLine         = 0;
        Nonce              = 0;
        FreeSpinsRemaining = 0;
        LastSpin           = null;
        LastRevealedSeed   = null;

        OnChanged?.Invoke();
    }
}
