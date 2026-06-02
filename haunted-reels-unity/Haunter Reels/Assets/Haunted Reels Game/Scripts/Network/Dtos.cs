using System;
using SlotEngine;

// ─── Compartilhado ────────────────────────────────────────────────────────────

[Serializable]
public class ErrorResponse
{
    public string error;
}

// ─── Session ──────────────────────────────────────────────────────────────────

[Serializable]
public class SessionStateResponse
{
    public string sessionId;
    public string serverSeedHash;
    public int coins;
    public int betPerLine;
    public int nonce;
    public int freeSpinsRemaining;
    public string clientSeed;
}

[Serializable]
public class SetClientSeedRequest
{
    public string clientSeed;
}

[Serializable]
public class SetClientSeedResponse
{
    public bool ok;
    public string error;
}

[Serializable]
public class RotateRevealedData
{
    public string serverSeed;
    public string serverSeedHash;
    public int[] nonceRange;
}

[Serializable]
public class RotateResponse
{
    public bool ok;
    public RotateRevealedData revealed;
    public string newServerSeedHash;
}

// ─── Spin ─────────────────────────────────────────────────────────────────────

[Serializable]
public class SpinRequest
{
    public string sessionId;
    public int? betPerLine;
}

[Serializable]
public class LineWin
{
    public int lineId;
    public int symbolId;
    public int count;
    public int multiplier;
    public int coins;
    public int[][] cells;
}

[Serializable]
public class SpinData
{
    public int[] stopPositions;
    public int[][] grid;
    public LineWin[] lineWins;
    public int lineWinTotal;
    public int scatterCount;
    public int[][] scatterPositions;
    public int scatterCoins;
    public bool triggerFreeSpins;
    public int freeSpinsAwarded;
    public int totalBet;
    public int totalWin;
    public string winLevel;
}

[Serializable]
public class SpinSessionData
{
    public int coins;
    public int freeSpinsRemaining;
    public int nonce;
    public string serverSeedHash;
}

[Serializable]
public class ProvablyFairData
{
    public string serverSeedHash;
    public string clientSeed;
    public int nonce;
}

[Serializable]
public class SpinResponse : ISpinResult
{
    public SpinData spin;
    public SpinSessionData session;
    public ProvablyFairData provablyFair;
}

// ─── Verify ───────────────────────────────────────────────────────────────────

[Serializable]
public class VerifyRequest
{
    public string serverSeed;
    public string clientSeed;
    public int nonce;
    public int[] stops;
}

[Serializable]
public class VerifyResponse
{
    public bool valid;
    public int[] recomputedStops;
}

// ─── Constantes ───────────────────────────────────────────────────────────────

public static class WinLevel
{
    public const string None    = "none";
    public const string Small   = "small";
    public const string Big     = "big";
    public const string Mega    = "mega";
    public const string Jackpot = "jackpot";
}

/// <summary>IDs de símbolo retornados em SpinData.grid e LineWin.symbolId.</summary>
public static class SymbolId
{
    public const int Witch    = 0; // H1
    public const int Pumpkin  = 1; // H2
    public const int Skull    = 2; // H3
    public const int Bat      = 3; // L1
    public const int Spider   = 4; // L2
    public const int Potion   = 5; // L3
    public const int Wild     = 6; // Ghost Wild
    public const int Scatter  = 7; // Cauldron
}

[Serializable]
public class GridRow
{
    public int[] cells;
}
