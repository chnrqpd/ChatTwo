using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ChatTwo.Code;
using Dalamud.Plugin.Ipc;

namespace ChatTwo;

internal sealed class TranslationBridge
{
    private readonly Plugin Plugin;
    private ICallGateSubscriber<string, string, string, string?>? TranslateSubscriber;
    private string? CachedIpcName;
    private static readonly HashSet<ChatType> TranslatableChatTypes = new()
    {
        ChatType.Say,
        ChatType.Shout,
        ChatType.Yell,
        ChatType.TellIncoming,
        ChatType.TellOutgoing,
        ChatType.Party,
        ChatType.CrossParty,
        ChatType.Alliance,
        ChatType.FreeCompany,
        ChatType.PvpTeam,
        ChatType.NoviceNetwork,
        ChatType.CrossLinkshell1,
        ChatType.CrossLinkshell2,
        ChatType.CrossLinkshell3,
        ChatType.CrossLinkshell4,
        ChatType.CrossLinkshell5,
        ChatType.CrossLinkshell6,
        ChatType.CrossLinkshell7,
        ChatType.CrossLinkshell8,
        ChatType.Linkshell1,
        ChatType.Linkshell2,
        ChatType.Linkshell3,
        ChatType.Linkshell4,
        ChatType.Linkshell5,
        ChatType.Linkshell6,
        ChatType.Linkshell7,
        ChatType.Linkshell8,
        ChatType.GmSay,
        ChatType.GmShout,
        ChatType.GmYell,
        ChatType.GmTell,
        ChatType.GmParty,
        ChatType.GmFreeCompany,
        ChatType.GmNoviceNetwork,
    };
    private static readonly HttpClient HttpClient = new();

    internal TranslationBridge(Plugin plugin)
    {
        Plugin = plugin;
    }

    private static bool IsLikelyCommand(string text)
    {
        if (string.IsNullOrEmpty(text))
            return true;

        return text[0] is '/' or '!';
    }

    private static bool ShouldSkipLowContent(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
            return true;

        var letterOrDigitCount = 0;
        foreach (var ch in trimmed)
        {
            if (char.IsLetterOrDigit(ch))
                letterOrDigitCount++;
        }

        if (letterOrDigitCount == 0)
            return true;

        return letterOrDigitCount == 1 && trimmed.Length <= 3;
    }

    private static string ToBcp47(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return "en";

        var normalized = language.ToLowerInvariant();
        return normalized switch
        {
            "ja" or "ja-jp" => "ja",
            "de" or "de-de" => "de",
            "fr" or "fr-fr" => "fr",
            "en" or "en-us" => "en",
            _ => "en",
        };
    }

    private string ResolveIncomingLanguage()
    {
        if (!string.IsNullOrWhiteSpace(Plugin.Config.TranslationIncomingLanguage))
            return Plugin.Config.TranslationIncomingLanguage;

        var uiLanguage = Plugin.Interface.UiLanguage;
        return ToBcp47(uiLanguage);
    }

    internal bool IsTranslatableChatType(ChatType type)
    {
        return TranslatableChatTypes.Contains(type);
    }

    private ICallGateSubscriber<string, string, string, string?>? GetSubscriber()
    {
        var ipcName = Plugin.Config.TranslationIpcName?.Trim();
        if (string.IsNullOrEmpty(ipcName))
        {
            Plugin.Log.Debug("IPC name is empty");
            return null;
        }

        if (ipcName == CachedIpcName && TranslateSubscriber != null)
        {
            Plugin.Log.Debug($"Using cached IPC subscriber for '{ipcName}'");
            return TranslateSubscriber;
        }

        try
        {
            Plugin.Log.Debug($"Creating IPC subscriber for '{ipcName}'");
            TranslateSubscriber = Plugin.Interface.GetIpcSubscriber<string, string, string, string?>(ipcName);
            CachedIpcName = ipcName;
            Plugin.Log.Info($"Successfully bound to translation IPC '{ipcName}'");
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, $"Failed to bind translation IPC '{ipcName}'");
            TranslateSubscriber = null;
            CachedIpcName = null;
        }

        return TranslateSubscriber;
    }

    internal string? Translate(string text, string sourceLanguage, string targetLanguage)
    {
        if (!Plugin.Config.TranslationEnabled)
        {
            Plugin.Log.Debug("Translation is disabled");
            return null;
        }

        // Try OpenAI direct translation first if enabled
        if (Plugin.Config.UseOpenAIDirectly)
        {
            Plugin.Log.Debug("Using OpenAI direct translation");
            return TranslateViaOpenAI(text, sourceLanguage, targetLanguage);
        }

        // Fallback to IPC
        var subscriber = GetSubscriber();
        if (subscriber == null)
        {
            Plugin.Log.Warning("No IPC subscriber available for translation");
            return null;
        }

        try
        {
            Plugin.Log.Debug($"Invoking IPC: text='{text}', source='{sourceLanguage}', target='{targetLanguage}'");
            var result = subscriber.InvokeFunc(text, sourceLanguage, targetLanguage);
            Plugin.Log.Debug($"IPC returned: '{result}'");
            return result;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Translation call failed");
            return null;
        }
    }

    private string? TranslateViaOpenAI(string text, string sourceLanguage, string targetLanguage)
    {
        if (string.IsNullOrWhiteSpace(Plugin.Config.OpenAIApiKey))
        {
            Plugin.Log.Warning("OpenAI API key is not configured");
            return null;
        }

        try
        {
            var prompt = $"Translate this FFXIV/MMO chat message to {targetLanguage}. Rules:\n" +
                        "- This is Final Fantasy XIV game chat - use proper gaming terminology\n" +
                        "- 'party/parties' = grupo/grupos (not festa)\n" +
                        "- 'raid' = raid (keep in English or use 'raide')\n" +
                        "- 'dungeon' = masmorra/instância\n" +
                        "- 'boss' = chefe/boss\n" +
                        "- 'tank/healer/DPS' = keep in English\n" +
                        "- 'loot/drop' = loot/drop (gaming terms)\n" +
                        "- Translate naturally, not literally; prefer casual tone (avoid formal/polite register)\n" +
                        "- Prefer gamer/MMO phrasing and slang; keep common abbreviations (strat, cd, aoe, dps, pull, wipe, aggro)\n" +
                        "- Keep contractions when they exist (e.g., don't, can't, isn't) to stay conversational\n" +
                        "- If the text is emoji, punctuation, or a short particle (e.g., '.', '..', '...'), return it unchanged\n" +
                        "- Normalize repeated letters (noooo → não)\n" +
                        "- Preserve tone and emotion; keep playful/banter vibe when present\n" +
                        "- Only output translation, nothing else\n\n" +
                        $"Text: {text}";
            
            var requestBody = new
            {
                model = Plugin.Config.OpenAIModel,
                messages = new[]
                {
                    new { role = "system", content = "You are a translator specialized in MMORPG/FFXIV chat. You understand gaming terminology and translate conversationally while keeping game-specific terms accurate. 'Party' in gaming context means 'grupo' in Portuguese, not 'festa'. Preserve the natural flow and emotion of gaming communication." },
                    new { role = "user", content = prompt }
                },
                temperature = 0.3,
                max_tokens = 500
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, Plugin.Config.OpenAIBaseUrl)
            {
                Content = content
            };
            request.Headers.Add("Authorization", $"Bearer {Plugin.Config.OpenAIApiKey}");

            Plugin.Log.Debug($"Sending OpenAI request for text: '{text}'");
            var response = HttpClient.SendAsync(request).Result;
            var responseContent = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
            {
                Plugin.Log.Warning($"OpenAI API error: {response.StatusCode} - {responseContent}");
                return null;
            }

            var jsonDoc = JsonDocument.Parse(responseContent);
            var translatedText = jsonDoc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString()?.Trim();

            Plugin.Log.Info($"OpenAI translation: '{text}' -> '{translatedText}'");
            return translatedText;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Failed to translate via OpenAI");
            return null;
        }
    }

    internal void TranslateIncoming(Message message)
    {
        if (!IsTranslatableChatType(message.Code.Type))
            return;

        if (!Plugin.Config.TranslationEnabled || !Plugin.Config.TranslateIncoming)
        {
            Plugin.Log.Debug($"Translation skipped - Enabled: {Plugin.Config.TranslationEnabled}, TranslateIncoming: {Plugin.Config.TranslateIncoming}");
            return;
        }

        var text = message.ContentSource.TextValue;
        if (string.IsNullOrWhiteSpace(text))
        {
            Plugin.Log.Debug("Translation skipped - empty text");
            return;
        }

        if (ShouldSkipLowContent(text))
        {
            Plugin.Log.Debug("Translation skipped - low content/emoji-like input");
            return;
        }

        var target = ResolveIncomingLanguage();
        Plugin.Log.Debug($"Starting translation for text: '{text}' to language: '{target}'");

        Task.Run(() =>
        {
            try
            {
                Plugin.Log.Debug($"Calling Translate() for text: '{text}'");
                var translated = Translate(text, "auto", target);
                if (string.IsNullOrWhiteSpace(translated))
                {
                    Plugin.Log.Debug("Translation returned empty result");
                    return;
                }

                Plugin.Log.Info($"Translation successful: '{text}' -> '{translated}'");
                message.SetTranslation(translated, target);
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning(ex, "Translation failed for incoming message");
            }
        });
    }

    internal async Task<string> TranslateOutgoingAsync(string text, ChatType chatType)
    {
        if (!IsTranslatableChatType(chatType))
            return text;

        if (!Plugin.Config.TranslationEnabled || !Plugin.Config.TranslateOutgoing)
        {
            Plugin.Log.Info("Translation disabled in config");
            return text;
        }

        if (IsLikelyCommand(text))
        {
            Plugin.Log.Info($"Skipping translation for command: {text}");
            return text;
        }

        if (ShouldSkipLowContent(text))
        {
            Plugin.Log.Debug("Skipping translation for low-content/emoji-like text");
            return text;
        }

        Plugin.Log.Info($"Starting translation for: '{text}'");

        var target = string.IsNullOrWhiteSpace(Plugin.Config.TranslationOutgoingLanguage)
            ? "en"
            : Plugin.Config.TranslationOutgoingLanguage;

        try
        {
            var translated = await Task.Run(() => Translate(text, "auto", target)).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(translated))
            {
                Plugin.Log.Info($"Outgoing translated: '{text}' -> '{translated}'");
                return translated;
            }

            Plugin.Log.Info("Translation returned empty result, using original text");
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Translation failed, sending original");
        }

        return text;
    }
}
