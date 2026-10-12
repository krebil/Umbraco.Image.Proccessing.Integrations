using Umbraco.Image.Processing.Core.Routing;

namespace Umbraco.Image.Processing.Service;

/// <summary>
/// Where one source (<c>ImageProcessing:Sources:N:Storage:Mode</c>) resolves original (unprocessed) images
/// from. LocalDisk reads a directory (<c>Storage:LocalDisk:RootPath</c>), e.g. a disk/volume shared with
/// Umbraco; AzureBlob reads a Blob container (<c>Storage:AzureBlob</c>), e.g. the one Umbraco's media file
/// system writes to; HttpProxy fetches the raw original from Umbraco itself over HTTP (<c>Storage:HttpProxy</c>).
/// </summary>
internal enum ImageStorageMode
{
    LocalDisk,
    AzureBlob,
    HttpProxy,
}

/// <summary>One entry of <c>ImageProcessing:Sources</c> as this service registers it: its key in the list, its normalized prefix, and its storage.</summary>
internal sealed record ConfiguredImageSource(string Key, string RoutePrefix, ImageStorageMode Mode, IConfigurationSection Storage);

/// <summary>
/// Reads <c>ImageProcessing:Sources:N:Storage</c> and refuses a configuration that would silently read the
/// wrong place. One source keeps the 2.x defaults (LocalDisk, <c>wwwroot/media</c>). With several, each must
/// say where its original images come from: an explicit <c>Storage:Mode</c> and the settings that mode needs.
/// The routes themselves (RoutePrefix, PathPattern) are checked by Core when the host starts.
/// </summary>
internal static class ImageSourceConfiguration
{
    private static readonly string[] SettingsRemovedIn3 = ["RoutePrefix", "Storage", "Proxy", "OriginalsRootPath"];

    public static IReadOnlyList<ConfiguredImageSource> Read(IConfigurationSection imageProcessingSection)
    {
        IConfigurationSection[] entries = [.. imageProcessingSection.GetSection("Sources").GetChildren()];
        if (entries.Length == 0)
        {
            throw new InvalidOperationException(
                "ImageProcessing:Sources must configure at least one source, e.g. Sources:0:RoutePrefix = \"/media\" " +
                "with Sources:0:Storage:Mode = \"LocalDisk\" | \"AzureBlob\" | \"HttpProxy\".");
        }

        bool several = entries.Length > 1;
        var problems = new List<string>();
        var sources = new List<ConfiguredImageSource>();
        foreach (IConfigurationSection entry in entries)
        {
            string name = $"ImageProcessing:Sources:{entry.Key}";
            string? routePrefix = ImageSourceMatcher.NormalizeRoutePrefix(entry["RoutePrefix"]);
            if (routePrefix is null)
            {
                problems.Add($"{name} has no RoutePrefix.");
                continue;
            }

            name = $"{name} (\"{routePrefix}\")";
            ConfiguredImageSource? twin = sources.Find(s => s.RoutePrefix == routePrefix);
            if (twin is not null)
            {
                problems.Add($"{name} has the same RoutePrefix as ImageProcessing:Sources:{twin.Key}.");
                continue;
            }

            IConfigurationSection storage = entry.GetSection("Storage");
            if (!TryReadMode(storage["Mode"], several, out ImageStorageMode mode, out string? problem))
            {
                problems.Add($"{name}: {problem}");
                continue;
            }

            problems.AddRange(RequiredSettings(mode, several)
                .Where(setting => string.IsNullOrWhiteSpace(storage[setting]))
                .Select(setting => $"{name}: Storage:{setting} is required for Storage:Mode {mode}."));

            sources.Add(new ConfiguredImageSource(entry.Key, routePrefix, mode, storage));
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException("ImageProcessing:Sources is not valid:" + string.Concat(problems.Select(p => Environment.NewLine + "- " + p)));
        }

        return sources;
    }

    /// <summary>The 2.x settings this service no longer reads, for a warning at start-up.</summary>
    public static IEnumerable<string> SettingsNoLongerRead(IConfigurationSection imageProcessingSection) =>
        SettingsRemovedIn3.Where(key => imageProcessingSection.GetSection(key).Exists()).Select(key => $"ImageProcessing:{key}");

    private static bool TryReadMode(string? value, bool several, out ImageStorageMode mode, out string? problem)
    {
        problem = null;
        if (value is null)
        {
            mode = ImageStorageMode.LocalDisk;
            if (several)
            {
                problem = "Storage:Mode is required when several sources are configured, so each says where its original images come from (LocalDisk, AzureBlob or HttpProxy).";
            }

            return problem is null;
        }

        if (Enum.TryParse(value, ignoreCase: true, out mode))
        {
            return true;
        }

        problem = $"Storage:Mode \"{value}\" is not LocalDisk, AzureBlob or HttpProxy.";
        return false;
    }

    /// <summary>
    /// What a mode cannot work without. A connection is always required; a root or container that has a
    /// default is only required once several sources could otherwise share that default.
    /// </summary>
    private static string[] RequiredSettings(ImageStorageMode mode, bool several) => mode switch
    {
        ImageStorageMode.LocalDisk => several ? ["LocalDisk:RootPath"] : [],
        ImageStorageMode.AzureBlob => several ? ["AzureBlob:ConnectionString", "AzureBlob:ContainerName"] : ["AzureBlob:ConnectionString"],
        ImageStorageMode.HttpProxy => ["HttpProxy:UmbracoBaseUrl"],
        _ => [],
    };
}
