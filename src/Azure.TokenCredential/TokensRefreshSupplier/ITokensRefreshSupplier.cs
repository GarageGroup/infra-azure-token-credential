using System.Threading;
using System.Threading.Tasks;

namespace GarageGroup.Infra;

public interface ITokensRefreshSupplier
{
    Task RefreshTokensAsync(CancellationToken cancellationToken);
}