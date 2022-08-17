using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Order;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FacegenBaseline
{
    public class Program
    {
        static Lazy<Settings> _settings = null!;
        static public Settings settings => _settings.Value;

        private static IPatcherState<ISkyrimMod, ISkyrimModGetter>? _state;
        public static IPatcherState<ISkyrimMod, ISkyrimModGetter> PatcherState
        {
            get => _state!;
        }
        public static async Task<int> Main(string[] args)
        {
            return await SynthesisPipeline.Instance
                .SetTypicalOpen(GameRelease.SkyrimSE, "FacegenBaseline.esp")
                .SetAutogeneratedSettings("Settings", "settings.json", out _settings)
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .Run(args);
        }

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            uint alreadyWins = 0;
            uint hasBetterFacegen = 0;
            uint useBaseline = 0;
            _state = state;

            // no mod names in list
            if (settings.BaselineMods.Count == 0) throw new ArgumentException("Baseline plugins found in baseline mods list");

            if (settings.BaselineMods.Count > 1) settings.BaselineMods.Reverse(); // reverse order to parse first with more priority


            bool checkIfExcluded = settings.ExcludeNPCByKeywords.Count > 0;
            if (!checkIfExcluded) Console.WriteLine($"Excluded list is empty");

            var parsedNPCs = new HashSet<FormKey>();
            int npcRecordsCount = 0;
            foreach (var baselineModName in settings.BaselineMods)
            {
                // skip invalid mods, missing or null
                ModKey baselineModKey = ModKey.FromNameAndExtension(baselineModName);
                if (!PatcherState.LoadOrder.TryGetValue(baselineModKey, out IModListing<ISkyrimModGetter>? baselineMod) || baselineMod == null || baselineMod.Mod == null)
                {
                    Console.WriteLine($"{baselineModName} not found in Load Order");
                }

                var baselineNPCs = baselineMod!.Mod!.Npcs;
                npcRecordsCount += baselineNPCs.Count;

                foreach (var baselineNPC in baselineNPCs)
                {
                    if (baselineNPC == null) continue;
                    if (parsedNPCs.Contains(baselineNPC.FormKey)) continue; // skip already parsed
                    if (checkIfExcluded && !string.IsNullOrWhiteSpace(baselineNPC.EditorID) && settings.ExcludeNPCByKeywords
                        .Any(keyword => 
                        !string.IsNullOrWhiteSpace(keyword)
                        && baselineNPC.EditorID.Contains(keyword))) continue; // skip because edid contains keyword

                    // we need to introspect the provenance of the record
                    var contexts = state.LinkCache.ResolveAllContexts<INpc, INpcGetter>(baselineNPC.FormKey).ToList();
                    var currentWinner = contexts[0];
                    if (currentWinner.ModKey == baselineModKey)
                    {
                        Console.WriteLine("Baseline is winning override for {0}/{1:X8}", baselineNPC.Name, baselineNPC.FormKey.ID);
                        ++alreadyWins;
                        continue;
                    }

                    parsedNPCs.Add(baselineNPC.FormKey);

                    // Compare winning override Head Parts with master - if this record is already overriding the appearance, we let it win
                    var master = contexts.Last();
                    var masterHDPTs = master.Record.HeadParts.Select(s => s.TryResolve<IHeadPartGetter>(state.LinkCache)).ToHashSet();
                    var winnerHDPTs = currentWinner.Record.HeadParts.Select(s => s.TryResolve<IHeadPartGetter>(state.LinkCache)).ToHashSet();
                    if (masterHDPTs.SetEquals(winnerHDPTs))
                    {
                        Console.WriteLine("Baseline appearance used for {0}/{1:X8}", baselineNPC.Name, baselineNPC.FormKey.ID);
                        UseBaselineAppearance(baselineNPC, currentWinner.Record);
                        ++useBaseline;
                    }
                    else
                    {
                        Console.WriteLine("Appearance for {0}/{1:X8} provided by {2}", baselineNPC.Name, baselineNPC.FormKey.ID, currentWinner.ModKey.FileName);
                        ++hasBetterFacegen;
                    }
                }
            }

            Console.WriteLine("NPC Records {0} : Baseline already the winner for {1}, used as new winner for {2}, lost to better facegen for {3}",
                npcRecordsCount, alreadyWins, useBaseline, hasBetterFacegen);
        }

        private static void UseBaselineAppearance(INpcGetter baselineNPC, INpcGetter currentWinner)
        {
            // forward appearance, with acknowledgement to https://github.com/Piranha91/NPC-Plugin-Chooser
            var synthesisNpc = PatcherState.PatchMod.Npcs.GetOrAddAsOverride(currentWinner);

            synthesisNpc.FaceMorph = baselineNPC.FaceMorph?.DeepCopy();

            synthesisNpc.FaceParts = baselineNPC.FaceParts?.DeepCopy();

            synthesisNpc.FarAwayModel.SetTo(baselineNPC.FarAwayModel);

            synthesisNpc.HairColor.SetTo(baselineNPC.HairColor);

            synthesisNpc.HeadParts.Clear();
            synthesisNpc.HeadParts.AddRange(baselineNPC.HeadParts);

            synthesisNpc.HeadTexture.SetTo(baselineNPC.HeadTexture);

            synthesisNpc.Height = baselineNPC.Height;

            synthesisNpc.Race.SetTo(baselineNPC.Race);

            synthesisNpc.TextureLighting = baselineNPC.TextureLighting;

            synthesisNpc.TintLayers.Clear();
            synthesisNpc.TintLayers.AddRange(baselineNPC.TintLayers.Select(a => a.DeepCopy()));

            synthesisNpc.Weight = baselineNPC.Weight;

            synthesisNpc.WornArmor.SetTo(baselineNPC.WornArmor);

            // import protected flag if set to true
            if (settings.GetProtectedFlag && baselineNPC.Configuration.Flags.HasFlag(NpcConfiguration.Flag.Protected))
                synthesisNpc.Configuration.Flags |= NpcConfiguration.Flag.Protected;
        }
    }
}
