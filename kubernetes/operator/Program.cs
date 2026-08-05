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

Banner.Print();

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

// Not ILogger<Program>: top-level statements compile Program into the global namespace, so that
// category would be the bare string "Program" — outside the "HmacManager" prefix every log-level
// filter in this codebase (and appsettings.json's Default: None) is written against, which would
// silence this message along with everything we don't own.
var startupLogger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("HmacManager.Operator");

OperatorLog.OperatorStarting(
    startupLogger,
    string.IsNullOrWhiteSpace(operatorOptions.WatchNamespace) ? "(the resource's own)" : operatorOptions.WatchNamespace!,
    operatorOptions.ConfigMapName,
    operatorOptions.SecretName,
    operatorOptions.NonceCacheType);

host.Run();
