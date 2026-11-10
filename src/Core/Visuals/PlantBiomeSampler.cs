using System.Collections.Generic;
using UnityEngine;

namespace PlantBiomeHeeler.Core.Visuals
{
    // every biome answer goes through Heightmap.GetBiome, the exact call Plant.UpdateHealth makes,
    // so the overlay cant drift from what plants do even if the corner vote changes in a game update
    internal sealed class PlantBiomeSampler
    {
        private const float ZoneHalfDiagonal = 45.26f;

        private readonly Dictionary<Vector2s, Heightmap> _heightmapsByZone = new Dictionary<Vector2s, Heightmap>(64);

        public Dictionary<Vector2s, Heightmap>.ValueCollection Heightmaps => _heightmapsByZone.Values;

        // a zone heightmap can touch the overlay when its center is within the radius plus half a zone diagonal
        public static bool IsWithinReach(Vector3 heightmapCenter, Vector2 focus, float radius)
        {
            float reach = radius + ZoneHalfDiagonal;
            float offsetX = heightmapCenter.x - focus.x;
            float offsetZ = heightmapCenter.z - focus.y;
            return (offsetX * offsetX) + (offsetZ * offsetZ) <= reach * reach;
        }

        public void Prepare(Vector2 focus, float radius)
        {
            _heightmapsByZone.Clear();

            List<Heightmap> heightmaps = Heightmap.GetAllHeightmaps();
            for (int index = 0; index < heightmaps.Count; index++)
            {
                Heightmap heightmap = heightmaps[index];
                Vector3 center = heightmap.transform.position;
                if (IsWithinReach(center, focus, radius))
                {
                    _heightmapsByZone[ZoneSystem.GetZone(center)] = heightmap;
                }
            }
        }

        // drop the heightmap refs after each rebuild so unloaded zones dont stay referenced
        public void Clear() => _heightmapsByZone.Clear();

        public bool TryGetHeightmap(Vector2 point, out Heightmap heightmap) =>
            _heightmapsByZone.TryGetValue(ZoneSystem.GetZone(new Vector3(point.x, 0f, point.y)), out heightmap);

        public Heightmap.Biome Classify(Vector2 point) =>
            TryGetHeightmap(point, out Heightmap heightmap)
                ? heightmap.GetBiome(new Vector3(point.x, 0f, point.y))
                : Heightmap.Biome.None;
    }
}
