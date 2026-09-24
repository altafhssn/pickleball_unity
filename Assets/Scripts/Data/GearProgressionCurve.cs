using System.Collections.Generic;
using UnityEngine;
using Pickleball.UI;

namespace Pickleball.Data
{
    /// <summary>The six stats a gear item can carry (Docs/GearProgression.md#1-stats). A given slot
    /// only ever populates 3 of these -- see GearProgressionCurve.ComputeStats -- the rest stay 0 on
    /// that item by construction, not by a display filter.</summary>
    public struct GearStatSet
    {
        public int power;
        public int spin;
        public int control;
        public int speed;
        public int serve;
        public int stamina;
    }

    /// <summary>The formulas behind the 40-item ladder: how a (slot, tier, level) triple becomes
    /// stats, a rarity band, and an upgrade cost. This is the single source of truth for those
    /// numbers -- GearCatalog only supplies identity (name/tier/icon), everything derived lives here,
    /// matching Docs/GearProgression.md sections 3-5.
    ///
    /// Deliberately free of any per-item state: every method takes its inputs explicitly so both the
    /// live catalog (GearCatalog.ApplyLevel) and a UI upgrade preview (GearDetailScreen, one level
    /// ahead of the item's real state) can call the exact same math without mutating anything.</summary>
    public static class GearProgressionCurve
    {
        public const int MaxLevel = 10;

        /// <summary>Item Power Score -- Docs/GearProgression.md#4-the-power-curve. Tiers 1-3 are
        /// Common, 4-6 Rare, 7-9 Epic, 10 Legendary (see RarityForTier); level ranges 1-10 within a
        /// tier.</summary>
        public static float ItemPowerScore(int tier, int level)
        {
            int t = Mathf.Max(1, tier);
            int l = Mathf.Clamp(level, 1, MaxLevel);
            return 10f * Mathf.Pow(1.25f, t - 1) * Mathf.Pow(1.10f, l - 1);
        }

        public static Rarity RarityForTier(int tier)
        {
            if (tier >= 10) return Rarity.Legendary;
            if (tier >= 7) return Rarity.Epic;
            if (tier >= 4) return Rarity.Rare;
            return Rarity.Common;
        }

        /// <summary>Splits a slot's Item Power Score across its 3 active stats as 50/30/20
        /// (Docs/GearProgression.md#4-the-power-curve) and assigns them to the stat each slot owns
        /// (section 2's slot table). The other 3 stats on the returned set are left at their default,
        /// 0 -- an item never carries a nonzero value for a stat its slot doesn't grant.</summary>
        public static GearStatSet ComputeStats(GearType type, int tier, int level)
        {
            float ips = ItemPowerScore(tier, level);
            int primary = Mathf.RoundToInt(ips * 0.50f);
            int secondary = Mathf.RoundToInt(ips * 0.30f);
            int tertiary = Mathf.RoundToInt(ips * 0.20f);

            GearStatSet s = default;
            switch (type)
            {
                case GearType.Paddle:
                    s.power = primary; s.spin = secondary; s.control = tertiary;
                    break;
                case GearType.Shoes:
                    s.speed = primary; s.stamina = secondary; s.control = tertiary;
                    break;
                case GearType.Grip:
                    s.control = primary; s.spin = secondary; s.serve = tertiary;
                    break;
                case GearType.Accessory:
                    s.stamina = primary; s.serve = secondary; s.power = tertiary;
                    break;
            }
            return s;
        }

        public static GearStatSet StatsOf(GearItem item)
        {
            return new GearStatSet
            {
                power = item.power,
                spin = item.spin,
                control = item.control,
                speed = item.speed,
                serve = item.serve,
                stamina = item.stamina,
            };
        }

        /// <summary>The 3 stat labels a slot displays, in primary/secondary/tertiary order (matches
        /// ComputeStats' split and the mockup's 3-bar gear card).</summary>
        public static string[] DisplayLabels(GearType type)
        {
            switch (type)
            {
                case GearType.Paddle: return new[] { "POWER", "SPIN", "CONTROL" };
                case GearType.Shoes: return new[] { "SPEED", "STAMINA", "CONTROL" };
                case GearType.Grip: return new[] { "CONTROL", "SPIN", "SERVE" };
                case GearType.Accessory: return new[] { "STAMINA", "SERVE", "POWER" };
                default: return new[] { "POWER", "SPIN", "SERVE" };
            }
        }

        /// <summary>Pulls the 3 values DisplayLabels names out of a stat set, in the same order.</summary>
        public static int[] DisplayValues(GearType type, GearStatSet s)
        {
            switch (type)
            {
                case GearType.Paddle: return new[] { s.power, s.spin, s.control };
                case GearType.Shoes: return new[] { s.speed, s.stamina, s.control };
                case GearType.Grip: return new[] { s.control, s.spin, s.serve };
                case GearType.Accessory: return new[] { s.stamina, s.serve, s.power };
                default: return new[] { s.power, s.spin, s.serve };
            }
        }

        // Cards/coins to advance from level i to i+1, i = 1..9 (index 0..8) -- Common baseline,
        // Docs/GearProgression.md#5-upgrade-costs.
        private static readonly int[] BaseCardCost = { 2, 4, 10, 20, 50, 100, 200, 400, 800 };
        private static readonly int[] BaseCoinCost = { 50, 150, 400, 1000, 2000, 4000, 8000, 16000, 32000 };

        private static void RarityMultipliers(Rarity rarity, out float cardMult, out float coinMult)
        {
            switch (rarity)
            {
                case Rarity.Rare: cardMult = 0.5f; coinMult = 1.6f; break;
                case Rarity.Epic: cardMult = 0.2f; coinMult = 2.6f; break;
                case Rarity.Legendary: cardMult = 0.08f; coinMult = 4.5f; break;
                default: cardMult = 1f; coinMult = 1f; break;
            }
        }

        /// <summary>Cards/coins required to advance FROM currentLevel to currentLevel+1. Callers must
        /// check currentLevel &lt; MaxLevel first -- there is no level 10-&gt;11 entry.</summary>
        public static void UpgradeCost(Rarity rarity, int currentLevel, out int cards, out int coins)
        {
            int index = Mathf.Clamp(currentLevel - 1, 0, BaseCardCost.Length - 1);
            float cardMult, coinMult;
            RarityMultipliers(rarity, out cardMult, out coinMult);
            cards = Mathf.Max(1, Mathf.RoundToInt(BaseCardCost[index] * cardMult));
            coins = Mathf.Max(1, Mathf.RoundToInt(BaseCoinCost[index] * coinMult));
        }

        // Overall Rating (Docs/GearProgression.md#7-overall-rating-and-matchmaking): each slot's share
        // of the 4-slot loadout, matching section 2's stat-budget table.
        public static float SlotWeight(GearType type)
        {
            switch (type)
            {
                case GearType.Paddle: return 0.40f;
                case GearType.Shoes: return 0.25f;
                case GearType.Grip: return 0.20f;
                case GearType.Accessory: return 0.15f;
                default: return 0f;
            }
        }

        // Slot weights sum to 1.0, so a single item at max tier/level in every slot yields exactly
        // this IPS as the weighted sum -- the value that must map to a displayed 1000.
        private static readonly float MaxOvrIps = ItemPowerScore(10, MaxLevel);

        /// <summary>OVR = sum over equipped gear of (ItemPowerScore * its slot's weight), scaled so a
        /// fully-maxed 4-slot loadout reads exactly 1000. Null entries (an empty slot) are skipped
        /// rather than treated as zero-stat items -- there is no "0-tier" item to represent them as.</summary>
        public static int ComputeOverallRating(IEnumerable<GearItem> equippedItems)
        {
            float sum = 0f;
            foreach (GearItem item in equippedItems)
            {
                if (item == null) continue;
                sum += ItemPowerScore(item.tier, item.level) * SlotWeight(item.type);
            }
            return Mathf.RoundToInt(sum * (1000f / MaxOvrIps));
        }
    }
}
