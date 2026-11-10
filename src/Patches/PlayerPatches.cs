using HarmonyLib;
using PlantBiomeHeeler.Core.State;
using PlantBiomeHeeler.Core.Visuals;

namespace PlantBiomeHeeler.Patches
{
    [HarmonyPatch(typeof(Player))]
    internal static class PlayerPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(Player.SetLocalPlayer))]
        private static void SetLocalPlayerPostfix(Player __instance) =>
            BiomeOverlayRenderer.EnsureRenderer(__instance);

        [HarmonyPostfix]
        [HarmonyPatch(nameof(Player.Update))]
        private static void UpdatePostfix(Player __instance)
        {
            if (__instance == Player.m_localPlayer)
            {
                OverlayVisibility.Update(__instance);
            }
        }

        // prefix because Player.OnDestroy clears m_localPlayer itself. runs on logout and on respawn after death
        [HarmonyPrefix]
        [HarmonyPatch(nameof(Player.OnDestroy))]
        private static void OnDestroyPrefix(Player __instance)
        {
            if (__instance == Player.m_localPlayer)
            {
                OverlayVisibility.Reset();
            }
        }
    }
}
