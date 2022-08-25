using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Synthesis.Settings;
using System.Collections.Generic;

namespace FacegenBaseline
{
    public class Settings
    {
        [SynthesisTooltip("Mods list of changing npc appearance mods")]
        public HashSet<ModKey> BaselineMods { get; set; } = new();

        [SynthesisTooltip("List of string keywords. All npc with editorid including the keywords will be skipped")]
        public List<string> ExcludeNPCEditorId { get; set; } = new List<string>();

        [SynthesisTooltip("Face mod can also mark changed npcs with protected flag. Enable if you need to import it also in patch")]
        public bool GetProtectedFlag { get; set; } = true;
    }
}
