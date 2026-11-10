using PlantBiomeHeeler.Configuration;
using UnityEngine;

namespace PlantBiomeHeeler.Core.Visuals
{
    // one home for where an overlay vertex sits and what color it gets, shared by the fill and the border strips
    internal sealed class OverlayVertexFactory
    {
        private const float InnerFadeFraction = 0.65f;

        private readonly TerrainHeightSampler _heightSampler;
        private readonly Color32[] _fillColors = new Color32[(int)Heightmap.BiomeIndex.Count];
        private readonly Color32[] _borderColors = new Color32[(int)Heightmap.BiomeIndex.Count];

        private Vector2 _focus;
        private float _radius;
        private float _radiusSquared;
        private float _innerRadius;
        private float _innerRadiusSquared;
        private float _heightOffset;
        private float _waterLevel;
        private float _hatchOrigin;
        private float _hatchFrequency;

        public OverlayVertexFactory(TerrainHeightSampler heightSampler)
        {
            _heightSampler = heightSampler;
        }

        public void Prepare(in OverlayBuildSpec spec)
        {
            _focus = spec.FocusCenter;
            _radius = spec.Radius;
            _radiusSquared = spec.Radius * spec.Radius;
            _innerRadius = spec.Radius * InnerFadeFraction;
            _innerRadiusSquared = _innerRadius * _innerRadius;
            _heightOffset = spec.HeightOffset;
            _waterLevel = spec.WaterLevel;

            // hatch lines run along x + z = constant, the sqrt2 turns line spacing into the period along x + z
            // and the origin snaps to a whole period near the player so the uv stays small and the lines stay put in the world
            float hatchPeriod = spec.HatchLineSpacing * Mathf.Sqrt(2f);
            _hatchFrequency = 1f / hatchPeriod;
            _hatchOrigin = Mathf.Round((_focus.x + _focus.y) / hatchPeriod) * hatchPeriod;

            // the game renders in linear color space and Sprites/Default uses vertex colors raw,
            // so the picked color gets converted here or it shows up lighter and washed out
            // the picked alpha times InteriorOpacity is the fill opacity, borders share one opacity across all biomes
            for (Heightmap.BiomeIndex biomeIndex = Heightmap.BiomeIndex.Meadows; biomeIndex < Heightmap.BiomeIndex.Count; biomeIndex++)
            {
                Color fillColor = ModConfig.GetBiomeColor(biomeIndex.ToBiome()).linear;
                Color borderColor = fillColor;
                borderColor.a = spec.BorderOpacity;
                fillColor.a *= spec.InteriorOpacity;
                _fillColors[(int)biomeIndex] = fillColor;
                _borderColors[(int)biomeIndex] = borderColor;
            }
        }

        public Vector3 GetPosition(Vector2 point) =>
            new Vector3(point.x, _heightSampler.SampleSurfaceHeight(point, _waterLevel) + _heightOffset, point.y);

        public Color32 GetFillColor(Heightmap.Biome biome, Vector2 point) =>
            ApplyRadialFade(_fillColors[biome.ToIndex()], point);

        public Color32 GetBorderColor(Heightmap.Biome biome, Vector2 point) =>
            ApplyRadialFade(_borderColors[biome.ToIndex()], point);

        public Vector2 GetHatchUv(Vector2 point) =>
            new Vector2(((point.x + point.y) - _hatchOrigin) * _hatchFrequency, 0f);

        private Color32 ApplyRadialFade(Color32 color, Vector2 point)
        {
            float offsetX = point.x - _focus.x;
            float offsetZ = point.y - _focus.y;
            float distanceSquared = (offsetX * offsetX) + (offsetZ * offsetZ);
            if (distanceSquared <= _innerRadiusSquared)
            {
                return color;
            }

            if (distanceSquared >= _radiusSquared)
            {
                color.a = 0;
                return color;
            }

            float normalizedFade = (_radius - Mathf.Sqrt(distanceSquared)) / (_radius - _innerRadius);
            float smoothFade = normalizedFade * normalizedFade * (3f - (2f * normalizedFade));
            color.a = (byte)(color.a * smoothFade);
            return color;
        }
    }
}
