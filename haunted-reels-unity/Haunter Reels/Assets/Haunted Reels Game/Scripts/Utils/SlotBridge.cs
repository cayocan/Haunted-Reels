using System.Runtime.InteropServices;

// Em builds WebGL, envia o estado da sessão ao frame pai (Next.js)
// via window.parent.postMessage(). Silencioso em Editor e builds standalone.

public static class SlotBridge
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void JS_PostSessionData(string json);
#endif

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
