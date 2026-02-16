using Mystical.WinUI.Models;

namespace Mystical.WinUI.Services;

public interface IDealsService
{
    Task<DealsSnapshot> FetchDealsAsync(CancellationToken cancellationToken = default);
}
