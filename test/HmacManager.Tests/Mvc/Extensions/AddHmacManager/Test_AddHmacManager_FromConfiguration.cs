using HmacManager.Components;
using HmacManager.Mvc.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Unit.Tests;

[TestFixture]
public class Test_AddHmacManager_FromConfiguration
{
    [Test]
    public void Test()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddJsonFile("appsettings.test.json");
        builder.Services.AddHmacManager(builder.Configuration.GetSection("HmacManager"));

        var serviceProvider = builder.Services.BuildServiceProvider();
        var hmacManagerFactory = serviceProvider.GetService<IHmacManagerFactory>();
        
        // TODO: Assert
    }
}