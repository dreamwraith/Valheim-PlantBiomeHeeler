using UnityEngine;

namespace PlantBiomeHeeler.Core.Visuals
{
    internal record struct OverlayBuildSpec(
        int FocusCellX,
        int FocusCellZ,
        float Radius,
        float HeightOffset,
        float InteriorOpacity,
        float BorderOpacity,
        float BorderWidth,
        float WaterLevel,
        bool EnableHatchLines,
        float HatchLineSpacing,
        float HatchLineWidth)
    {
        public Vector2 FocusCenter => new Vector2(FocusCellX + 0.5f, FocusCellZ + 0.5f);
    }
}
