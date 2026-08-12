using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

public static class HotKeyChordHelper
{
    public static bool SameChord(HotKeyBinding left, HotKeyBinding right) =>
        left.Modifiers == right.Modifiers
        && string.Equals(left.Key, right.Key, StringComparison.OrdinalIgnoreCase);

    public static bool ConflictsWithHotKeys(VoiceSettings voice, HotKeySettings hotKeys)
    {
        if (!hotKeys.Enabled)
        {
            return false;
        }

        var pushToTalk = voice.ToPushToTalkBinding();
        return hotKeys.Bindings.Any(binding => SameChord(binding, pushToTalk));
    }

    public static bool TryFindDuplicate(IReadOnlyList<HotKeyBinding> bindings, out HotKeyBinding? duplicate)
    {
        for (var i = 0; i < bindings.Count; i++)
        {
            for (var j = i + 1; j < bindings.Count; j++)
            {
                if (SameChord(bindings[i], bindings[j]))
                {
                    duplicate = bindings[j];
                    return true;
                }
            }
        }

        duplicate = null;
        return false;
    }
}
