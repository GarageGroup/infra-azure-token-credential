using System;
using System.Net.Http;
using Azure.Core;
using Microsoft.Extensions.DependencyInjection;
using PrimeFuncPack;

namespace GarageGroup.Infra;

public static class TokenCredentialHandlerDependency
{
    public static Dependency<HttpMessageHandler> UseTokenCredentialStandard(
        this Dependency<HttpMessageHandler> dependency, params string[] scopes)
    {
        ArgumentNullException.ThrowIfNull(dependency);
        return dependency.Map<HttpMessageHandler>(ResolveHandler);

        TokenCredentialHandler ResolveHandler(IServiceProvider serviceProvider, HttpMessageHandler httpMessageHandler)
        {
            ArgumentNullException.ThrowIfNull(serviceProvider);
            ArgumentNullException.ThrowIfNull(httpMessageHandler);

            return new(httpMessageHandler, serviceProvider.GetRequiredService<TokenCredential>(), scopes);
        }
    }

    public static Dependency<HttpMessageHandler> UseTokenCredentialStandard(
        this Dependency<HttpMessageHandler> dependency,
        Func<IServiceProvider, string[]> scopesResolver)
    {
        ArgumentNullException.ThrowIfNull(dependency);
        ArgumentNullException.ThrowIfNull(scopesResolver);

        return dependency.Map<HttpMessageHandler>(ResolveHandler);

        TokenCredentialHandler ResolveHandler(IServiceProvider serviceProvider, HttpMessageHandler httpMessageHandler)
        {
            ArgumentNullException.ThrowIfNull(serviceProvider);
            ArgumentNullException.ThrowIfNull(httpMessageHandler);

            return new(httpMessageHandler, serviceProvider.GetRequiredService<TokenCredential>(), scopesResolver.Invoke(serviceProvider));
        }
    }

    public static Dependency<HttpMessageHandler> UseTokenCredential(
        this Dependency<HttpMessageHandler> dependency,
        Func<IServiceProvider, TokenCredential> tokenCredentialResolver,
        params string[] scopes)
    {
        ArgumentNullException.ThrowIfNull(dependency);
        ArgumentNullException.ThrowIfNull(tokenCredentialResolver);

        return dependency.Map<HttpMessageHandler>(ResolveHandler);

        TokenCredentialHandler ResolveHandler(IServiceProvider serviceProvider, HttpMessageHandler httpMessageHandler)
        {
            ArgumentNullException.ThrowIfNull(serviceProvider);
            ArgumentNullException.ThrowIfNull(httpMessageHandler);

            return new(httpMessageHandler, tokenCredentialResolver.Invoke(serviceProvider), scopes);
        }
    }

    public static Dependency<HttpMessageHandler> UseTokenCredential(
        this Dependency<HttpMessageHandler> dependency,
        Func<IServiceProvider, TokenCredential> tokenCredentialResolver,
        Func<IServiceProvider, string[]> scopesResolver)
    {
        ArgumentNullException.ThrowIfNull(dependency);
        ArgumentNullException.ThrowIfNull(tokenCredentialResolver);
        ArgumentNullException.ThrowIfNull(scopesResolver);

        return dependency.Map<HttpMessageHandler>(ResolveHandler);

        TokenCredentialHandler ResolveHandler(IServiceProvider serviceProvider, HttpMessageHandler httpMessageHandler)
        {
            ArgumentNullException.ThrowIfNull(serviceProvider);
            ArgumentNullException.ThrowIfNull(httpMessageHandler);

            return new(httpMessageHandler, tokenCredentialResolver.Invoke(serviceProvider), scopesResolver.Invoke(serviceProvider));
        }
    }
}