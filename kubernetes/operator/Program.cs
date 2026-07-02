using HmacManager.Operator;
using HmacManager.Operator.Controllers;
using HmacManager.Operator.Entities;
using KubeOps.Operator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<OperatorOptions>(builder.Configuration.GetSection(OperatorOptions.SectionName));

builder.Services
    .AddKubernetesOperator()
    .AddController<HmacPolicyController, V1HmacPolicy>();

builder.Build().Run();
