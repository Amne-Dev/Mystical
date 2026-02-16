namespace Mystical.WinUI.Models;

public sealed class DealsSnapshot
{
    public IReadOnlyList<DealItem> MassiveDiscountDeals { get; init; } = Array.Empty<DealItem>();

    public IReadOnlyList<DealItem> FreeNowDeals { get; init; } = Array.Empty<DealItem>();

    public IReadOnlyList<DealItem> FreeSoonDeals { get; init; } = Array.Empty<DealItem>();

    public DateTimeOffset FetchedAt { get; init; } = DateTimeOffset.UtcNow;
}
