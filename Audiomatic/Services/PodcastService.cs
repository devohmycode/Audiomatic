using System.Net.Http;
using System.Text.Json;
using System.Xml.Linq;

namespace Audiomatic.Services;

public record PodcastInfo(string Name, string Author, string FeedUrl, string ArtworkUrl);

public record PodcastEpisode(string Title, string Published, string Duration, string AudioUrl, string Description);

public static class PodcastService
{
    private static readonly HttpClient Http = new();
    private static readonly string PodcastsPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Audiomatic", "podcasts.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Search podcasts via iTunes Search API.
    /// </summary>
    public static async Task<List<PodcastInfo>> SearchAsync(string query, int limit = 20)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];

        var url = $"https://itunes.apple.com/search?term={Uri.EscapeDataString(query)}&media=podcast&limit={limit}";
        var json = await Http.GetStringAsync(url);
        var doc = JsonDocument.Parse(json);

        var results = new List<PodcastInfo>();
        foreach (var item in doc.RootElement.GetProperty("results").EnumerateArray())
        {
            var name = item.TryGetProperty("collectionName", out var n) ? n.GetString() ?? "" : "";
            var author = item.TryGetProperty("artistName", out var a) ? a.GetString() ?? "" : "";
            var feedUrl = item.TryGetProperty("feedUrl", out var f) ? f.GetString() ?? "" : "";
            var artwork = item.TryGetProperty("artworkUrl100", out var art) ? art.GetString() ?? "" : "";

            if (!string.IsNullOrEmpty(feedUrl))
                results.Add(new PodcastInfo(name, author, feedUrl, artwork));
        }
        return results;
    }

    /// <summary>
    /// Fetch episodes from a podcast RSS feed.
    /// </summary>
    public static async Task<List<PodcastEpisode>> FetchEpisodesAsync(string feedUrl, int limit = 50)
    {
        var xml = await Http.GetStringAsync(feedUrl);
        var doc = XDocument.Parse(xml);
        XNamespace itunes = "http://www.itunes.com/dtds/podcast-1.0.dtd";

        var episodes = new List<PodcastEpisode>();
        var items = doc.Descendants("item");

        foreach (var item in items.Take(limit))
        {
            var title = item.Element("title")?.Value ?? "";
            var pubDate = item.Element("pubDate")?.Value ?? "";
            var duration = item.Element(itunes + "duration")?.Value ?? "";
            var enclosure = item.Element("enclosure");
            var audioUrl = enclosure?.Attribute("url")?.Value ?? "";
            var description = item.Element("description")?.Value ?? "";

            // Clean up published date
            if (DateTime.TryParse(pubDate, out var dt))
                pubDate = dt.ToString("dd MMM yyyy");

            if (!string.IsNullOrEmpty(audioUrl))
                episodes.Add(new PodcastEpisode(title, pubDate, duration, audioUrl, description));
        }
        return episodes;
    }

    // -- Read/unread episode tracking --

    private static readonly string ReadPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Audiomatic", "podcast_read.json");

    public static HashSet<string> LoadReadEpisodes()
    {
        try
        {
            if (File.Exists(ReadPath))
            {
                var json = File.ReadAllText(ReadPath);
                var list = JsonSerializer.Deserialize<List<string>>(json, JsonOpts);
                return list != null ? new HashSet<string>(list) : [];
            }
        }
        catch { }
        return [];
    }

    public static void SaveReadEpisodes(HashSet<string> readUrls)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReadPath)!);
            var json = JsonSerializer.Serialize(readUrls.ToList(), JsonOpts);
            File.WriteAllText(ReadPath, json);
        }
        catch { }
    }

    /// <summary>
    /// Load saved podcast subscriptions.
    /// </summary>
    public static List<PodcastInfo> LoadSubscriptions()
    {
        try
        {
            if (File.Exists(PodcastsPath))
            {
                var json = File.ReadAllText(PodcastsPath);
                return JsonSerializer.Deserialize<List<PodcastInfo>>(json, JsonOpts) ?? [];
            }
        }
        catch { }
        return [];
    }

    /// <summary>
    /// Save podcast subscriptions.
    /// </summary>
    public static void SaveSubscriptions(List<PodcastInfo> podcasts)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PodcastsPath)!);
            var json = JsonSerializer.Serialize(podcasts, JsonOpts);
            File.WriteAllText(PodcastsPath, json);
        }
        catch { }
    }
}
