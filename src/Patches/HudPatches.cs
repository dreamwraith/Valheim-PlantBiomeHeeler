using HarmonyLib;
using PlantBiomeHeeler.Core.UI;

namespace PlantBiomeHeeler.Patches
{
    [HarmonyPatch(typeof(Hud))]
    internal static class HudPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(Hud.Awake))]
        private static void AwakePostfix(Hud __instance) =>
            BiomeHudManager.AttachToHud(__instance);
    }
}
