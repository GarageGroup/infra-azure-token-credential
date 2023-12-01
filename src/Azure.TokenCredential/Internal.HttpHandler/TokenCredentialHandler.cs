using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using Azure.Core;

namespace GarageGroup.Infra;

internal sealed partial class TokenCredentialHandler : DelegatingHandler
{
    private const string ScopeRelativeUri = "/.default";

    private const string AuthorizationScheme = "Bearer";

    private readonly TokenCredential tokenCredential;

    private readonly string[] scopes;

    internal TokenCredentialHandler(
        HttpMessageHandler innerHandler,
        TokenCredential tokenCredential,
        [AllowNull] string[] scopes = null)
        : base(innerHandler)
    {
        this.tokenCredential = tokenCredential;
#if NET8_0_OR_GREATER
        this.scopes = scopes ?? [];
#else
        this.scopes = scopes ?? Array.Empty<string>();
#endif
    }

    private TokenRequestContext? CreateRequestContext(Uri? requestUri)
    {
        if (scopes.Length > 0)
        {
            return new(
                scopes: scopes);
        }

        if (requestUri is null)
        {
            return null;
        }

#if NET8_0_OR_GREATER
        return new(
            scopes:
            [
                new Uri(requestUri, ScopeRelativeUri).ToString()
            ]);
#else
        return new(
            scopes: new[]
            {
                new Uri(requestUri, ScopeRelativeUri).ToString()
            });
#endif
    }
}