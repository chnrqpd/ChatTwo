using System.Text.RegularExpressions;
using ChatTwo.Resources;
using ChatTwo.Util;
using Dalamud.Bindings.ImGui;

namespace ChatTwo.Ui.SettingsTabs;

internal sealed class Miscellaneous(Configuration mutable) : ISettingsTab
{
    private const int LanguageTagMaxLength = 32;
    private static readonly Regex LanguageTagRegex = new("^[A-Za-z]{2,3}(?:-[A-Za-z0-9]{2,8})*$", RegexOptions.Compiled);

    private Configuration Mutable { get; } = mutable;
    public string Name => Language.Options_Miscellaneous_Tab + "###tabs-miscellaneous";

    public void Draw(bool changed)
    {
        using (var combo = ImGuiUtil.BeginComboVertical(Language.Options_Language_Name, Mutable.LanguageOverride.Name()))
        {
            if (combo.Success)
            {
                foreach (var language in Enum.GetValues<LanguageOverride>())
                    if (ImGui.Selectable(language.Name()))
                        Mutable.LanguageOverride = language;
            }
        }

        ImGuiUtil.HelpText(string.Format(Language.Options_Language_Description, Plugin.PluginName));
        ImGui.Spacing();

        using (var combo = ImGuiUtil.BeginComboVertical(Language.Options_CommandHelpSide_Name, Mutable.CommandHelpSide.Name()))
        {
            if (combo.Success)
            {
                foreach (var side in Enum.GetValues<CommandHelpSide>())
                    if (ImGui.Selectable(side.Name(), Mutable.CommandHelpSide == side))
                        Mutable.CommandHelpSide = side;
            }
        }

        ImGuiUtil.HelpText(string.Format(Language.Options_CommandHelpSide_Description, Plugin.PluginName));
        ImGui.Spacing();

        using (var combo = ImGuiUtil.BeginComboVertical(Language.Options_KeybindMode_Name, Mutable.KeybindMode.Name()))
        {
            if (combo.Success)
            {
                foreach (var mode in Enum.GetValues<KeybindMode>())
                {
                    if (ImGui.Selectable(mode.Name(), Mutable.KeybindMode == mode))
                        Mutable.KeybindMode = mode;

                    if (ImGui.IsItemHovered())
                        ImGuiUtil.Tooltip(mode.Tooltip() ?? "");
                }
            }
        }

        ImGuiUtil.HelpText(string.Format(Language.Options_KeybindMode_Description, Plugin.PluginName));
        ImGui.Spacing();

        ImGui.Checkbox(Language.Options_SortAutoTranslate_Name, ref Mutable.SortAutoTranslate);
        ImGuiUtil.HelpText(Language.Options_SortAutoTranslate_Description);
        ImGui.Spacing();

        ImGui.Separator();
        ImGui.TextUnformatted("AI Translation");
        ImGuiUtil.HelpText("Translate chat messages using OpenAI API or external plugin via IPC.");

        ImGui.Checkbox("Enable translation features", ref Mutable.TranslationEnabled);
        
        ImGui.Spacing();
        ImGui.Checkbox("Use OpenAI API directly", ref Mutable.UseOpenAIDirectly);
        ImGuiUtil.HelpText("When enabled, ChatTwo will call OpenAI API directly. Otherwise, it will use IPC.");
        
        if (Mutable.UseOpenAIDirectly)
        {
            ImGui.Indent();
            ImGui.InputText("OpenAI API Key", ref Mutable.OpenAIApiKey, 256, ImGuiInputTextFlags.Password);
            ImGuiUtil.HelpText("Your OpenAI API key (sk-...)");
            
            ImGui.InputText("OpenAI Model", ref Mutable.OpenAIModel, 64);
            ImGuiUtil.HelpText("Model to use (e.g., gpt-4o-mini, gpt-4, gpt-3.5-turbo)");
            
            ImGui.InputText("OpenAI API URL", ref Mutable.OpenAIBaseUrl, 256);
            ImGuiUtil.HelpText("API endpoint URL (default: https://api.openai.com/v1/chat/completions)");
            ImGui.Unindent();
            ImGui.Spacing();
        }
        else
        {
            ImGui.Indent();
            ImGui.InputText("Translation IPC channel name", ref Mutable.TranslationIpcName, 64);
            ImGuiUtil.HelpText("IPC must accept (string text, string sourceLanguage, string targetLanguage) and return translated text.");
            ImGui.Unindent();
            ImGui.Spacing();
        }
        
        ImGui.Checkbox("Translate incoming chat to target language", ref Mutable.TranslateIncoming);
        var incomingValid = LooksLikeLanguageTag(Mutable.TranslationIncomingLanguage);
        ImGui.InputText("Incoming target language (e.g. pt-BR)", ref Mutable.TranslationIncomingLanguage, LanguageTagMaxLength);
        if (!incomingValid)
            ImGuiUtil.HelpText("Use a BCP-47 language tag such as en or pt-BR.");

        ImGui.Checkbox("Translate outgoing chat to target language", ref Mutable.TranslateOutgoing);
        var outgoingValid = LooksLikeLanguageTag(Mutable.TranslationOutgoingLanguage);
        ImGui.InputText("Outgoing target language (e.g. en)", ref Mutable.TranslationOutgoingLanguage, LanguageTagMaxLength);
        if (!outgoingValid)
            ImGuiUtil.HelpText("Use a BCP-47 language tag such as en or pt-BR.");
    }

    private static bool LooksLikeLanguageTag(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (value.Length > LanguageTagMaxLength)
            return false;

        return LanguageTagRegex.IsMatch(value);
    }
}
