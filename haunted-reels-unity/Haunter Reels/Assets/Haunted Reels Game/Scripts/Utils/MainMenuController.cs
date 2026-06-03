using System;
using UnityEngine;

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

    public void SkipWakeUp()
    {
        _wakePresenter?.StopWakeUp();
        if (backendWakeUpView != null) backendWakeUpView.gameObject.SetActive(false);
        OnWakeUpComplete();
    }
}
