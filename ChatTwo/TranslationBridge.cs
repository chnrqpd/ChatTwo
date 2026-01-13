using System;
using Dalamud.Plugin.Ipc;

namespace ChatTwo;

internal sealed class TranslationBridge
{
    private readonly Plugin Plugin;
    private ICallGateSubscriber<string, string, string, string?>? TranslateSubscriber;
    private string? CachedIpcName;

    internal TranslationBridge(Plugin plugin)
    {
        Plugin = plugin;
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

        var target = string.IsNullOrWhiteSpace(Plugin.Config.TranslationIncomingLanguage)
            ? Plugin.Interface.UiLanguage
            : Plugin.Config.TranslationIncomingLanguage;

        var translated = Translate(text, "auto", target);
        if (string.IsNullOrWhiteSpace(translated) || translated.Equals(text, StringComparison.OrdinalIgnoreCase))
            return;

        message.SetTranslation(translated, target);
    }

    internal string TranslateOutgoing(string text)
    {
        if (!Plugin.Config.TranslationEnabled || !Plugin.Config.TranslateOutgoing)
            return text;

        if (text.StartsWith('/'))
            return text;

        var target = string.IsNullOrWhiteSpace(Plugin.Config.TranslationOutgoingLanguage)
            ? "en"
            : Plugin.Config.TranslationOutgoingLanguage;

        var translated = Translate(text, "auto", target);
        return string.IsNullOrWhiteSpace(translated) ? text : translated;
    }
}
