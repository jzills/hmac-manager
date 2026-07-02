using System.Text.Json;
using System.Text.Json.Serialization;

namespace HmacManager.Operator.Rendering;

/// <summary>
/// Renders a set of resolved policies into the aggregate <c>config.json</c> (public fields) and
/// the per-policy private-key files, with the config array order and key-file indices aligned by
/// construction. The output mirrors what the Helm chart produced in stage 1 and is consumed by
/// the library's configuration pipeline unchanged.
/// </summary>
public sealed class PolicyConfigRenderer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _nonceCacheType;

    /// <param name="nonceCacheType">The cluster-wide nonce cache type ("Memory" or "Distributed").</param>
    public PolicyConfigRenderer(string nonceCacheType) => _nonceCacheType = nonceCacheType;

    public RenderResult Render(IReadOnlyList<ResolvedPolicy> policies)
    {
        // A stable, deterministic order so the config array and the key-file indices line up and
        // so unchanged input produces byte-identical output (avoids needless ConfigMap/Secret churn).
        var ordered = policies.OrderBy(policy => policy.Name, StringComparer.Ordinal).ToList();

        var root = new ConfigRoot
        {
            HmacManager = ordered.Select(policy => new ConfigPolicy
            {
                Name = policy.Name,
                Keys = new ConfigKeys { PublicKey = policy.PublicKey },
                Algorithms = new ConfigAlgorithms
                {
                    ContentHashAlgorithm = policy.ContentHashAlgorithm,
                    SigningHashAlgorithm = policy.SigningHashAlgorithm,
                },
                Nonce = new ConfigNonce
                {
                    CacheType = _nonceCacheType,
                    MaxAgeInSeconds = policy.NonceMaxAgeInSeconds,
                },
                Schemes = policy.Schemes.Count == 0
                    ? null
                    : policy.Schemes.Select(scheme => new ConfigScheme
                    {
                        Name = scheme.Name,
                        Headers = scheme.Headers.Select(header => new ConfigHeader
                        {
                            Name = header.Name,
                            ClaimType = header.ClaimType ?? header.Name,
                        }).ToList(),
                    }).ToList(),
            }).ToList(),
        };

        var configJson = JsonSerializer.Serialize(root, SerializerOptions);

        var keyFiles = new Dictionary<string, string>();
        for (var i = 0; i < ordered.Count; i++)
        {
            keyFiles[$"HmacManager__{i}__Keys__PrivateKey"] = ordered[i].PrivateKey;
        }

        return new RenderResult(configJson, keyFiles);
    }

    // Private DTOs whose property names mirror the JSON the library binds
    // (HmacPolicyConfigurationSection), matching the shape stage 1's Helm helper produced.
    private sealed class ConfigRoot
    {
        public List<ConfigPolicy> HmacManager { get; set; } = new();
    }

    private sealed class ConfigPolicy
    {
        public string Name { get; set; } = string.Empty;
        public ConfigKeys Keys { get; set; } = new();
        public ConfigAlgorithms Algorithms { get; set; } = new();
        public ConfigNonce Nonce { get; set; } = new();
        public List<ConfigScheme>? Schemes { get; set; }
    }

    private sealed class ConfigKeys
    {
        public string PublicKey { get; set; } = string.Empty;
    }

    private sealed class ConfigAlgorithms
    {
        public string ContentHashAlgorithm { get; set; } = string.Empty;
        public string SigningHashAlgorithm { get; set; } = string.Empty;
    }

    private sealed class ConfigNonce
    {
        public string CacheType { get; set; } = string.Empty;
        public int MaxAgeInSeconds { get; set; }
    }

    private sealed class ConfigScheme
    {
        public string Name { get; set; } = string.Empty;
        public List<ConfigHeader> Headers { get; set; } = new();
    }

    private sealed class ConfigHeader
    {
        public string Name { get; set; } = string.Empty;
        public string ClaimType { get; set; } = string.Empty;
    }
}
