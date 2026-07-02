using System.Text;
using HmacManager.Operator.Entities;
using HmacManager.Operator.Rendering;
using k8s.Models;
using KubeOps.Abstractions.Rbac;
using KubeOps.Abstractions.Reconciliation;
using KubeOps.Abstractions.Reconciliation.Controller;
using KubeOps.KubernetesClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HmacManager.Operator.Controllers;

/// <summary>
/// Reconciles <see cref="V1HmacPolicy"/> resources into the aggregate ConfigMap + Secret the
/// hmac-manager pods mount and hot-reload. Reconciliation is set-based: any change rebuilds the
/// whole aggregate from all policies in the namespace, so the config array and key-file indices
/// stay consistent by construction and a partial write can never occur.
/// </summary>
[EntityRbac(typeof(V1HmacPolicy), Verbs = RbacVerb.All)]
[EntityRbac(typeof(V1ConfigMap), typeof(V1Secret), Verbs = RbacVerb.Get | RbacVerb.List | RbacVerb.Create | RbacVerb.Update)]
public sealed class HmacPolicyController : IEntityController<V1HmacPolicy>
{
    private readonly IKubernetesClient _client;
    private readonly ILogger<HmacPolicyController> _logger;
    private readonly OperatorOptions _options;
    private readonly PolicyConfigRenderer _renderer;

    public HmacPolicyController(
        IKubernetesClient client,
        ILogger<HmacPolicyController> logger,
        IOptions<OperatorOptions> options)
    {
        _client = client;
        _logger = logger;
        _options = options.Value;
        _renderer = new PolicyConfigRenderer(_options.NonceCacheType);
    }

    public async Task<ReconciliationResult<V1HmacPolicy>> ReconcileAsync(V1HmacPolicy entity, CancellationToken cancellationToken)
    {
        await RebuildAsync(ResolveNamespace(entity), cancellationToken);
        return ReconciliationResult<V1HmacPolicy>.Success(entity);
    }

    public async Task<ReconciliationResult<V1HmacPolicy>> DeletedAsync(V1HmacPolicy entity, CancellationToken cancellationToken)
    {
        // The deleted policy is already absent from the list, so rebuilding drops it from the aggregate.
        await RebuildAsync(ResolveNamespace(entity), cancellationToken);
        return ReconciliationResult<V1HmacPolicy>.Success(entity);
    }

    private string ResolveNamespace(V1HmacPolicy entity) =>
        !string.IsNullOrEmpty(_options.WatchNamespace)
            ? _options.WatchNamespace!
            : entity.Metadata.NamespaceProperty ?? _client.GetCurrentNamespace();

    private async Task RebuildAsync(string @namespace, CancellationToken cancellationToken)
    {
        var policies = await _client.ListAsync<V1HmacPolicy>(@namespace, cancellationToken: cancellationToken);
        var resolved = new List<ResolvedPolicy>();

        foreach (var policy in policies.OrderBy(policy => policy.Metadata.Name, StringComparer.Ordinal))
        {
            var privateKey = await ResolvePrivateKeyAsync(policy, @namespace, cancellationToken);
            var (isValid, message) = PolicyValidation.Validate(policy.Metadata.Name, policy.Spec.PublicKey, privateKey);

            if (isValid)
            {
                resolved.Add(PolicyMapper.ToResolvedPolicy(policy, privateKey!));
            }

            await UpdateStatusIfChangedAsync(policy, isValid ? "Ready" : "Invalid", isValid ? null : message, cancellationToken);
        }

        var rendered = _renderer.Render(resolved);
        await SaveConfigMapAsync(@namespace, rendered.ConfigJson, cancellationToken);
        await SaveSecretAsync(@namespace, rendered.KeyFiles, cancellationToken);

        _logger.LogInformation(
            "Reconciled aggregate config for namespace {Namespace}: {ValidCount}/{TotalCount} policies valid.",
            @namespace, resolved.Count, policies.Count);
    }

    private async Task<string?> ResolvePrivateKeyAsync(V1HmacPolicy policy, string @namespace, CancellationToken cancellationToken)
    {
        var reference = policy.Spec.PrivateKeySecretRef;
        if (reference is null || string.IsNullOrEmpty(reference.Name) || string.IsNullOrEmpty(reference.Key))
        {
            return null;
        }

        var secret = await _client.GetAsync<V1Secret>(reference.Name, @namespace, cancellationToken);
        if (secret?.Data is null || !secret.Data.TryGetValue(reference.Key, out var value))
        {
            return null;
        }

        return Encoding.UTF8.GetString(value);
    }

    private async Task UpdateStatusIfChangedAsync(V1HmacPolicy policy, string phase, string? message, CancellationToken cancellationToken)
    {
        policy.Status ??= new V1HmacPolicy.PolicyStatus();
        if (policy.Status.Phase == phase && policy.Status.Message == message)
        {
            // Avoid a status write that would re-trigger reconciliation for no reason.
            return;
        }

        policy.Status.Phase = phase;
        policy.Status.Message = message;
        policy.Status.ObservedGeneration = policy.Metadata.Generation ?? 0;
        await _client.UpdateStatusAsync(policy, cancellationToken);
    }

    private Task SaveConfigMapAsync(string @namespace, string configJson, CancellationToken cancellationToken) =>
        _client.SaveAsync(new V1ConfigMap
        {
            ApiVersion = "v1",
            Kind = "ConfigMap",
            Metadata = new V1ObjectMeta { Name = _options.ConfigMapName, NamespaceProperty = @namespace },
            Data = new Dictionary<string, string> { ["config.json"] = configJson },
        }, cancellationToken);

    private Task SaveSecretAsync(string @namespace, IReadOnlyDictionary<string, string> keyFiles, CancellationToken cancellationToken) =>
        _client.SaveAsync(new V1Secret
        {
            ApiVersion = "v1",
            Kind = "Secret",
            Metadata = new V1ObjectMeta { Name = _options.SecretName, NamespaceProperty = @namespace },
            Data = keyFiles.ToDictionary(entry => entry.Key, entry => Encoding.UTF8.GetBytes(entry.Value)),
        }, cancellationToken);
}
