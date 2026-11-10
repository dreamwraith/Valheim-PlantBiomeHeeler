using HarmonyLib;
using PlantBiomeHeeler.Core.Visuals;

namespace PlantBiomeHeeler.Patches
{
    [HarmonyPatch(typeof(Heightmap))]
    internal static class HeightmapPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(Heightmap.RebuildCollisionMesh))]
        private static void RebuildCollisionMeshPostfix(Heightmap __instance) =>
            BiomeOverlayRenderer.MarkTerrainDirty(__instance);
    }
}
