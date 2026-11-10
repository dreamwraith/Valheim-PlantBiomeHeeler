using System.Collections.Generic;
using UnityEngine;

namespace PlantBiomeHeeler.Core.Visuals
{
    internal sealed class TerrainHeightSampler
    {
        private readonly PlantBiomeSampler _biomeSampler;

        public TerrainHeightSampler(PlantBiomeSampler biomeSampler)
        {
            _biomeSampler = biomeSampler;
        }

        // bilinear over the zone heightmap vertices. water counts as the surface so shore and ocean regions stay visible
        public float SampleSurfaceHeight(Vector2 point, float waterLevel)
        {
            if (!_biomeSampler.TryGetHeightmap(point, out Heightmap heightmap))
            {
                return waterLevel;
            }

            int width = heightmap.m_width;
            int stride = width + 1;
            float scale = heightmap.m_scale;
            float halfSize = width * scale * 0.5f;
            Vector3 heightmapOrigin = heightmap.transform.position;

            float gridX = Mathf.Clamp(((point.x - heightmapOrigin.x) + halfSize) / scale, 0f, width);
            float gridZ = Mathf.Clamp(((point.y - heightmapOrigin.z) + halfSize) / scale, 0f, width);
            int column = Mathf.Min((int)gridX, width - 1);
            int row = Mathf.Min((int)gridZ, width - 1);
            float blendX = gridX - column;
            float blendZ = gridZ - row;

            List<float> heights = heightmap.m_heights;
            int southIndex = (row * stride) + column;
            int northIndex = southIndex + stride;
            float southHeight = Mathf.LerpUnclamped(heights[southIndex], heights[southIndex + 1], blendX);
            float northHeight = Mathf.LerpUnclamped(heights[northIndex], heights[northIndex + 1], blendX);
            float groundHeight = heightmapOrigin.y + Mathf.LerpUnclamped(southHeight, northHeight, blendZ);
            return Mathf.Max(groundHeight, waterLevel);
        }
    }
}
