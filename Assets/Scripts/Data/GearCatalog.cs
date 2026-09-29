// Retired from the launch build: this feature is outside the scope in Docs/LaunchScope.md
// (Pickleball Game Design Document v1.0). The source is kept for reference and is not compiled;
// PICKLEBALL_RETIRED_FEATURES is deliberately never defined.
#if PICKLEBALL_RETIRED_FEATURES
using System.Collections.Generic;
using UnityEngine;
using Pickleball.UI;

namespace Pickleball.Data
{
    /// <summary>Identity-only description of one ladder rung: which slot, where in that slot's 1-10
    /// tier order, and its display name/icon. Everything else -- rarity, stats, upgrade cost -- is
    /// derived from (type, tier, level) by GearProgressionCurve, so this table is the single place
    /// the 40-item ladder in Docs/GearProgression.md#3-item-ladders is authored. Nobody hand-types a
    /// stat or a cost; changing the curve changes all 400 upgrade states at once.</summary>
    internal struct GearBlueprint
    {
        public string id;
        public string name;
        public GearType type;
        public int tier;
        public IconId icon;
        public bool isStarter;
        /// <summary>Seed state for a brand-new profile. Every item defaults to level 1 / 0 cards
        /// except the 4 items that existed before this catalog did -- their historical prototype
        /// progress (carbon_pro L7, swift_step L5, starter_grip and warrior_grip's starting cards)
        /// is preserved here rather than reset, so a fresh install still opens with the same
        /// hand-tuned starting loadout it always has. A returning player's actual save always wins;
        /// this only seeds ProfileService's first-run profile.</summary>
        public int startLevel;
        public int startCardsCollected;
    }

    public static class GearCatalog
    {
        private static GearBlueprint Item(string id, string name, GearType type, int tier, IconId icon,
            bool isStarter = false, int startLevel = 1, int startCards = 0)
        {
            return new GearBlueprint
            {
                id = id,
                name = name,
                type = type,
                tier = tier,
                icon = icon,
                isStarter = isStarter,
                startLevel = startLevel,
                startCardsCollected = startCards,
            };
        }

        // Rarity bands: tier 1-3 Common, 4-6 Rare, 7-9 Epic, 10 Legendary (GearProgressionCurve.RarityForTier).
        private static readonly GearBlueprint[] Blueprints =
        {
            // ---- Paddle ----
            Item("paddle_wooden_rec", "WOODEN REC", GearType.Paddle, 1, IconId.Paddle),
            Item("paddle_playground_poly", "PLAYGROUND POLY", GearType.Paddle, 2, IconId.Paddle),
            Item("paddle_club_composite", "CLUB COMPOSITE", GearType.Paddle, 3, IconId.Paddle),
            Item("paddle_fiberglass_edge", "FIBERGLASS EDGE", GearType.Paddle, 4, IconId.Paddle),
            Item("paddle_graphite_rally", "GRAPHITE RALLY", GearType.Paddle, 5, IconId.Paddle),
            Item("carbon_pro", "CARBON PRO", GearType.Paddle, 6, IconId.Paddle, startLevel: 7, startCards: 24),
            Item("paddle_thermo_kevlar", "THERMO KEVLAR", GearType.Paddle, 7, IconId.Paddle),
            Item("paddle_raw_carbon_t700", "RAW CARBON T700", GearType.Paddle, 8, IconId.Paddle),
            Item("paddle_elongated_sniper", "ELONGATED SNIPER", GearType.Paddle, 9, IconId.Paddle),
            Item("paddle_titan_apex", "TITAN APEX", GearType.Paddle, 10, IconId.Paddle),

            // ---- Shoes ----
            Item("shoes_canvas_trainers", "CANVAS TRAINERS", GearType.Shoes, 1, IconId.Shoe),
            Item("shoes_court_basics", "COURT BASICS", GearType.Shoes, 2, IconId.Shoe),
            Item("shoes_grip_runner", "GRIP RUNNER", GearType.Shoes, 3, IconId.Shoe),
            Item("swift_step", "SWIFT STEP", GearType.Shoes, 4, IconId.Shoe, startLevel: 5, startCards: 14),
            Item("shoes_lateral_lock", "LATERAL LOCK", GearType.Shoes, 5, IconId.Shoe),
            Item("shoes_airglide_court", "AIRGLIDE COURT", GearType.Shoes, 6, IconId.Shoe),
            Item("shoes_pivot_pro", "PIVOT PRO", GearType.Shoes, 7, IconId.Shoe),
            Item("shoes_kinetic_sole", "KINETIC SOLE", GearType.Shoes, 8, IconId.Shoe),
            Item("shoes_phantom_slide", "PHANTOM SLIDE", GearType.Shoes, 9, IconId.Shoe),
            Item("shoes_velocity_x", "VELOCITY X", GearType.Shoes, 10, IconId.Shoe),

            // ---- Grip ----
            Item("starter_grip", "STARTER GRIP", GearType.Grip, 1, IconId.Tape, isStarter: true, startCards: 8),
            Item("grip_cotton_wrap", "COTTON WRAP", GearType.Grip, 2, IconId.Tape),
            Item("warrior_grip", "THE WARRIOR", GearType.Grip, 3, IconId.Tape, startCards: 4),
            Item("grip_tacky_weave", "TACKY WEAVE", GearType.Grip, 4, IconId.Tape),
            Item("grip_perforated_comfort", "PERFORATED COMFORT", GearType.Grip, 5, IconId.Tape),
            Item("grip_gel_core", "GEL CORE", GearType.Grip, 6, IconId.Tape),
            Item("grip_moisture_lock", "MOISTURE LOCK", GearType.Grip, 7, IconId.Tape),
            Item("grip_contour_pro", "CONTOUR PRO", GearType.Grip, 8, IconId.Tape),
            Item("grip_torque_wrap", "TORQUE WRAP", GearType.Grip, 9, IconId.Tape),
            Item("grip_serpent_coil", "SERPENT COIL", GearType.Grip, 10, IconId.Tape),

            // ---- Wristband (GearType.Accessory) ----
            Item("wrist_terry_band", "TERRY BAND", GearType.Accessory, 1, IconId.Wristband),
            Item("wrist_sweat_guard", "SWEAT GUARD", GearType.Accessory, 2, IconId.Wristband),
            Item("wrist_club_cuff", "CLUB CUFF", GearType.Accessory, 3, IconId.Wristband),
            Item("wrist_compression_sleeve", "COMPRESSION SLEEVE", GearType.Accessory, 4, IconId.Wristband),
            Item("wrist_kinesio_wrap", "KINESIO WRAP", GearType.Accessory, 5, IconId.Wristband),
            Item("wrist_power_cuff", "POWER CUFF", GearType.Accessory, 6, IconId.Wristband),
            Item("wrist_thermo_brace", "THERMO BRACE", GearType.Accessory, 7, IconId.Wristband),
            Item("wrist_pulse_band", "PULSE BAND", GearType.Accessory, 8, IconId.Wristband),
            Item("wrist_recoil_sleeve", "RECOIL SLEEVE", GearType.Accessory, 9, IconId.Wristband),
            Item("wrist_ironwrist", "IRONWRIST", GearType.Accessory, 10, IconId.Wristband),
        };

        public static List<GearItem> Build()
        {
            var list = new List<GearItem>(Blueprints.Length);
            foreach (GearBlueprint bp in Blueprints)
            {
                GearItem item = new GearItem
                {
                    id = bp.id,
                    name = bp.name,
                    type = bp.type,
                    icon = bp.icon,
                    rarity = GearProgressionCurve.RarityForTier(bp.tier),
                    tier = bp.tier,
                    isStarter = bp.isStarter,
                    cardsCollected = bp.startCardsCollected,
                };
                ApplyLevel(item, bp.startLevel);
                list.Add(item);
            }
            return list;
        }

        /// <summary>Sets an item's level and recomputes everything that follows from it: stats via
        /// GearProgressionCurve.ComputeStats, and the cost of the next upgrade via UpgradeCost. Used
        /// both to seed a fresh item (Build) and to apply an upgrade in place (MetaGameState.TryUpgradeGear)
        /// -- the two paths must never compute stats differently.</summary>
        public static void ApplyLevel(GearItem item, int level)
        {
            level = Mathf.Clamp(level, 1, GearProgressionCurve.MaxLevel);
            item.level = level;

            GearStatSet s = GearProgressionCurve.ComputeStats(item.type, item.tier, level);
            item.power = s.power;
            item.spin = s.spin;
            item.control = s.control;
            item.speed = s.speed;
            item.serve = s.serve;
            item.stamina = s.stamina;

            if (level < GearProgressionCurve.MaxLevel)
            {
                int cards, coins;
                GearProgressionCurve.UpgradeCost(item.rarity, level, out cards, out coins);
                item.cardsNeeded = cards;
                item.upgradeCostCoins = coins;
            }
            else
            {
                // No level 10 -> 11 entry in the cost table; MetaGameState.CanUpgrade gates on
                // level < MaxLevel before ever reading these, but int.MaxValue keeps them honest
                // (unaffordable, uncollectable) if anything reads them first.
                item.cardsNeeded = int.MaxValue;
                item.upgradeCostCoins = int.MaxValue;
            }
        }
    }
}
#endif
