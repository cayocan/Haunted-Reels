using System;
using System.Security.Cryptography;
using SlotEngine;

/// <summary>
/// Modelo de domínio da sessão de jogo.
/// Concentra todo o estado mutável da partida — saldo, seeds, nonce, free spins e o resultado
/// do último spin — e notifica observadores via eventos sempre que o estado muda.
/// Implementa <see cref="IGameModel"/> para integração com o SlotEngine.
/// </summary>
/// <remarks>
/// Padrão MVP: este é o M (Model). Não possui nenhuma referência a Unity ou MonoBehaviour;
/// é instanciado e mantido vivo pelo <see cref="SessionPresenter"/>.
/// </remarks>
public class SessionModel : IGameModel
{
    /// <summary>Identificador único da sessão no backend.</summary>
    public string SessionId          { get; private set; }

    /// <summary>Hash SHA-256 do server seed comprometido antes do início da sessão (Provably Fair).</summary>
    public string ServerSeedHash     { get; private set; }

    /// <summary>Client seed atual, definido pelo jogador ou gerado aleatoriamente.</summary>
    public string ClientSeed         { get; private set; }

    /// <summary>Aposta por linha na unidade de moeda do jogo.</summary>
    public int    BetPerLine         { get; private set; }

    /// <summary>Contador de spins da sessão, usado como nonce no cálculo Provably Fair.</summary>
    public int    Nonce              { get; private set; }

    /// <summary>Quantidade de Free Spins ainda disponíveis nesta rodada de bônus.</summary>
    public int    FreeSpinsRemaining { get; private set; }

    // saldo completo em float — IGameModel.Coins expõe int truncado para o engine
    private float _coinsFloat;

    /// <summary>Saldo atual com precisão de centavos (2 casas decimais).</summary>
    public float CoinsFloat => _coinsFloat;

    /// <summary>Saldo truncado para inteiro; exposto pelo contrato <see cref="IGameModel"/>.</summary>
    public int   Coins      => (int)_coinsFloat; // IGameModel

    /// <summary>Resposta completa do último spin, incluindo grid, lineWins e dados Provably Fair.</summary>
    public SpinResponse LastSpin    { get; private set; }
    ISpinResult IGameModel.LastSpin => LastSpin;

    /// <summary>Dados revelados do server seed após uma rotação (verificação Provably Fair).</summary>
    public RotateRevealedData LastRevealedSeed { get; private set; }

    /// <summary>Indica se a sessão foi inicializada com um ID válido.</summary>
    public bool HasSession    => !string.IsNullOrEmpty(SessionId);

    /// <summary>Indica se o client seed foi definido.</summary>
    public bool HasClientSeed => !string.IsNullOrEmpty(ClientSeed);

    /// <summary>Disparado sempre que qualquer campo do modelo muda.</summary>
    public event Action OnChanged;

    /// <summary>Disparado quando o saldo muda; entrega o valor inteiro truncado (contrato IGameModel).</summary>
    public event Action<int> OnCoinsChanged; // int por contrato IGameModel

    /// <summary>Disparado após ApplySpin — entrega a resposta completa do spin.</summary>
    public event Action<SpinResponse> OnSpinCompleted;

    /// <summary>Disparado após ApplyRotate — entrega a resposta de rotação com o seed revelado.</summary>
    public event Action<RotateResponse> OnSeedRotated;

    /// <summary>
    /// Aplica o estado completo retornado pelo backend ao criar ou recuperar uma sessão.
    /// </summary>
    /// <param name="r">Resposta de estado de sessão do endpoint <c>POST /session</c> ou <c>GET /session/:id</c>.</param>
    public void Apply(SessionStateResponse r)
    {
        if (r == null) throw new ArgumentNullException(nameof(r));

        SessionId          = r.sessionId;
        ServerSeedHash     = r.serverSeedHash;
        ClientSeed         = r.clientSeed;
        _coinsFloat        = (float)System.Math.Round(r.coins, 2);
        BetPerLine         = r.betPerLine;
        Nonce              = r.nonce;
        FreeSpinsRemaining = r.freeSpinsRemaining;

        OnChanged?.Invoke();
    }

    /// <summary>
    /// Aplica o resultado de um spin, atualizando saldo, nonce, free spins e o último spin.
    /// Dispara <see cref="OnCoinsChanged"/> apenas se o saldo efetivamente mudou.
    /// </summary>
    /// <param name="r">Resposta completa do endpoint <c>POST /spin</c>.</param>
    public void ApplySpin(SpinResponse r)
    {
        if (r == null) throw new ArgumentNullException(nameof(r));

        LastSpin = r;

        if (r.session != null)
        {
            float previous = _coinsFloat;

            _coinsFloat        = (float)System.Math.Round(r.session.coins, 2);
            Nonce              = r.session.nonce;
            FreeSpinsRemaining = r.session.freeSpinsRemaining;
            ServerSeedHash     = r.session.serverSeedHash;

            if (_coinsFloat != previous)
                OnCoinsChanged?.Invoke(Coins);
        }

        OnSpinCompleted?.Invoke(r);
        OnChanged?.Invoke();
    }

    /// <summary>Atualiza o client seed após confirmação do backend.</summary>
    /// <param name="seed">Seed aceito pelo servidor.</param>
    public void ApplyClientSeed(string seed)
    {
        ClientSeed = seed;
        OnChanged?.Invoke();
    }

    /// <summary>
    /// Aplica a rotação de seed: revela o server seed anterior e registra o novo hash.
    /// Permite ao jogador verificar todos os spins anteriores via <c>POST /verify</c>.
    /// </summary>
    /// <param name="r">Resposta do endpoint <c>POST /session/:id/rotate</c>.</param>
    public void ApplyRotate(RotateResponse r)
    {
        if (r == null) throw new ArgumentNullException(nameof(r));

        LastRevealedSeed = r.revealed;
        ServerSeedHash   = r.newServerSeedHash;

        OnSeedRotated?.Invoke(r);
        OnChanged?.Invoke();
    }

    /// <summary>
    /// Gera um client seed criptograficamente aleatório (16 bytes via <see cref="RandomNumberGenerator"/>)
    /// e o armazena localmente. Deve ser enviado ao backend via <c>SetClientSeedAsync</c> antes do primeiro spin.
    /// </summary>
    /// <returns>Seed gerado em formato hexadecimal lowercase.</returns>
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

    /// <summary>Zera todos os campos do modelo e dispara <see cref="OnChanged"/>. Usado ao reiniciar o jogo.</summary>
    public void Reset()
    {
        SessionId          = null;
        ServerSeedHash     = null;
        ClientSeed         = null;
        _coinsFloat        = 0f;
        BetPerLine         = 0;
        Nonce              = 0;
        FreeSpinsRemaining = 0;
        LastSpin           = null;
        LastRevealedSeed   = null;

        OnChanged?.Invoke();
    }
}
