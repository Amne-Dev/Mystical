using System.Globalization;
using System.Text.Json;
using Mystical.WinUI.Models;

namespace Mystical.WinUI.Services;

public sealed class DealsService : IDealsService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    private static readonly Dictionary<string, string> StoreNameById = new(StringComparer.OrdinalIgnoreCase)
    {
        ["1"] = "Steam",
        ["7"] = "GOG",
        ["25"] = "Epic Games"
    };

    private const string CheapSharkDealsUrl = "https://www.cheapshark.com/api/1.0/deals?storeID=1,7,25&upperPrice=70&pageSize=80&sortBy=Savings";
    private const string EpicFreeGamesUrl = "https://store-site-backend-static.ak.epicgames.com/freeGamesPromotions?locale=en-US&country=US&allowCountries=US";

    public async Task<DealsSnapshot> FetchDealsAsync(CancellationToken cancellationToken = default)
    {
        var cheapSharkDealsTask = FetchCheapSharkDealsAsync(cancellationToken);
        var epicFreeDealsTask = FetchEpicFreeDealsAsync(cancellationToken);

        await Task.WhenAll(cheapSharkDealsTask, epicFreeDealsTask);

        var cheapSharkDeals = cheapSharkDealsTask.Result;
        var epicFree = epicFreeDealsTask.Result;

        var massive = cheapSharkDeals
            .Where(d => d.SavingsPercent >= 70 && d.SalePrice > 0m)
            .OrderByDescending(d => d.SavingsPercent)
            .ThenBy(d => d.SalePrice)
            .Take(40)
            .ToList();

        var freeNow = cheapSharkDeals
            .Where(d => d.IsFree)
            .Concat(epicFree.FreeNow)
            .GroupBy(d => NormalizeTitleKey(d.Title, d.Store), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(d => d.Store)
            .ThenBy(d => d.Title, StringComparer.OrdinalIgnoreCase)
            .Take(30)
            .ToList();

        var freeSoon = epicFree.FreeSoon
            .OrderBy(d => d.StartsAt)
            .Take(30)
            .ToList();

        return new DealsSnapshot
        {
            MassiveDiscountDeals = massive,
            FreeNowDeals = freeNow,
            FreeSoonDeals = freeSoon,
            FetchedAt = DateTimeOffset.UtcNow
        };
    }

    private static async Task<IReadOnlyList<DealItem>> FetchCheapSharkDealsAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, CheapSharkDealsUrl);
        using var response = await HttpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<DealItem>();
        }

        var deals = new List<DealItem>();

        foreach (var element in document.RootElement.EnumerateArray())
        {
            var title = GetString(element, "title");
            var dealId = GetString(element, "dealID");
            var thumb = GetString(element, "thumb");
            var storeId = GetString(element, "storeID");

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(dealId))
            {
                continue;
            }

            var deal = new DealItem
            {
                Title = title,
                Store = StoreNameById.TryGetValue(storeId, out var storeName) ? storeName : "Store",
                Description = "Massive discount deal",
                OriginalPrice = GetDecimal(element, "normalPrice"),
                SalePrice = GetDecimal(element, "salePrice"),
                SavingsPercent = (int)Math.Round(GetDecimal(element, "savings"), MidpointRounding.AwayFromZero),
                ImageUrl = thumb,
                DealUrl = $"https://www.cheapshark.com/redirect?dealID={Uri.EscapeDataString(dealId)}"
            };

            deals.Add(deal);
        }

        return deals;
    }

    private static async Task<(IReadOnlyList<DealItem> FreeNow, IReadOnlyList<DealItem> FreeSoon)> FetchEpicFreeDealsAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, EpicFreeGamesUrl);
        using var response = await HttpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!TryGetProperty(document.RootElement, out var elements, "data", "Catalog", "searchStore", "elements") ||
            elements.ValueKind != JsonValueKind.Array)
        {
            return (Array.Empty<DealItem>(), Array.Empty<DealItem>());
        }

        var now = new List<DealItem>();
        var soon = new List<DealItem>();

        foreach (var element in elements.EnumerateArray())
        {
            var title = GetString(element, "title");
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var imageUrl = GetEpicImageUrl(element);
            var productUrl = GetEpicProductUrl(element);
            var originalPrice = GetEpicOriginalPrice(element);

            if (TryGetProperty(element, out var promotionalOffers, "promotions", "promotionalOffers") &&
                promotionalOffers.ValueKind == JsonValueKind.Array)
            {
                foreach (var offerGroup in promotionalOffers.EnumerateArray())
                {
                    if (!offerGroup.TryGetProperty("promotionalOffers", out var offers) || offers.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var offer in offers.EnumerateArray())
                    {
                        var discountPercentage = GetNestedInt(offer, "discountSetting", "discountPercentage");
                        if (discountPercentage != 0)
                        {
                            continue;
                        }

                        var startsAt = GetDateTimeOffset(offer, "startDate");
                        var endsAt = GetDateTimeOffset(offer, "endDate");

                        now.Add(new DealItem
                        {
                            Title = title,
                            Store = "Epic Games",
                            Description = "Free now",
                            OriginalPrice = originalPrice,
                            SalePrice = 0m,
                            SavingsPercent = 100,
                            ImageUrl = imageUrl,
                            DealUrl = productUrl,
                            StartsAt = startsAt,
                            EndsAt = endsAt
                        });
                    }
                }
            }

            if (TryGetProperty(element, out var upcomingPromotionalOffers, "promotions", "upcomingPromotionalOffers") &&
                upcomingPromotionalOffers.ValueKind == JsonValueKind.Array)
            {
                foreach (var offerGroup in upcomingPromotionalOffers.EnumerateArray())
                {
                    if (!offerGroup.TryGetProperty("promotionalOffers", out var offers) || offers.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var offer in offers.EnumerateArray())
                    {
                        var discountPercentage = GetNestedInt(offer, "discountSetting", "discountPercentage");
                        if (discountPercentage != 0)
                        {
                            continue;
                        }

                        var startsAt = GetDateTimeOffset(offer, "startDate");
                        var endsAt = GetDateTimeOffset(offer, "endDate");

                        soon.Add(new DealItem
                        {
                            Title = title,
                            Store = "Epic Games",
                            Description = "Free soon",
                            OriginalPrice = originalPrice,
                            SalePrice = 0m,
                            SavingsPercent = 100,
                            ImageUrl = imageUrl,
                            DealUrl = productUrl,
                            StartsAt = startsAt,
                            EndsAt = endsAt
                        });
                    }
                }
            }
        }

        return (
            now.GroupBy(d => NormalizeTitleKey(d.Title, d.Store), StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList(),
            soon.GroupBy(d => NormalizeTitleKey(d.Title, d.Store), StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList());
    }

    private static string NormalizeTitleKey(string title, string store)
    {
        return $"{store}:{title.Trim().ToLowerInvariant()}";
    }

    private static bool TryGetProperty(JsonElement root, out JsonElement value, params string[] path)
    {
        value = root;

        foreach (var segment in path)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out value))
            {
                return false;
            }
        }

        return true;
    }

    private static string GetEpicImageUrl(JsonElement element)
    {
        if (!element.TryGetProperty("keyImages", out var keyImages) || keyImages.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var prioritizedTypes = new[]
        {
            "OfferImageWide",
            "DieselStoreFrontWide",
            "Thumbnail",
            "VaultClosed"
        };

        foreach (var type in prioritizedTypes)
        {
            foreach (var image in keyImages.EnumerateArray())
            {
                if (GetString(image, "type").Equals(type, StringComparison.OrdinalIgnoreCase))
                {
                    var url = GetString(image, "url");
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        return url;
                    }
                }
            }
        }

        foreach (var image in keyImages.EnumerateArray())
        {
            var url = GetString(image, "url");
            if (!string.IsNullOrWhiteSpace(url))
            {
                return url;
            }
        }

        return string.Empty;
    }

    private static string GetEpicProductUrl(JsonElement element)
    {
        var slug = string.Empty;

        if (TryGetProperty(element, out var mappings, "catalogNs", "mappings") && mappings.ValueKind == JsonValueKind.Array)
        {
            var first = mappings.EnumerateArray().FirstOrDefault();
            if (first.ValueKind == JsonValueKind.Object)
            {
                slug = GetString(first, "pageSlug");
            }
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = GetString(element, "productSlug");
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = GetString(element, "urlSlug");
        }

        return string.IsNullOrWhiteSpace(slug)
            ? "https://store.epicgames.com/"
            : $"https://store.epicgames.com/en-US/p/{slug}";
    }

    private static decimal GetEpicOriginalPrice(JsonElement element)
    {
        if (TryGetProperty(element, out var originalPriceElement, "price", "totalPrice", "fmtPrice", "originalPrice") &&
            originalPriceElement.ValueKind == JsonValueKind.String &&
            decimal.TryParse(originalPriceElement.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var formattedPrice))
        {
            return formattedPrice;
        }

        if (TryGetProperty(element, out var discountPriceElement, "price", "totalPrice", "originalPrice") &&
            discountPriceElement.ValueKind == JsonValueKind.Number &&
            discountPriceElement.TryGetInt32(out var rawCents))
        {
            return rawCents / 100m;
        }

        return 0m;
    }

    private static DateTimeOffset? GetDateTimeOffset(JsonElement element, string propertyName)
    {
        var value = GetString(element, propertyName);
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
    }

    private static int GetNestedInt(JsonElement element, string nestedObjectName, string propertyName)
    {
        if (!element.TryGetProperty(nestedObjectName, out var nestedObject) || nestedObject.ValueKind != JsonValueKind.Object)
        {
            return -1;
        }

        if (!nestedObject.TryGetProperty(propertyName, out var value))
        {
            return -1;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var intValue))
        {
            return intValue;
        }

        return -1;
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static decimal GetDecimal(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return 0m;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var decimalValue))
        {
            return decimalValue;
        }

        if (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var stringValue))
        {
            return stringValue;
        }

        return 0m;
    }
}
