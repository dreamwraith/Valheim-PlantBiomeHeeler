using System;
using PlantBiomeHeeler.Configuration;
using PlantBiomeHeeler.Core.Input;

namespace PlantBiomeHeeler.Core.State
{
    public static class OverlayVisibility
    {
        private const string CultivatorPieceTableName = "_CultivatorPieceTable";

        private static bool _isVisible;
        private static bool _isAutoShown;
        private static bool _wasCultivatorEquipped;
        private static int _cachedPieceTableId;
        private static bool _cachedIsCultivator;

        public static bool IsOverlayActive =>
            _isVisible && ModConfig.EnableMod.Value && ModConfig.EnableOverlay.Value;

        public static bool IsHudVisible =>
            ModConfig.EnableMod.Value && (ModConfig.HudMode.Value switch
            {
                HudDisplayMode.Always => true,
                HudDisplayMode.WithOverlay => IsOverlayActive,
                _ => false
            });

        public static void Update(Player player)
        {
            bool isCultivatorEquipped = IsCultivatorEquipped(player);
            if (isCultivatorEquipped != _wasCultivatorEquipped)
            {
                _wasCultivatorEquipped = isCultivatorEquipped;
                if (isCultivatorEquipped)
                {
                    OnCultivatorEquipped();
                }
                else
                {
                    OnCultivatorUnequipped();
                }
            }

            if (ZInput.GetButtonDown(ModInputManager.ToggleOverlayButton.Name))
            {
                _isVisible = !_isVisible;
                _isAutoShown = false;
            }
        }

        // the next local player starts hidden, so a cultivator it spawns holding still counts as a fresh equip
        public static void Reset()
        {
            _isVisible = false;
            _isAutoShown = false;
            _wasCultivatorEquipped = false;
        }

        private static void OnCultivatorEquipped()
        {
            if (ModConfig.ShowOverlayWithCultivator.Value && !_isVisible)
            {
                _isVisible = true;
                _isAutoShown = true;
            }
        }

        // only undo what equipping did. if the key was pressed since then the player owns the state now
        private static void OnCultivatorUnequipped()
        {
            if (_isAutoShown)
            {
                _isVisible = false;
                _isAutoShown = false;
            }
        }

        // m_buildPieces is the table on the tool prefab so the id stays the same per tool. name allocates, so only read it on a change
        private static bool IsCultivatorEquipped(Player player)
        {
            PieceTable buildPieces = player.m_buildPieces;
            if (!buildPieces)
            {
                return false;
            }

            int pieceTableId = buildPieces.GetInstanceID();
            if (pieceTableId != _cachedPieceTableId)
            {
                _cachedPieceTableId = pieceTableId;
                _cachedIsCultivator = string.Equals(buildPieces.name, CultivatorPieceTableName, StringComparison.Ordinal);
            }

            return _cachedIsCultivator;
        }
    }
}
