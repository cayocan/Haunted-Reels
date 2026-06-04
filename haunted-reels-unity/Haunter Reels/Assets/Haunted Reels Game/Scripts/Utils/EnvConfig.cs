using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Utilitário estático que carrega variáveis de ambiente do arquivo <c>Resources/app.txt</c>
/// em tempo de execução e as expõe como propriedades tipadas.
/// </summary>
/// <remarks>
/// Formato do arquivo (chave=valor, linhas começando com # são comentários):
/// <code>
/// DEV_API_URL=http://localhost:3000
/// PROD_API_URL=https://meu-ngrok.ngrok-free.app
/// USE_PROD_API_IN_EDITOR=false
/// </code>
/// Lógica de seleção de URL (<see cref="ApiUrl"/>):
/// <list type="bullet">
///   <item>No Editor: usa DEV_API_URL, a menos que USE_PROD_API_IN_EDITOR=true.</item>
///   <item>Em build de desenvolvimento (<c>Debug.isDebugBuild=true</c>): usa DEV_API_URL.</item>
///   <item>Em build de release (<c>Debug.isDebugBuild=false</c>): usa PROD_API_URL.</item>
/// </list>
/// O arquivo é carregado lazy na primeira chamada e cacheado para o restante da sessão.
/// </remarks>
public static class EnvConfig
{
    private const string ResourcePath = "app";
    private const string DevUrlKey = "DEV_API_URL";
    private const string ProdUrlKey = "PROD_API_URL";
    private const string UseProdInEditorKey = "USE_PROD_API_IN_EDITOR";

    private static Dictionary<string, string> _vars;

    public static string ApiUrl
    {
        get
        {
#if UNITY_EDITOR
            if (GetOrDefault(UseProdInEditorKey, "false").ToLowerInvariant() == "true")
                return Get(ProdUrlKey);
#endif
            return Debug.isDebugBuild ? Get(DevUrlKey) : Get(ProdUrlKey);
        }
    }

    public static string Get(string key)
    {
        EnsureLoaded();
        if (_vars.TryGetValue(key, out string value)) return value;
        throw new KeyNotFoundException($"[EnvConfig] Chave '{key}' não encontrada em Resources/{ResourcePath}.env");
    }

    public static string GetOrDefault(string key, string fallback = "")
    {
        EnsureLoaded();
        return _vars.TryGetValue(key, out string value) ? value : fallback;
    }

    private static void EnsureLoaded()
    {
        if (_vars != null) return;

        var asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null)
            throw new InvalidOperationException(
                $"[EnvConfig] Arquivo 'Resources/{ResourcePath}.env' não encontrado.");

        _vars = Parse(asset.text);
    }

    private static Dictionary<string, string> Parse(string raw)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (string rawLine in raw.Split('\n'))
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#", StringComparison.Ordinal)) continue;

            int sep = line.IndexOf('=');
            if (sep <= 0) continue;

            string key = line.Substring(0, sep).Trim();
            string value = line.Substring(sep + 1).Trim();

            if (value.Length >= 2 &&
                ((value[0] == '"' && value[value.Length - 1] == '"') ||
                    (value[0] == '\'' && value[value.Length - 1] == '\'')))
            {
                value = value.Substring(1, value.Length - 2);
            }

            result[key] = value;
        }

        return result;
    }
}
