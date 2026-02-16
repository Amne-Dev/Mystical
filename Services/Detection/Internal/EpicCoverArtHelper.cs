using System.Text.Json;

namespace Mystical.WinUI.Services.Detection.Internal;

internal static class EpicCoverArtHelper
{
    private static readonly string[] PreferredImageTypes =
    {
        "DieselGameBoxTall",
        "OfferImageTall",
        "DieselGameBox",
        "OfferImageWide",
        "Thumbnail"
    };

    public static string ResolveCoverArtUrl(JsonElement sourceElement, string catalogItemId, string appName)
    {
        var keyImageUrl = TryGetBestKeyImageUrl(sourceElement);
        if (!string.IsNullOrWhiteSpace(keyImageUrl))
        {
            return keyImageUrl;
        }

        return BuildFallbackOfferCoverUrl(catalogItemId, appName);
    }

    public static string BuildFallbackOfferCoverUrl(string catalogItemId, string appName)
    {
        var key = !string.IsNullOrWhiteSpace(catalogItemId) ? catalogItemId : appName;
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        return $"https://cdn1.epicgames.com/offer/{key}/wide/854x480.jpg";
    }

    public static string PickBetterCoverUrl(string primaryCoverUrl, string secondaryCoverUrl)
    {
        var primary = NormalizeImageUrl(primaryCoverUrl);
        var secondary = NormalizeImageUrl(secondaryCoverUrl);

        if (string.IsNullOrWhiteSpace(primary))
        {
            return secondary;
        }

        if (string.IsNullOrWhiteSpace(secondary))
        {
            return primary;
        }

        var primaryWeak = IsWeakCoverUrl(primary);
        var secondaryWeak = IsWeakCoverUrl(secondary);

        if (primaryWeak && !secondaryWeak)
        {
            return secondary;
        }

        if (!primaryWeak && secondaryWeak)
        {
            return primary;
        }

        return primary;
    }

    public static bool IsWeakCoverUrl(string coverUrl)
    {
        if (string.IsNullOrWhiteSpace(coverUrl))
        {
            return true;
        }

        if (!Uri.TryCreate(coverUrl, UriKind.Absolute, out _))
        {
            return true;
        }

        if (coverUrl.Contains("epicgames.com/offer/", StringComparison.OrdinalIgnoreCase) &&
            coverUrl.Contains("/wide/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return coverUrl.Contains("imdb.com", StringComparison.OrdinalIgnoreCase) ||
               coverUrl.Contains("media-imdb.com", StringComparison.OrdinalIgnoreCase);
    }

    private static string TryGetBestKeyImageUrl(JsonElement element)
    {
        if (!TryFindKeyImagesArray(element, depth: 5, out var keyImages))
        {
            return string.Empty;
        }

        var allImages = new List<(string Type, string Url)>();
        foreach (var image in keyImages.EnumerateArray())
        {
            var imageUrl = NormalizeImageUrl(GetString(image, "url"));
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                imageUrl = NormalizeImageUrl(GetString(image, "imageUrl"));
            }

            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                continue;
            }

            var type = GetString(image, "type");
            allImages.Add((type, imageUrl));
        }

        if (allImages.Count == 0)
        {
            return string.Empty;
        }

        foreach (var preferredType in PreferredImageTypes)
        {
            var matched = allImages.FirstOrDefault(image =>
                string.Equals(image.Type, preferredType, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(matched.Url))
            {
                return matched.Url;
            }
        }

        return allImages[0].Url;
    }

    private static bool TryFindKeyImagesArray(JsonElement element, int depth, out JsonElement keyImagesArray)
    {
        keyImagesArray = default;
        if (depth < 0)
        {
            return false;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.Equals("keyImages", StringComparison.OrdinalIgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.Array)
                {
                    keyImagesArray = property.Value;
                    return true;
                }

                if ((property.Value.ValueKind == JsonValueKind.Object || property.Value.ValueKind == JsonValueKind.Array) &&
                    TryFindKeyImagesArray(property.Value, depth - 1, out keyImagesArray))
                {
                    return true;
                }
            }

            return false;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if ((item.ValueKind == JsonValueKind.Object || item.ValueKind == JsonValueKind.Array) &&
                    TryFindKeyImagesArray(item, depth - 1, out keyImagesArray))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string NormalizeImageUrl(string value)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        if (trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            return $"https:{trimmed}";
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out _))
        {
            return trimmed;
        }

        return string.Empty;
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }
}
