using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using Azure.Core;

namespace GarageGroup.Infra;

internal sealed partial class TokenCredentialHandler : DelegatingHandler
{
    private const string ScopeRelativeUri = "/.default";

    private const string AuthorizationScheme = "Bearer";

    private const string ResourceTokenTemplate = "type=aad&ver=1.0&sig={0}";

    private readonly TokenCredential tokenCredential;

    private readonly TokenType tokenType;

    private readonly string[] scopes;

    internal TokenCredentialHandler(
        HttpMessageHandler innerHandler,
        TokenCredential tokenCredential,
        TokenType tokenType,
        [AllowNull] string[] scopes = null)
        : base(innerHandler)
    {
        this.tokenCredential = tokenCredential;
        this.tokenType = tokenType;
#if NET8_0_OR_GREATER
        this.scopes = scopes ?? [];
#else
        this.scopes = scopes ?? Array.Empty<string>();
#endif
    }
}