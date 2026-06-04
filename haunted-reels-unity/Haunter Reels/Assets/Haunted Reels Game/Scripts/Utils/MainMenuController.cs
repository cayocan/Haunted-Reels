using System;
using UnityEngine;

/// <summary>
/// Controlador da cena de menu. Orquestra a sequência de inicialização:
/// verifica se o backend precisa ser "acordado" e, quando pronto, ativa o menu principal.
/// </summary>
/// <remarks>
/// Fluxo de inicialização:
/// <list type="number">
///   <item>Inicia a música de menu via <see cref="HauntedAudioManager"/>.</item>
///   <item>Se a URL da API for a mesma que a DEV (localhost), pula o wake-up.</item>
///   <item>Caso contrário, inicia o <see cref="BackendWakeUpPresenter"/> que faz ping até responder.</item>
///   <item>Quando o backend responde, ativa o <c>menuRoot</c> com o <see cref="MenuView"/>.</item>
/// </list>
/// </remarks>
public class MainMenuController : MonoBehaviour
{
    [Header("Backend Wake-up")]
    [SerializeField] private BackendWakeUpView    backendWakeUpView;
    [SerializeField] private BackendHealthService backendHealthService;

    [Header("Menu")]
    [Tooltip("Root do menu (onde está a MenuView). Será ativado quando o backend estiver pronto.")]
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private MenuView   menuView;

    private BackendWakeUpPresenter _wakePresenter;

    private void Awake()
    {
        if (backendWakeUpView    == null) Debug.LogWarning("[MainMenuController] backendWakeUpView não atribuído.");
        if (backendHealthService == null) Debug.LogWarning("[MainMenuController] backendHealthService não atribuído.");
        if (menuRoot == null && menuView != null) menuRoot = menuView.gameObject;
    }

    private void Start()
    {
        if (HauntedAudioManager.Instance != null)
            HauntedAudioManager.Instance.Play("Music");

        _wakePresenter = new BackendWakeUpPresenter(backendWakeUpView, backendHealthService, this);

        if (!ShouldRunWakeUp())
        {
            Debug.Log("[MainMenuController] Backend de desenvolvimento — pulando wake-up.");
            if (backendWakeUpView != null) backendWakeUpView.gameObject.SetActive(false);
            OnWakeUpComplete();
            return;
        }

        _wakePresenter.StartWakeUp(OnWakeUpComplete);
    }

    /// <summary>
    /// Retorna <c>true</c> se a URL configurada em <see cref="EnvConfig.ApiUrl"/> aponta para
    /// um servidor remoto (produção/ngrok), indicando que o wake-up é necessário.
    /// Retorna <c>false</c> em desenvolvimento local (localhost), pulando a espera.
    /// </summary>
    private bool ShouldRunWakeUp()
    {
        try
        {
            string dev = EnvConfig.GetOrDefault("DEV_API_URL", "http://localhost:3000");
            string api = EnvConfig.ApiUrl;
            return !string.Equals(api, dev, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[MainMenuController] Falha ao ler EnvConfig: {ex.Message}. Executando wake-up.");
            return true;
        }
    }

    private void OnWakeUpComplete()
    {
        Debug.Log("[MainMenuController] Backend pronto.");
        if (menuRoot != null) menuRoot.SetActive(true);
    }

    private void OnDestroy() => _wakePresenter?.StopWakeUp();

    /// <summary>
    /// Força o pulo do wake-up (útil para botão de debug na UI).
    /// Para o ping loop e ativa o menu imediatamente.
    /// </summary>
    public void SkipWakeUp()
    {
        _wakePresenter?.StopWakeUp();
        if (backendWakeUpView != null) backendWakeUpView.gameObject.SetActive(false);
        OnWakeUpComplete();
    }
}
