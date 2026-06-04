/// <summary>
/// Contrato de UI para a view de sessão. Implementado por qualquer MonoBehaviour que
/// queira receber atualizações do <see cref="SessionPresenter"/>.
/// </summary>
/// <remarks>
/// Usar uma interface permite o presenter ser testado com mocks e desacopla
/// completamente a lógica de negócio da hierarquia de GameObjects do Unity.
/// </remarks>
public interface ISessionView
{
    /// <summary>Exibe ou oculta indicador de carregamento durante requisições à API.</summary>
    void ShowLoading(bool isLoading);

    /// <summary>Atualiza o display de saldo com precisão de centavos.</summary>
    /// <param name="coins">Saldo atual em ponto flutuante.</param>
    void UpdateCoins(float coins);

    /// <summary>Atualiza o contador de free spins restantes.</summary>
    void UpdateFreeSpins(int freeSpinsRemaining);

    /// <summary>Atualiza a exibição do hash do server seed (Provably Fair).</summary>
    void UpdateServerSeedHash(string hash);

    /// <summary>Atualiza a exibição do client seed atual.</summary>
    void UpdateClientSeed(string seed);

    /// <summary>Notifica a view sobre o resultado de um spin concluído.</summary>
    void ShowSpinResult(SpinResponse result);

    /// <summary>Notifica a view sobre a revelação do server seed após rotação.</summary>
    void ShowRotateResult(RotateResponse result);

    /// <summary>Exibe uma mensagem de erro amigável na UI.</summary>
    void ShowError(string message);
}
