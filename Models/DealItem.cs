using WinRT;

namespace Mystical.WinUI.Models;

[GeneratedBindableCustomProperty]
public sealed partial class DealItem
{
    public string Title { get; set; } = string.Empty;

    public string Store { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal OriginalPrice { get; set; }

    public decimal SalePrice { get; set; }

    public int SavingsPercent { get; set; }

    public string ImageUrl { get; set; } = string.Empty;

    public string DealUrl { get; set; } = string.Empty;

    public DateTimeOffset? StartsAt { get; set; }

    public DateTimeOffset? EndsAt { get; set; }

    public bool IsFree => SalePrice <= 0m;

    public Uri? ImageUri => Uri.TryCreate(ImageUrl, UriKind.Absolute, out var uri) ? uri : null;

    public Uri? DealUri => Uri.TryCreate(DealUrl, UriKind.Absolute, out var uri) ? uri : null;

    public string PriceText => IsFree
        ? "Free"
        : $"${SalePrice:0.00} (was ${OriginalPrice:0.00})";

    public string SavingsText => SavingsPercent > 0 ? $"-{SavingsPercent}%" : "Deal";

    public string AvailabilityText
    {
        get
        {
            if (StartsAt is not null && EndsAt is not null)
            {
                return $"{StartsAt.Value.LocalDateTime:g} - {EndsAt.Value.LocalDateTime:g}";
            }

            if (EndsAt is not null)
            {
                return $"Ends {EndsAt.Value.LocalDateTime:g}";
            }

            return string.Empty;
        }
    }
}
