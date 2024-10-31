using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Azure.Core;

namespace GarageGroup.Infra;

internal sealed partial class RefreshableTokenCredential : TokenCredential, ITokensRefreshSupplier
{
    private const int ExpirationPeriodInMinutes = 3;

    private readonly TokenCredentialProvider innerCredentialProvider;

    private readonly ConcurrentDictionary<TokenRequestContext, AccessToken> inMemoryCache;

    internal RefreshableTokenCredential(TokenCredentialProvider innerCredentialProvider)
    {
        this.innerCredentialProvider = innerCredentialProvider;
        inMemoryCache = new(InnerTokenRequestContextEqualityComparer.Instance);
    }

    private sealed class InnerTokenRequestContextEqualityComparer : IEqualityComparer<TokenRequestContext>
    {
        internal static readonly InnerTokenRequestContextEqualityComparer Instance
            =
            new();

        public bool Equals(TokenRequestContext x, TokenRequestContext y)
        {
            if (string.Equals(x.ParentRequestId, y.ParentRequestId, StringComparison.Ordinal) is false)
            {
                return false;
            }

            if (string.Equals(x.Claims, y.Claims, StringComparison.Ordinal) is false)
            {
                return false;
            }

            if (string.Equals(x.TenantId, y.TenantId, StringComparison.Ordinal) is false)
            {
                return false;
            }

            if ((x.IsCaeEnabled != y.IsCaeEnabled) || (x.IsProofOfPossessionEnabled != y.IsProofOfPossessionEnabled))
            {
                return false;
            }

            if (string.Equals(x.ProofOfPossessionNonce, y.ProofOfPossessionNonce, StringComparison.Ordinal) is false)
            {
                return false;
            }

            if (string.Equals(x.ResourceRequestMethod, y.ResourceRequestMethod, StringComparison.Ordinal) is false)
            {
                return false;
            }

            if (x.ResourceRequestUri != y.ResourceRequestUri)
            {
                return false;
            }

            if (x.Scopes?.Length != y.Scopes?.Length)
            {
                return false;
            }

            if (x.Scopes?.Length is not > 0)
            {
                return true;
            }

            for (int i = 0; i < x.Scopes.Length; i++)
            {
                // y.Scopes can not be null here. It's checked
                if (string.Equals(x.Scopes[i], y.Scopes![i], StringComparison.Ordinal))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        public int GetHashCode([DisallowNull] TokenRequestContext obj)
        {
            HashCode builder = new();

            builder.Add(obj.ParentRequestId?.GetHashCode());
            builder.Add(obj.Claims?.GetHashCode());
            builder.Add(obj.TenantId?.GetHashCode());

            builder.Add(obj.IsCaeEnabled.GetHashCode());
            builder.Add(obj.IsProofOfPossessionEnabled.GetHashCode());

            builder.Add(obj.ProofOfPossessionNonce?.GetHashCode());
            builder.Add(obj.ResourceRequestMethod?.GetHashCode());
            builder.Add(obj.ResourceRequestUri?.GetHashCode());

            if (obj.Scopes is null)
            {
                builder.Add<string[]?>(null);
                return builder.ToHashCode();
            }

            foreach (var scope in obj.Scopes)
            {
                builder.Add(scope?.GetHashCode());
            }

            return builder.ToHashCode();
        }
    }
}