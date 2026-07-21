using HmacManager.Operator;
using HmacManager.Operator.Controllers;
using HmacManager.Operator.Diagnostics;
using HmacManager.Operator.Entities;
using KubeOps.Abstractions.Builder;
using KubeOps.Operator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

var operatorSection = builder.Configuration.GetSection(OperatorOptions.SectionName);
builder.Services.Configure<OperatorOptions>(operatorSection);
var operatorOptions = operatorSection.Get<OperatorOptions>() ?? new OperatorOptions();

builder.Services
    .AddKubernetesOperator(settings =>
    {
        // Scope the watch to a single namespace when configured so the namespaced RBAC the chart
        // grants is sufficient. Left unset (null), KubeOps watches every namespace, which would
        // require a cluster-scoped ClusterRole to watch the custom resources.
        if (!string.IsNullOrWhiteSpace(operatorOptions.WatchNamespace))
        {
            settings.WithNamespace(operatorOptions.WatchNamespace);
        }
    })
    .AddController<HmacPolicyController, V1HmacPolicy>();

var host = builder.Build();

OperatorLog.OperatorStarting(
    host.Services.GetRequiredService<ILogger<Program>>(),
    string.IsNullOrWhiteSpace(operatorOptions.WatchNamespace) ? "(the resource's own)" : operatorOptions.WatchNamespace!,
    operatorOptions.ConfigMapName,
    operatorOptions.SecretName,
    operatorOptions.NonceCacheType);

host.Run();
