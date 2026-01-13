using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Dalamud.Plugin.Ipc;

namespace ChatTwo.Ipc;

internal sealed class TranslationIpcProvider : IDisposable
{
    private const string DefaultIpcName = "ChatTwo.TranslateText";
    private const string DefaultEndpoint = "https://api.openai.com/v1/chat/completions";
    private const string ModelName = "gpt-5-nano";
    private const int CacheLimit = 128;

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(6),
    };

    private readonly Plugin Plugin;
    private readonly ICallGateProvider<string, string, string, string?> Provider;
    private readonly ConcurrentDictionary<string, string> Cache = new();

    internal TranslationIpcProvider(Plugin plugin)
    {
        Plugin = plugin;

        var ipcName = plugin.Config.TranslationIpcName?.Trim();
        if (string.IsNullOrEmpty(ipcName))
            ipcName = DefaultIpcName;

        Provider = Plugin.Interface.GetIpcProvider<string, string, string, string?>(ipcName);
        Provider.RegisterFunc(Translate);
    }

    public void Dispose()
        => Provider.UnregisterFunc();

    private string CacheKey(string text, string targetLanguage)
        => $"{targetLanguage}\u001f{text}";

    private string? Translate(string text, string sourceLanguage, string targetLanguage)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var normalizedTarget = string.IsNullOrWhiteSpace(targetLanguage) ? "en" : targetLanguage;

        var cacheKey = CacheKey(text, normalizedTarget);
        if (Cache.TryGetValue(cacheKey, out var cached))
            return cached;

        try
        {
            var translated = TranslateAsync(text, sourceLanguage, normalizedTarget).GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(translated))
                return null;

            var coherent = EnsureCoherence(text, translated);
            Cache[cacheKey] = coherent;
            TrimCache();
            return coherent;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Translation IPC failed");
            return null;
        }
    }

    private async Task<string?> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        using var request = new HttpRequestMessage(HttpMethod.Post, DefaultEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");

        var body = new
        {
            model = ModelName,
            temperature = 0.2,
            max_tokens = Math.Max(64, Math.Min(400, text.Length * 2)),
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = $"You translate messages faithfully into {targetLanguage}. Preserve meaning and intent, but adjust phrasing when a literal translation would be incoherent. Reply with translation only."
                },
                new
                {
                    role = "user",
                    content = $"Source language: {sourceLanguage}\nTarget language: {targetLanguage}\nText: {text}"
                }
            }
        };

        var json = JsonSerializer.Serialize(body);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await Http.SendAsync(request).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        var parsed = await JsonSerializer.DeserializeAsync<ChatCompletionResponse>(stream).ConfigureAwait(false);
        var content = parsed?.Choices?.FirstOrDefault()?.Message?.Content;
        return string.IsNullOrWhiteSpace(content) ? null : content.Trim();
    }

    private static string EnsureCoherence(string original, string translated)
    {
        if (string.IsNullOrWhiteSpace(translated))
            return original;

        var alphaCount = translated.Count(char.IsLetter);
        if (alphaCount < 2 || translated.Length < Math.Min(3, original.Length / 4))
            return original;

        return translated;
    }

    private void TrimCache()
    {
        if (Cache.Count <= CacheLimit)
            return;

        foreach (var key in Cache.Keys.Take(Cache.Count - CacheLimit))
            Cache.TryRemove(key, out _);
    }

    private sealed class ChatCompletionResponse
    {
        [JsonPropertyName("choices")] public Choice[]? Choices { get; init; }
    }

    private sealed class Choice
    {
        [JsonPropertyName("message")] public ChatMessage? Message { get; init; }
    }

    private sealed class ChatMessage
    {
        [JsonPropertyName("content")] public string? Content { get; init; }
    }
}
