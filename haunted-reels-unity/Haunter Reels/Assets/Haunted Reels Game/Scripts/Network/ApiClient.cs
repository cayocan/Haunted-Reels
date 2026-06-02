using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

public class ApiClient
{
    private readonly string _baseUrl;
    private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
    {
        NullValueHandling = NullValueHandling.Ignore,
    };

    public ApiClient() : this(EnvConfig.ApiUrl) { }

    public ApiClient(string baseUrl)
    {
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public async UniTask<SessionStateResponse> CreateSessionAsync(CancellationToken ct = default)
    {
        return await PostAsync<SessionStateResponse>("/session", null, expectedCode: 201, ct: ct);
    }

    public async UniTask<SessionStateResponse> GetSessionAsync(string sessionId, CancellationToken ct = default)
    {
        ValidateId(sessionId);
        return await GetAsync<SessionStateResponse>($"/session/{sessionId}", ct);
    }

    public async UniTask<SetClientSeedResponse> SetClientSeedAsync(
        string sessionId, string clientSeed, CancellationToken ct = default)
    {
        ValidateId(sessionId);
        var body = new SetClientSeedRequest { clientSeed = clientSeed };
        return await PostAsync<SetClientSeedResponse>($"/session/{sessionId}/seed", body, ct: ct);
    }

    public async UniTask<RotateResponse> RotateSeedAsync(string sessionId, CancellationToken ct = default)
    {
        ValidateId(sessionId);
        return await PostAsync<RotateResponse>($"/session/{sessionId}/rotate", null, ct: ct);
    }

    public async UniTask<SpinResponse> SpinAsync(
        string sessionId, int betPerLine = 0, CancellationToken ct = default)
    {
        var body = new SpinRequest
        {
            sessionId  = sessionId,
            betPerLine = betPerLine > 0 ? betPerLine : (int?)null,
        };
        return await PostAsync<SpinResponse>("/spin", body, ct: ct);
    }

    public async UniTask<VerifyResponse> VerifySpinAsync(
        string serverSeed, string clientSeed, int nonce, int[] stops, CancellationToken ct = default)
    {
        var body = new VerifyRequest
        {
            serverSeed = serverSeed,
            clientSeed = clientSeed,
            nonce      = nonce,
            stops      = stops,
        };
        return await PostAsync<VerifyResponse>("/verify", body, ct: ct);
    }

    private async UniTask<T> GetAsync<T>(string path, CancellationToken ct)
    {
        string url = _baseUrl + path;
        using var request = UnityWebRequest.Get(url);
        SetCommonHeaders(request);

        await request.SendWebRequest().WithCancellation(ct);

        ThrowIfError(request, url);
        return Deserialize<T>(request.downloadHandler.text);
    }

    private async UniTask<T> PostAsync<T>(
        string path, object body, int expectedCode = 200, CancellationToken ct = default)
    {
        string url     = _baseUrl + path;
        string json    = body != null ? JsonConvert.SerializeObject(body, _jsonSettings) : "{}";
        byte[] rawBody = Encoding.UTF8.GetBytes(json);

        using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
        {
            uploadHandler   = new UploadHandlerRaw(rawBody),
            downloadHandler = new DownloadHandlerBuffer(),
        };
        SetCommonHeaders(request);
        request.SetRequestHeader("Content-Type", "application/json");

        await request.SendWebRequest().WithCancellation(ct);

        ThrowIfError(request, url);
        return Deserialize<T>(request.downloadHandler.text);
    }

    private static void SetCommonHeaders(UnityWebRequest request)
    {
        request.SetRequestHeader("Accept", "application/json");
    }

    private static void ThrowIfError(UnityWebRequest request, string url)
    {
        if (request.result == UnityWebRequest.Result.Success) return;

        string body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;

        string apiError = string.Empty;
        if (!string.IsNullOrEmpty(body))
        {
            try
            {
                var err = JsonConvert.DeserializeObject<ErrorResponse>(body);
                if (err?.error != null) apiError = err.error;
            }
            catch { }
        }

        string message = string.IsNullOrEmpty(apiError)
            ? $"[ApiClient] {request.result} — {url} ({request.responseCode}): {body}"
            : $"[ApiClient] {url} ({request.responseCode}): {apiError}";

        Debug.LogError(message);
        throw new ApiException(message, (int)request.responseCode, apiError);
    }

    private static T Deserialize<T>(string json)
    {
        try
        {
            return JsonConvert.DeserializeObject<T>(json);
        }
        catch (Exception ex)
        {
            throw new ApiException($"[ApiClient] Falha ao desserializar resposta: {ex.Message}", 0, json);
        }
    }

    private static void ValidateId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("sessionId não pode ser vazio.", nameof(id));
    }
}

public class ApiException : Exception
{
    public int StatusCode { get; }
    public string ApiError { get; }

    public ApiException(string message, int statusCode, string apiError)
        : base(message)
    {
        StatusCode = statusCode;
        ApiError   = apiError;
    }
}
