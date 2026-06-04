using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Serviço que verifica periodicamente a disponibilidade do backend via <c>GET /health</c>.
/// Usado pelo <see cref="BackendWakeUpPresenter"/> para detectar quando o servidor (ngrok/produção)
/// está pronto para aceitar requisições após cold start.
/// </summary>
public class BackendHealthService : MonoBehaviour
{
    // Lido de EnvConfig em runtime — aponta para o backend de produção
    private static string HealthUrl => EnvConfig.GetOrDefault("PROD_API_URL", "http://localhost:3000") + "/health";

    /// <summary>
    /// Coroutine que faz ping em <c>/health</c> a cada 3 segundos até receber HTTP 200.
    /// Deve ser iniciada por um MonoBehaviour externo (ex.: <see cref="BackendWakeUpPresenter"/>).
    /// </summary>
    /// <param name="onSuccess">Callback invocado na primeira resposta bem-sucedida; a coroutine encerra.</param>
    /// <param name="onAttempt">Callback invocado a cada tentativa com o número da tentativa atual.</param>
    public IEnumerator PingLoop(Action onSuccess, Action<int> onAttempt)
    {
        int attempt = 0;
        while (true)
        {
            attempt++;
            onAttempt?.Invoke(attempt);

            using var ping = UnityWebRequest.Get(HealthUrl);
            ping.timeout = 10;
            ping.SetRequestHeader("ngrok-skip-browser-warning", "true");
            yield return ping.SendWebRequest();

            if (ping.result == UnityWebRequest.Result.Success)
            {
                onSuccess?.Invoke();
                yield break;
            }

            yield return new WaitForSeconds(3f);
        }
    }
}
