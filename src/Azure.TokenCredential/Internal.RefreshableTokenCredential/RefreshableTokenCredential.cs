using Azure.Core;

namespace GarageGroup.Infra;

internal sealed partial class RefreshableTokenCredential : TokenCredential, ITokensRefreshSupplier
{
    private const int MaxDegreeOfParallelism = 4;

    private readonly TokenCredentialProvider credentialProvider;

    private readonly AccessTokenInMemoryCache inMemoryCache;

    private readonly AccessTokenRemoteCache? remoteCache;

    internal RefreshableTokenCredential(
        TokenCredentialProvider credentialProvider,
        AccessTokenInMemoryCache inMemoryCache,
        AccessTokenRemoteCache? remoteCache)
    {
        this.credentialProvider = credentialProvider;
        this.inMemoryCache = inMemoryCache;
        this.remoteCache = remoteCache;
    }
}
