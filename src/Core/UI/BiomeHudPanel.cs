using PlantBiomeHeeler.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlantBiomeHeeler.Core.UI
{
    internal sealed class BiomeHudPanel
    {
        private const string NoBiomeText = "-";
        private const string TitleText = "Plant Biome";
        private const float TitleFontScale = 0.75f;
        private const string TargetPrefix = "Target: ";
        private const string StandingPrefix = "Standing: ";

        private readonly GameObject _root;
        private readonly RectTransform _rootRect;
        private readonly Image _background;
        private readonly TextMeshProUGUI _titleLabel;
        private readonly TextMeshProUGUI _targetLabel;
        private readonly TextMeshProUGUI _standingLabel;
        private readonly string?[] _biomeTextCache = new string?[(int)Heightmap.BiomeIndex.Count];

        private Heightmap.Biome _shownTargetBiome;
        private Heightmap.Biome _shownStandingBiome;
        private bool _hasText;

        public BiomeHudPanel(Transform parentTransform)
        {
            _root = new GameObject("PlantBiomeHeeler_BiomeHud", typeof(RectTransform));
            _root.transform.SetParent(parentTransform, false);

            _rootRect = (RectTransform)_root.transform;
            _rootRect.anchorMin = new Vector2(0f, 1f);
            _rootRect.anchorMax = new Vector2(0f, 1f);
            _rootRect.pivot = new Vector2(0f, 1f);

            _background = _root.AddComponent<Image>();
            _background.raycastTarget = false;

            VerticalLayoutGroup layout = _root.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(10, 10, 6, 6);

            ContentSizeFitter fitter = _root.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _titleLabel = CreateLabel(_rootRect, "TitleLabel");
            _titleLabel.fontStyle = FontStyles.Bold;
            _titleLabel.text = TitleText;

            _targetLabel = CreateLabel(_rootRect, "TargetLabel");
            _standingLabel = CreateLabel(_rootRect, "StandingLabel");
        }

        public void ApplySettings()
        {
            _rootRect.anchoredPosition = new Vector2(ModConfig.HudOffsetX.Value, ModConfig.HudOffsetY.Value);

            Color backgroundColor = ModConfig.HudBackgroundColor.Value;
            _background.color = backgroundColor;
            _background.enabled = backgroundColor.a > 0.001f;

            float fontSize = ModConfig.HudFontSize.Value;
            _titleLabel.fontSize = fontSize * TitleFontScale;
            _targetLabel.fontSize = fontSize;
            _standingLabel.fontSize = fontSize;

            // colors may have changed, so the cached rich text strings are stale
            for (int index = 0; index < _biomeTextCache.Length; index++)
            {
                _biomeTextCache[index] = null;
            }

            _hasText = false;
        }

        public void SetBiomes(Heightmap.Biome targetBiome, Heightmap.Biome standingBiome)
        {
            SetRow(_targetLabel, TargetPrefix, targetBiome, ref _shownTargetBiome);
            SetRow(_standingLabel, StandingPrefix, standingBiome, ref _shownStandingBiome);
            _hasText = true;
        }

        public void SetActive(bool active)
        {
            if (_root.activeSelf != active)
            {
                _root.SetActive(active);
            }
        }

        public void Destroy()
        {
            if (_root)
            {
                Object.Destroy(_root);
            }
        }

        // only touch the label when its biome moved, so a steady readout makes no new strings
        private void SetRow(TextMeshProUGUI label, string prefix, Heightmap.Biome biome, ref Heightmap.Biome shownBiome)
        {
            if (_hasText && biome == shownBiome)
            {
                return;
            }

            shownBiome = biome;
            label.text = prefix + GetBiomeText(biome);
        }

        private string GetBiomeText(Heightmap.Biome biome)
        {
            if (biome == Heightmap.Biome.None)
            {
                return NoBiomeText;
            }

            int cacheIndex = biome.ToIndex();
            string? cachedText = _biomeTextCache[cacheIndex];
            if (cachedText != null)
            {
                return cachedText;
            }

            string biomeName = Localization.instance.Localize(BiomeSector.GetBiomeName(biome));
            string colorHex = ColorUtility.ToHtmlStringRGB(ModConfig.GetBiomeColor(biome));
            string biomeText = $"<color=#{colorHex}>{biomeName}</color>";
            _biomeTextCache[cacheIndex] = biomeText;
            return biomeText;
        }

        private static TextMeshProUGUI CreateLabel(Transform parentTransform, string objectName)
        {
            // add the tmp component while inactive, its Awake goes looking for default tmp resources the game build stripped
            // the popup font material has the fallback glyphs, the plain jotunn font is the backup
            GameObject labelObject = new GameObject(objectName, typeof(RectTransform));
            labelObject.SetActive(false);
            labelObject.transform.SetParent(parentTransform, false);
            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();

            if (UnifiedPopup.instance && UnifiedPopup.instance.bodyText)
            {
                label.font = UnifiedPopup.instance.bodyText.font;
                label.fontSharedMaterial = UnifiedPopup.instance.bodyText.fontSharedMaterial;
            }
            else
            {
                label.font = Jotunn.Managers.GUIManager.Instance.TMP_AveriaSansLibre;
            }

            label.color = Color.white;
            label.richText = true;
            label.raycastTarget = false;
            label.enableAutoSizing = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.alignment = TextAlignmentOptions.TopLeft;
            labelObject.SetActive(true);
            return label;
        }
    }
}
