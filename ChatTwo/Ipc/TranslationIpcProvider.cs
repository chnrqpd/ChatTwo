using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dalamud.Plugin.Ipc;

namespace ChatTwo.Ipc;

internal sealed class TranslationIpcProvider : IDisposable
{
    private const string DefaultIpcName = "ChatTwo.TranslateText";
    private const string DefaultEndpoint = "https://api.openai.com/v1/chat/completions";
    private const string ModelName = "gpt-5-nano";
    private const int CacheLimit = 128;
    private const int MinimumLettersForCoherence = 2;
    private const int MinimumLengthForCoherence = 3;
    private const int MinimumSourceLengthRatioDivisor = 4;

    private readonly Plugin Plugin;
    private readonly ICallGateProvider<string, string, string, string?> Provider;
    private readonly HttpClient Http;
    private readonly Dictionary<string, string> Cache = new();
    private readonly Queue<string> CacheOrder = new();
    private readonly object CacheLock = new();
    private string? ApiKey;

    internal TranslationIpcProvider(Plugin plugin)
    {
        Plugin = plugin;

        var ipcName = Plugin.Config.TranslationIpcName?.Trim();
        if (string.IsNullOrEmpty(ipcName))
            ipcName = DefaultIpcName;

        Http = new HttpClient
        {
            // Allow a bit more time to avoid transient gateway slowness.
            Timeout = TimeSpan.FromSeconds(15),
        };

        ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        Provider = Plugin.Interface.GetIpcProvider<string, string, string, string?>(ipcName);
        Provider.RegisterFunc(Translate);
    }

    public void Dispose()
    {
        Provider.UnregisterFunc();
        Http.Dispose();
        ApiKey = null;
    }

    private string CacheKey(string text, string targetLanguage)
        => $"{targetLanguage}\u001f{text}";

    private string? Translate(string text, string sourceLanguage, string targetLanguage)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var normalizedTarget = string.IsNullOrWhiteSpace(targetLanguage) ? "en" : targetLanguage;

        var cacheKey = CacheKey(text, normalizedTarget);
        lock (CacheLock)
        {
            if (Cache.TryGetValue(cacheKey, out var cached))
                return cached;
        }

        try
        {
            var translated = TranslateBlocking(text, sourceLanguage, normalizedTarget);
            if (string.IsNullOrWhiteSpace(translated))
                return null;

            var coherent = EnsureCoherence(text, translated);
            lock (CacheLock)
            {
                var isNew = !Cache.ContainsKey(cacheKey);
                Cache[cacheKey] = coherent;
                if (isNew)
                {
                    CacheOrder.Enqueue(cacheKey);
                    TrimCache();
                }
            }
            return coherent;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Translation IPC failed");
            return null;
        }
    }

    private string? TranslateBlocking(string text, string sourceLanguage, string targetLanguage)
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
            return null;

        using var request = new HttpRequestMessage(HttpMethod.Post, DefaultEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
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

        // IPC contract is synchronous; use a bounded blocking call with the short client timeout.
        using var response = Http.Send(request, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
            return null;

        using var stream = response.Content.ReadAsStream();
        var parsed = JsonSerializer.Deserialize<ChatCompletionResponse>(stream);
        var content = parsed?.Choices?.FirstOrDefault()?.Message?.Content;
        return string.IsNullOrWhiteSpace(content) ? null : content.Trim();
    }

    private static string EnsureCoherence(string original, string translated)
    {
        if (string.IsNullOrWhiteSpace(translated))
            return original;

        var alphaCount = translated.Count(char.IsLetter);
        var minLength = Math.Min(MinimumLengthForCoherence, original.Length / MinimumSourceLengthRatioDivisor);
        if (alphaCount < MinimumLettersForCoherence || translated.Length < minLength)
            return original;

        return translated;
    }

    private void TrimCache()
    {
        lock (CacheLock)
        {
            while (Cache.Count > CacheLimit && CacheOrder.Count > 0)
            {
                var oldest = CacheOrder.Dequeue();
                Cache.Remove(oldest);
            }
        }
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
