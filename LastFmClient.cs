using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WinScrobb;

public class LastFmException(string message) : Exception(message);

public class LastFmClient(string apiKey, string apiSecret) : IDisposable
{
    private const string ApiUrl = "https://ws.audioscrobbler.com/2.0/";
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public string SessionKey { get; set; } = "";

    // -------------------------------------------------------------------------
    // Signing
    // -------------------------------------------------------------------------

    private string Sign(Dictionary<string, string> @params)
    {
        var body = new StringBuilder();
        foreach (var kv in @params.OrderBy(x => x.Key))
            body.Append(kv.Key).Append(kv.Value);
        body.Append(apiSecret);

        var hash = MD5.HashData(Encoding.UTF8.GetBytes(body.ToString()));
        return Convert.ToHexString(hash).ToLower();
    }

    private async Task<JsonDocument> CallAsync(
        string method,
        Dictionary<string, string> @params,
        bool post = false)
    {
        @params["api_key"] = apiKey;
        @params["method"] = method;
        @params["api_sig"] = Sign(@params);
        @params["format"] = "json";

        HttpResponseMessage resp;
        if (post)
        {
            var content = new FormUrlEncodedContent(@params);
            resp = await _http.PostAsync(ApiUrl, content);
        }
        else
        {
            var qs = string.Join("&", @params.Select(kv =>
                $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
            resp = await _http.GetAsync($"{ApiUrl}?{qs}");
        }

        resp.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());

        if (doc.RootElement.TryGetProperty("error", out var err))
            throw new LastFmException(
                $"Last.fm error {err.GetInt32()}: " +
                doc.RootElement.GetProperty("message").GetString());

        return doc;
    }

    // -------------------------------------------------------------------------
    // Auth
    // -------------------------------------------------------------------------

    public async Task<string> GetTokenAsync()
    {
        using var doc = await CallAsync("auth.getToken", []);
        return doc.RootElement.GetProperty("token").GetString()!;
    }

    public static string AuthUrl(string apiKey, string token) =>
        $"https://www.last.fm/api/auth/?api_key={apiKey}&token={token}";

    public async Task<(string sessionKey, string username)> GetSessionAsync(string token)
    {
        using var doc = await CallAsync("auth.getSession", new() { ["token"] = token });
        var session = doc.RootElement.GetProperty("session");
        return (
            session.GetProperty("key").GetString()!,
            session.GetProperty("name").GetString()!
        );
    }

    // -------------------------------------------------------------------------
    // Scrobbling
    // -------------------------------------------------------------------------

    public async Task UpdateNowPlayingAsync(string artist, string track, string album, int durationSeconds)
    {
        var p = new Dictionary<string, string>
        {
            ["artist"] = artist,
            ["track"] = track,
            ["sk"] = SessionKey,
        };
        if (!string.IsNullOrEmpty(album)) p["album"] = album;
        if (durationSeconds > 0) p["duration"] = durationSeconds.ToString();

        using var _ = await CallAsync("track.updateNowPlaying", p, post: true);
    }

    public async Task ScrobbleAsync(
        string artist,
        string track,
        string album,
        long timestamp,
        int durationSeconds)
    {
        var p = new Dictionary<string, string>
        {
            ["artist"] = artist,
            ["track"] = track,
            ["timestamp"] = timestamp.ToString(),
            ["sk"] = SessionKey,
        };
        if (!string.IsNullOrEmpty(album)) p["album"] = album;
        if (durationSeconds > 0) p["duration"] = durationSeconds.ToString();

        using var _ = await CallAsync("track.scrobble", p, post: true);
    }

    // -------------------------------------------------------------------------
    // Love / Unlove
    // -------------------------------------------------------------------------

    public async Task LoveAsync(string artist, string track)
    {
        using var _ = await CallAsync("track.love",
            new() { ["artist"] = artist, ["track"] = track, ["sk"] = SessionKey },
            post: true);
    }

    public async Task UnloveAsync(string artist, string track)
    {
        using var _ = await CallAsync("track.unlove",
            new() { ["artist"] = artist, ["track"] = track, ["sk"] = SessionKey },
            post: true);
    }

    public void Dispose() => _http.Dispose();
}
