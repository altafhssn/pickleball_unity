using System;

namespace Pickleball.Sim
{
    /// <summary>The three gear slots that carry a stat. Outfits are cosmetic and live apart from these.</summary>
    public enum GearSlot { Paddle, Shoes, Grip }

    public enum PurchaseResult { Purchased, NotEnoughCoins, MaxLevel }

    /// <summary>
    /// Gear levels, the stat each slot grants, and what the next level costs. Upgrade prices, the
    /// level cap and the stat curve are balancing placeholders, not approved values.
    /// </summary>
    public static class GearConfig
    {
        public const int StartLevel = 1;
        public const int MaxLevel = 10;

        /// <summary>Stat points per level: level 1 grants 10, level 10 grants 100.</summary>
        public const int StatPerLevel = 10;

        /// <summary>Coins to go from level n to n+1, at index n-1.</summary>
        public static readonly int[] UpgradeCosts = { 100, 200, 350, 550, 800, 1100, 1500, 2000, 2600 };
    }

    public static class GearRules
    {
        public static int ClampLevel(int level) => Math.Max(GearConfig.StartLevel, Math.Min(GearConfig.MaxLevel, level));

        public static bool IsMaxLevel(int level) => level >= GearConfig.MaxLevel;

        /// <summary>The stat a slot at this level grants.</summary>
        public static int StatAtLevel(int level) => ClampLevel(level) * GearConfig.StatPerLevel;

        /// <summary>Coins for the next level, or -1 at the cap.</summary>
        public static int UpgradeCost(int level)
        {
            if (IsMaxLevel(level)) return -1;
            int index = Math.Max(0, Math.Min(GearConfig.UpgradeCosts.Length - 1, ClampLevel(level) - 1));
            return GearConfig.UpgradeCosts[index];
        }

        /// <summary>The name of the one stat a slot improves.</summary>
        public static string StatName(GearSlot slot)
        {
            switch (slot)
            {
                case GearSlot.Paddle: return "POWER";
                case GearSlot.Shoes: return "SPEED";
                default: return "ACCURACY";
            }
        }

        /// <summary>Each slot feeds exactly one stat: the paddle Power, the shoes Speed, the grip
        /// Accuracy. Nothing else contributes.</summary>
        public static LoadoutStats Loadout(int paddleLevel, int shoesLevel, int gripLevel)
        {
            return new LoadoutStats
            {
                power = StatAtLevel(paddleLevel),
                speed = StatAtLevel(shoesLevel),
                accuracy = StatAtLevel(gripLevel),
            };
        }

        /// <summary>
        /// Buys one level. Atomic: either the balance is checked, the cost deducted and the level
        /// raised together, or nothing changes at all -- a refused purchase never takes coins, and a
        /// repeated call buys the next level at that level's price rather than charging twice for one.
        /// </summary>
        public static PurchaseResult TryUpgrade(ref int coins, ref int level)
        {
            if (IsMaxLevel(level)) return PurchaseResult.MaxLevel;
            int cost = UpgradeCost(level);
            if (!TrySpend(ref coins, cost)) return PurchaseResult.NotEnoughCoins;
            level = ClampLevel(level + 1);
            return PurchaseResult.Purchased;
        }

        /// <summary>Deducts <paramref name="price"/> if the balance covers it; otherwise leaves it untouched.</summary>
        public static bool TrySpend(ref int coins, int price)
        {
            if (price < 0 || coins < price) return false;
            coins -= price;
            return true;
        }
    }
}
