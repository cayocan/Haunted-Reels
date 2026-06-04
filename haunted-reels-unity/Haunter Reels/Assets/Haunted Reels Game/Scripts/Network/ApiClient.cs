using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Camada de acesso à API REST do backend Haunted Reels.
/// Encapsula todos os endpoints em métodos async/await via UniTask e UnityWebRequest.
/// Lança <see cref="ApiException"/> em qualquer falha de rede ou resposta HTTP de erro.
/// </summary>
/// <remarks>
/// Padrão MVP: este é o único componente que faz chamadas de rede.
/// Recebe a URL base de <see cref="EnvConfig.ApiUrl"/> por padrão, mas aceita URL customizada
/// para testes ou múltiplos ambientes.
/// </remarks>
public class ApiClient
{
    private readonly string _baseUrl;
    private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
    {
        NullValueHandling = NullValueHandling.Ignore,
    };

    /// <summary>Instancia o cliente usando a URL definida em <see cref="EnvConfig.ApiUrl"/>.</summary>
    public ApiClient() : this(EnvConfig.ApiUrl) { }

    /// <summary>Instancia o cliente com uma URL base explícita.</summary>
    /// <param name="baseUrl">URL base do backend, sem barra final.</param>
    public ApiClient(string baseUrl)
    {
        _baseUrl = baseUrl.TrimEnd('/');
    }

    /// <summary>
    /// Cria uma nova sessão no backend (<c>POST /session</c>).
    /// </summary>
    /// <param name="ct">Token de cancelamento opcional.</param>
    /// <returns>Estado inicial da sessão, incluindo sessionId e serverSeedHash.</returns>
    public async UniTask<SessionStateResponse> CreateSessionAsync(CancellationToken ct = default)
    {
        return await PostAsync<SessionStateResponse>("/session", null, expectedCode: 201, ct: ct);
    }

    /// <summary>Recupera o estado atual de uma sessão existente (<c>GET /session/:id</c>).</summary>
    /// <param name="sessionId">ID da sessão a ser consultada.</param>
    /// <param name="ct">Token de cancelamento opcional.</param>
    public async UniTask<SessionStateResponse> GetSessionAsync(string sessionId, CancellationToken ct = default)
    {
        ValidateId(sessionId);
        return await GetAsync<SessionStateResponse>($"/session/{sessionId}", ct);
    }

    /// <summary>
    /// Define o client seed da sessão no backend (<c>POST /session/:id/seed</c>).
    /// Deve ser chamado antes do primeiro spin para que o Provably Fair seja válido.
    /// </summary>
    /// <param name="sessionId">ID da sessão.</param>
    /// <param name="clientSeed">Seed fornecido pelo jogador ou gerado localmente.</param>
    /// <param name="ct">Token de cancelamento opcional.</param>
    public async UniTask<SetClientSeedResponse> SetClientSeedAsync(
        string sessionId, string clientSeed, CancellationToken ct = default)
    {
        ValidateId(sessionId);
        var body = new SetClientSeedRequest { clientSeed = clientSeed };
        return await PostAsync<SetClientSeedResponse>($"/session/{sessionId}/seed", body, ct: ct);
    }

    /// <summary>
    /// Rotaciona o server seed (<c>POST /session/:id/rotate</c>).
    /// O seed anterior é revelado na resposta, permitindo verificação dos spins passados.
    /// </summary>
    /// <param name="sessionId">ID da sessão.</param>
    /// <param name="ct">Token de cancelamento opcional.</param>
    public async UniTask<RotateResponse> RotateSeedAsync(string sessionId, CancellationToken ct = default)
    {
        ValidateId(sessionId);
        return await PostAsync<RotateResponse>($"/session/{sessionId}/rotate", null, ct: ct);
    }

    /// <summary>
    /// Executa um spin (<c>POST /spin</c>).
    /// O backend calcula o resultado usando HMAC-SHA256(serverSeed, clientSeed + nonce) e retorna
    /// o grid completo, lineWins, scatter, free spins concedidos e dados Provably Fair.
    /// </summary>
    /// <param name="sessionId">ID da sessão ativa.</param>
    /// <param name="betPerLine">Aposta por linha; use 0 para reutilizar a última aposta registrada.</param>
    /// <param name="ct">Token de cancelamento opcional.</param>
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

    /// <summary>
    /// Verifica a autenticidade de um spin passado (<c>POST /verify</c>).
    /// Recalcula os stops com os mesmos parâmetros e compara com os stops fornecidos.
    /// </summary>
    /// <param name="serverSeed">Server seed revelado após rotação.</param>
    /// <param name="clientSeed">Client seed usado no spin.</param>
    /// <param name="nonce">Nonce (número sequencial) do spin a verificar.</param>
    /// <param name="stops">Stop positions que devem ser recomputados pelo servidor.</param>
    /// <param name="ct">Token de cancelamento opcional.</param>
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

    /// <summary>
    /// Define os headers comuns a todos os requests.
    /// O header <c>ngrok-skip-browser-warning</c> bypassa a página de aviso do ngrok
    /// que bloqueia requests CORS quando o túnel é acessado por browsers externos.
    /// </summary>
    private static void SetCommonHeaders(UnityWebRequest request)
    {
        request.SetRequestHeader("Accept", "application/json");
        request.SetRequestHeader("ngrok-skip-browser-warning", "true");
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
