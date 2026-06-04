using System.Runtime.InteropServices;

/// <summary>
/// Ponte de comunicação entre o Unity WebGL e o frame JavaScript pai (ex.: Next.js ou GitHub Pages).
/// Em builds WebGL, serializa o estado da sessão em JSON e o envia via
/// <c>window.parent.postMessage()</c> e <c>CustomEvent("haunted-reels-session")</c>.
/// Em Editor e builds standalone, todos os métodos são no-op.
/// </summary>
/// <remarks>
/// A implementação JavaScript está em <c>Assets/Plugins/WebGL/SlotBridge.jslib</c>.
/// O método <c>JS_PostSessionData</c> é importado via <c>[DllImport("__Internal")]</c>,
/// que é o mecanismo padrão do Unity para chamar funções JavaScript em WebGL.
/// </remarks>
public static class SlotBridge
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void JS_PostSessionData(string json);
#endif

    /// <summary>
    /// Serializa o modelo de sessão e o envia ao frame pai via postMessage.
    /// Chamado após cada spin bem-sucedido e após a inicialização da sessão.
    /// </summary>
    /// <param name="model">Modelo de sessão atual; ignorado se null ou sem sessão ativa.</param>
    public static void BroadcastSession(SessionModel model)
    {
        if (model == null || !model.HasSession) return;

#if UNITY_WEBGL && !UNITY_EDITOR
        JS_PostSessionData(BuildJson(model));
#endif
    }

    private static string BuildJson(SessionModel model)
    {
        return string.Concat(
            "{\"type\":\"haunted-reels-session\"",
            ",\"sessionId\":\"",          Esc(model.SessionId),         "\"",
            ",\"serverSeedHash\":\"",     Esc(model.ServerSeedHash),    "\"",
            ",\"clientSeed\":\"",         Esc(model.ClientSeed ?? ""),  "\"",
            ",\"nonce\":",                model.Nonce,
            ",\"coins\":",                model.Coins,
            ",\"freeSpinsRemaining\":",   model.FreeSpinsRemaining,
            "}"
        );
    }

    private static string Esc(string s)
        => s?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";
}
