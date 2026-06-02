using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class BackendHealthService : MonoBehaviour
{
    // Lido de EnvConfig em runtime — aponta para o backend de produção
    private static string HealthUrl => EnvConfig.GetOrDefault("PROD_API_URL", "http://localhost:3000") + "/health";

    public IEnumerator PingLoop(Action onSuccess, Action<int> onAttempt)
    {
        int attempt = 0;
        while (true)
        {
            attempt++;
            onAttempt?.Invoke(attempt);

            using var ping = UnityWebRequest.Get(HealthUrl);
            ping.timeout = 10;
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
