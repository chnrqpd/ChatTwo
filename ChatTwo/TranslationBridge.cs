using System;
using System.Threading.Tasks;
using Dalamud.Game;
using Dalamud.Plugin.Ipc;

namespace ChatTwo;

internal sealed class TranslationBridge
{
    private readonly Plugin Plugin;
    private ICallGateSubscriber<string, string, string, string?>? TranslateSubscriber;
    private string? CachedIpcName;
    private static readonly TimeSpan OutgoingTimeout = TimeSpan.FromSeconds(2);

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

    private static string ToBcp47(ClientLanguage language) => language switch
    {
        ClientLanguage.Japanese => "ja",
        ClientLanguage.German => "de",
        ClientLanguage.French => "fr",
        ClientLanguage.ChineseSimplified => "zh-Hans",
        ClientLanguage.ChineseTraditional => "zh-Hant",
        ClientLanguage.Korean => "ko",
        _ => "en",
    };

    private string ResolveIncomingLanguage()
    {
        if (!string.IsNullOrWhiteSpace(Plugin.Config.TranslationIncomingLanguage))
            return Plugin.Config.TranslationIncomingLanguage;

        var uiLanguage = Plugin.Interface.UiLanguage;
        return ToBcp47(uiLanguage);
    }

    private ICallGateSubscriber<string, string, string, string?>? GetSubscriber()
    {
        var ipcName = Plugin.Config.TranslationIpcName?.Trim();
        if (string.IsNullOrEmpty(ipcName))
            return null;

        if (ipcName == CachedIpcName && TranslateSubscriber != null)
            return TranslateSubscriber;

        try
        {
            TranslateSubscriber = Plugin.Interface.GetIpcSubscriber<string, string, string, string?>(ipcName);
            CachedIpcName = ipcName;
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
            return null;

        var subscriber = GetSubscriber();
        if (subscriber == null)
            return null;

        try
        {
            return subscriber.InvokeFunc(text, sourceLanguage, targetLanguage);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Translation call failed");
            return null;
        }
    }

    internal void TranslateIncoming(Message message)
    {
        if (!Plugin.Config.TranslationEnabled || !Plugin.Config.TranslateIncoming)
            return;

        var text = message.ContentSource.TextValue;
        if (string.IsNullOrWhiteSpace(text))
            return;

        var target = ResolveIncomingLanguage();

        Task.Run(() =>
        {
            try
            {
                var translated = Translate(text, "auto", target);
                if (string.IsNullOrWhiteSpace(translated))
                    return;

                message.SetTranslation(translated, target);
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning(ex, "Translation failed for incoming message");
            }
        });
    }

    internal string TranslateOutgoing(string text)
    {
        if (!Plugin.Config.TranslationEnabled || !Plugin.Config.TranslateOutgoing)
            return text;

        if (IsLikelyCommand(text))
            return text;

        var target = string.IsNullOrWhiteSpace(Plugin.Config.TranslationOutgoingLanguage)
            ? "en"
            : Plugin.Config.TranslationOutgoingLanguage;

        var translationTask = Task.Run(() => Translate(text, "auto", target));
        try
        {
            if (!translationTask.Wait(OutgoingTimeout))
            {
                Plugin.Log.Warning("Translation timed out for outgoing text");
                return text;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Translation failed for outgoing text");
            return text;
        }

        var translated = translationTask.Result;
        return string.IsNullOrWhiteSpace(translated) ? text : translated;
    }
}
