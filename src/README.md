
# HmacManager

[![NuGet Version](https://img.shields.io/nuget/v/HmacManager.svg)](https://www.nuget.org/packages/HmacManager/) [![NuGet Downloads](https://img.shields.io/nuget/dt/HmacManager.svg)](https://www.nuget.org/packages/HmacManager/) [![.NET](https://github.com/jzills/hmac-manager/actions/workflows/pr.yml/badge.svg)](https://github.com/jzills/hmac-manager/actions/workflows/pr.yml)

- [Quickstart](#quickstart)
    * [Register without built-in authentication flow](#register-without-built-in-authentication-flow)
    * [Register with built-in authentication flow](#register-with-built-in-authentication-flow)
    * [Register with an `IConfigurationSection`](#register-with-an-iconfigurationsection)
    * [Register HttpClient with `HmacHttpMessageHandler`](#register-httpclient-with-hmachttpmessagehandler)
- [In-Depth Tutorial](#in-depth-tutorial)
    * [Event handling with the `HmacEvents` object](#event-handling-with-the-hmacevents-object)
    * [Dynamic policies with `IHmacPolicyCollection`](#dynamic-policies-with-ihmacpolicycollection)
    * [Dynamic policies with a scoped `IHmacPolicyCollection`](#dynamic-policies-with-a-scoped-ihmacpolicycollection)
    * [Enable consolidated headers](#enable-consolidated-headers)
    * [Custom signing content for `Hmac`](#custom-signing-content-for-an-hmac)

# Quickstart

A short and sweet overview of how to register `HmacManager` to help you get up and running. There are two methods of dependency injection registration. You should choose the one appropriate for your situation and how much flexibility you might require.

- [Register without built-in authentication flow](#register-without-built-in-authentication-flow)
    - Registers only the `HmacManager` object through an `IHmacManagerFactory` service where you will be required to handle signatures and verification manually. An implementation of `IHmacManagerFactory` is registered with the DI container automatically. This is how you will instantiate `HmacManager` objects. 
- [Register with built-in authentication flow](#register-with-built-in-authentication-flow)
    - Automatically registers an authentication handler and maps any authenticated request headers defined by a scheme to claims. This method handles verifying incoming requests without any additional setup. 

## Register without built-in authentication flow

Use the `IServiceCollection` extension method `AddHmacManager` to add all of the necessary components for `HmacManager` to the DI container.

    builder.Services.AddHmacManager(options => ...);

Configure one or more policies with the options builder.

    options.AddPolicy("SomePolicy", policy =>
    {
        policy.UsePublicKey(...);
        policy.UsePrivateKey(...);
        policy.UseMemoryCache(...);
    });

Access an instance of a `HmacManager` responsible for a specified policy from `IHmacManagerFactory`.

    var hmacManager = hmacManagerFactory.Create("SomePolicy");

> [!NOTE]
> An implementation of `IHmacManagerFactory` is automatically registered with the DI container so it can be accessed anywhere services can be injected.

A policy can be extended with schemes. These schemes represent the required headers that must be present in a request. The values of these headers become a part of the signing content.

    builder.Services.AddHmacManager(options =>
    {
        options.AddPolicy("SomePolicy", policy =>
        {
            policy.UsePublicKey(...);
            policy.AddScheme("SomeScheme", scheme =>
            {
                scheme.AddHeader("X-UserId");
                scheme.AddHeader("X-Email");
            });
        });
    });

> [!IMPORTANT]
> All headers that are defined on a scheme must be added to the `HttpRequestMessage` prior to calling `SignAsync` on an `HmacManager` instance.

## Register with built-in authentication flow

The `AddHmacManager` extension method can be bypassed in favor of the `IAuthenticationBuilder` extension method `AddHmac`. 

    builder.Services
        .AddAuthentication()
        .AddHmac(options => options.AddPolicy("SomePolicy", policy => ...));

- The `HmacAuthenticationHandler` handles parsing incoming requests and authenticating the correct scheme.
    - By default, if there is a policy that matches the one defined in the request headers that can be successfully verified, then the handler returns a success. If more granular authentication is required, such as protecting routes with specific policies or schemes, then there are a couple options available:
        - Use `HmacAuthenticateAttribute` to specify exact policies and schemes required to authenticate a given endpoint.

                [HmacAuthenticate(Policy = "HmacPolicy", Scheme = "HmacScheme")]
                public class HomeController : Controller

        - Use `HmacAuthenticateAttribute` as an `IAuthorizationRequirement` to an authorization policy and register the `HmacAuthorizationHandler` to handle the requirement automatically.

                builder.Services.AddAuthorization(options => 
                {
                    options.AddPolicy("RequireHmac", policy => 
                        policy.AddRequirements(new HmacAuthenticateAttribute 
                        { 
                            Policy = "HmacPolicy", 
                            Scheme = "HmacScheme"
                        }));
                });

        - Use the `AuthorizationPolicyBuilder` extensions `RequireHmacPolicy` and `RequireHmacScheme` to add hmac policy and scheme requirements to an authorization policy.

                builder.Services.AddAuthorization(options => 
                {
                    options.AddPolicy("RequireHmac", policy =>
                    {
                        policy.RequireHmacPolicy("HmacPolicy");
                        policy.RequireHmacScheme("HmacScheme");
                    });
                });

        - Use the `AuthorizationPolicyBuilder` extension `RequireHmacAuthentication` to add hmac policy and scheme requirements to an authorization policy.

                builder.Services.AddAuthorization(options => 
                {
                    options.AddPolicy("RequireHmac", policy =>
                    {
                        policy.RequireHmacAuthentication("HmacPolicy", "HmacScheme");
                    });
                });

> [!NOTE]
> Any scheme headers are mapped to their specified claim types. If no claim type is specified, the name of the header is used instead.

## Register with an `IConfigurationSection`

Both `AddHmacManager` and `AddHmac` have an overload which accepts an `IConfigurationSection` that corresponds to the json schema below. An example can be found [here](../../samples/WebToApiAuthenticationWithJsonConfiguration/README.md).

    [
        {
            "Name": "Some_Policy",
            "Keys": {
                "PublicKey": "37e3e675-370a-4ba9-af74-68f99b539f03",
                "PrivateKey": "zvg29s2cQ4idOqbUJWETOw=="
            },
            "Algorithms": {
                "ContentHashAlgorithm": "SHA256",
                "SigningHashAlgorithm": "HMACSHA256"
            },
            "Nonce": {
                "CacheType": "Memory",
                "MaxAgeInSeconds": 100
            },
            "Schemes": [
                {
                    "Name": "Some_Scheme",
                    "Headers": [
                        {
                            "Name": "Some_Header_1",
                            "ClaimType": "Header_1_ClaimType"
                        }
                    ]
                }
            ]
        }
    ]

The following properties are restricted to the following values.

| Property | Values | Additional Information |
| -------- | ------ | ---------------------- |
| `PublicKey` | Guid String | [Validation Details](./Validation/Validators/PublicKeyValidator.cs) |
| `PrivateKey` | Base64 Encoded String | [Validation Details](./Validation/Validators/PrivateKeyValidator.cs) |
| `ContentHashAlgorithm` | SHA1, SHA256, SHA512 | [Enum](./Components/Enums/ContentHashAlgorithm.cs) |
| `SigningHashAlgorithm` | HMACSHA1, HMACSHA256, HMACSHA512 | [Enum](./Components/Enums/SignatureHashAlgorithm.cs) |
| `CacheType` | Memory, Distributed | [Enum](./Caching/Enums/NonceCacheType.cs) |

## Register `HttpClient` with `HmacHttpMessageHandler`

The `AddHmacHttpMessageHandler` extension method registers an instance of `HmacDelegatingHandler` to the specified `HttpClient` with the specified policy and the optional scheme. This handler will automatically sign outgoing requests for that client. If the request cannot be signed, then an `HmacSigningException` exception is thrown.

    builder.Services.AddHttpClient("Hmac", client => ...)
        .AddHmacHttpMessageHandler("MyPolicy", "MyScheme");

> [!NOTE]  
> If a scheme is specified, then all headers in that scheme must be added to the request prior to calling `Send` or `SendAsync` on the `HttpClient`. By default the corresponding header values will become part of the signing content used to create the hmac.

# In-Depth Tutorial

This is where you can find a comprehensive guide on all of the functionality available to your disposal. This is currently a work in progress.

## Event handling with the `HmacEvents` object

One or more event handlers can be defined within the `AuthenticationBuilder` extension method `AddHmac`. 

    options.Events = new HmacEvents
    {
        OnValidateKeysAsync = (context, keys) => {...},
        OnAuthenticationSuccessAsync = (context, hmacResult) => {...},
        OnAuthenticationFailureAsync = (context, hmacResult) => {...}
    };

If using the `IConfigurationSection` overload of `AddHmac` then there is
an optional second parameter for `HmacEvents`.

    builder.Services.AddAuthentication()
        .AddHmac(configurationSection, new HmacEvents
        {
            OnValidateKeysAsync = (context, keys) => {...},
            OnAuthenticationSuccessAsync = (context, hmacResult) => {...},
            OnAuthenticationFailureAsync = (context, hmacResult) => {...}
        });

Events are executed through user defined delegates at different points within the `HmacAuthenticationHandler` flow. 

| Event | Path | Return |
| -------- | ------ | ------ |
| OnValidateKeysAsync | Executes after a signature has been parsed from an incoming request but before any attempts at verification | `Task<bool>`
| OnAuthenticationSuccessAsync | Executes upon a successful signature verification | `Task<Claim[]>`
| OnAuthenticationFailureAsync | Executes upon a failed signature verification | `Task<Exception>`

> [!NOTE]
> The default values for `HmacEvents` return pass through values, i.e. *OnValidateKeysAsync* returns a `Task` with the value `true`, *OnAuthenticationSuccessAsync* returns a `Task` with an empty `Claim[]` and *OnAuthenticationFailureAsync* returns a `Task` with an `HmacAuthenticationException`.

## Dynamic Policies with `IHmacPolicyCollection`

An implementation of `IHmacPolicyCollection` can be requested through the DI container and manipulated at runtime.

> [!NOTE]
> An implementation of `IHmacPolicyCollection` is automatically registered as a singleton when using the extension methods `AddHmacManager` or `AddHmac`.

- A policy can be added by constructing a new `HmacPolicy`.

        Policies.Add(new HmacPolicy {...});

- A policy can be removed by specifying the name of the policy to remove.

        Policies.Remove("Some Policy");

> [!CAUTION]
> Since `IHmacPolicyCollection` is a singleton, any changes to the collection will not 
persist across application restarts. Any backing store will need to be handled manually with the above approach. See below for the preferred approach to dynamic policies.

## Dynamic policies with a scoped `IHmacPolicyCollection`

Scoped policies can be enabled through both `AddHmac` and `AddHmacManager` extension methods. If scoped policies are enabled, all other policy configurations are disregarded. The `IServiceProvider` is available so that policies can be pulled from a database (or anywhere, really) if the ability to add and remove policies on a per request basis is required.

    services.AddHmacManager(options =>
    {
        options.EnableScopedPolicies(serviceProvider =>
        {
            var policies = new HmacPolicyCollection();

            // Create one or more policies
            var builder = new HmacPolicyBuilder(...);

            builder.UsePublicKey(...);
            builder.UsePrivateKey(...);

            // Continue policy configurations

            // Add policy to the collection
            policies.Add(builder.Build());

            return policies;
        });
    });

> [!NOTE]
> There is an additional overhead added since every authentication request will now use this delegate to access the current state of policies. If these are stored in a database, an additional hit will be incurred. There is currently no built support for a short lived caching mechanism. 

## Enable consolidated headers

By default, `HmacManager` adds the following headers in order to verify a signature.

| Header | Description | Value |
| ------ | ----------- | ----- |
| Hmac-Policy | Name of the policy | `string` |
| Hmac-Scheme | Name of the optional scheme | `string` |
| Hmac-Nonce | Nonce value | `Guid` |
| Hmac-DateRequested | Date the request was made in unix time milliseconds | `long` |

These can be configured to be consolidated into a single header with the name **Hmac-Options** and the value as a Base64 encoded concatenation of the headers described in the table above. 

> [!NOTE]
> Consolidated headers must be either enabled or disabled for both client and server otherwise authentication will fail.

    .AddHmacManager(options =>
    {
        options.EnableConsolidatedHeaders();
    });

## Custom Signing Content for an `Hmac`

> [!CAUTION]
> If this method is used, then the requirement of determining unique signing content per request falls on the user. Components like the date requested or nonce are NOT automatically added to the content for hashing and should be added by the implementation.

The signing content for an `Hmac` can be configured per policy. This allows user defined structures to be used as the input to the signature hash function.

    policy.UseSigningContentBuilder(context => 
    {
        var method = context.Request.Method;
        var suffix = $"{context.DateRequested}:{context.Nonce}";
        return $"{method}:{suffix}";
    });