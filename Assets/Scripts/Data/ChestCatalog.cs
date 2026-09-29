// Retired from the launch build: this feature is outside the scope in Docs/LaunchScope.md
// (Pickleball Game Design Document v1.0). The source is kept for reference and is not compiled;
// PICKLEBALL_RETIRED_FEATURES is deliberately never defined.
#if PICKLEBALL_RETIRED_FEATURES
using UnityEngine;
using Pickleball.UI;

namespace Pickleball.Data
{
    /// <summary>One chest type's identity and odds table (Docs/GearProgression.md#6-acquisition-b).
    /// `commonChance`/`rareChance`/`epicChance`/`legendaryChance` should sum to 1.0 -- ChestRoller
    /// treats them as consecutive bands, not independent probabilities, so a table that doesn't sum
    /// to 1.0 silently shifts how often the leftover mass falls into Legendary (see ResolveRarityBand).
    /// `unlockSeconds` of 0 means "opens immediately" -- shop-bought chests (Epic/Legendary) skip the
    /// BagSlot unlock timer entirely; see MetaGameState.StartBagUnlock.</summary>
    public struct ChestDefinition
    {
        public string id;
        public string name;
        public IconId icon;
        public int unlockSeconds;
        public int cardsMin;
        public int cardsMax;
        public int coinBonusMin;
        public int coinBonusMax;
        public float commonChance;
        public float rareChance;
        public float epicChance;
        public float legendaryChance;
        /// <summary>The Legendary Chest's own "1 guaranteed" column (Docs/GearProgression.md#6) isn't a
        /// percentage like the other three -- it reads as a floor on top of whatever the base table
        /// would give. Modeled here as an outright 100% Legendary roll rather than a second, separate
        /// guaranteed-slot reward alongside a base-table roll: simpler, and this is a "start" of the
        /// chest system, not the full multi-reward-per-chest version. commonChance/rareChance/epicChance
        /// are kept on this definition purely as documentation of what the doc's table specifies, and
        /// are never read when this is true.</summary>
        public bool guaranteesLegendary;
    }

    public static class ChestCatalog
    {
        // Docs/GearProgression.md#6-acquisition-b: "guaranteed Epic every 30 chests without one;
        // guaranteed Legendary every 300." Global counters, not per-chest-type -- the doc's framing
        // ("30 chests", not "30 chests of this type") reads as one pity clock across every chest a
        // player opens, and that is also the simplest thing to reason about and to persist.
        public const int PityEpicThreshold = 30;
        public const int PityLegendaryThreshold = 300;

        private static ChestDefinition Def(string id, string name, int unlockSeconds, int cardsMin, int cardsMax,
            int coinBonusMin, int coinBonusMax, float common, float rare, float epic, float legendary, bool guaranteesLegendary = false)
        {
            return new ChestDefinition
            {
                id = id,
                name = name,
                icon = IconId.Chest,
                unlockSeconds = unlockSeconds,
                cardsMin = cardsMin,
                cardsMax = cardsMax,
                coinBonusMin = coinBonusMin,
                coinBonusMax = coinBonusMax,
                commonChance = common,
                rareChance = rare,
                epicChance = epic,
                legendaryChance = legendary,
                guaranteesLegendary = guaranteesLegendary,
            };
        }

        // Odds and card counts are the doc's section-6 table verbatim. Coin bonuses are an addition
        // beyond that table -- chests granted coins alongside cards before this system existed
        // (OpenReadyBag's old hardcoded 350), and dropping that entirely would be a felt regression
        // for no reason the doc asks for; kept modest and scaled to chest tier instead.
        private static readonly ChestDefinition[] Definitions =
        {
            // Not in the doc's table -- a one-time first-run freebie, tuned generous on purpose for a
            // good first impression rather than derived from any of the real economy numbers below.
            Def("welcome_bag", "WELCOME BAG", 0, 15, 25, 100, 150, 0.60f, 0.30f, 0.09f, 0.01f),

            Def("match_bag", "MATCH BAG", 7200, 8, 14, 20, 40, 0.82f, 0.15f, 0.028f, 0.002f),
            Def("tour_crate", "TOUR CRATE", 14400, 20, 30, 40, 70, 0.72f, 0.22f, 0.054f, 0.006f),
            Def("league_chest", "LEAGUE CHEST", 28800, 45, 70, 80, 140, 0.62f, 0.28f, 0.085f, 0.015f),
            // Shop-purchased (gems) rather than timer-unlocked -- unlockSeconds is 0 (opens instantly
            // once granted) and there's no coin bonus, since it already cost a premium currency.
            Def("epic_chest", "EPIC CHEST", 0, 60, 90, 0, 0, 0.40f, 0.40f, 0.18f, 0.02f),
            // guaranteesLegendary -- see the field's own doc comment. The 0.25/0.40/0.30/0.05 values
            // are the doc's literal table (its 4th column read as a 5% base-table remainder) and are
            // never actually applied; kept for anyone reading this table against the doc.
            Def("legendary_chest", "LEGENDARY CHEST", 0, 120, 180, 0, 0, 0.25f, 0.40f, 0.30f, 0.05f, guaranteesLegendary: true),
        };

        /// <summary>Used when a BagSlot's bagId doesn't match any known definition -- a hand-edited or
        /// pre-this-system save, most plausibly. Match Bag's numbers are the closest thing to a
        /// "default" chest in the existing game (it's the one AddMatchBag has always granted).</summary>
        public static readonly ChestDefinition Fallback = Definitions[1];

        public static ChestDefinition? Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (ChestDefinition def in Definitions)
                if (def.id == id) return def;
            return null;
        }
    }

    /// <summary>Global pity progress -- see ChestCatalog's threshold constants. Persisted verbatim in
    /// PlayerProfileData; a returning v1 save with no pity fields yet simply starts both at 0 via the
    /// field initializer, which is exactly correct (nothing to migrate, pity just resumes counting).</summary>
    public struct PityState
    {
        public int chestsSinceEpic;
        public int chestsSinceLegendary;
    }

    /// <summary>One resolved chest opening, before it's applied to the player's coins/cards or turned
    /// into a gear pick. Deliberately separate from RewardEntry/RewardBundle (the UI-facing display
    /// types in MetaGameState) so the actual roll has no dependency on MetaGameState, the live Gear
    /// catalog, or UI types, and can be exercised standalone.</summary>
    public struct ChestRollResult
    {
        public Rarity rarity;
        public int cardCount;
        public int coinBonus;
        /// <summary>True if pity raised the rarity above what the natural roll alone would have given.
        /// Not surfaced in the UI yet -- carried through so a future "pity protected you!" callout has
        /// something to key off without re-deriving it.</summary>
        public bool pityTriggered;
    }

    /// <summary>The chest odds/pity math, kept pure and separate from ChestCatalog's identity data for
    /// the same reason GearProgressionCurve is separate from GearCatalog: every input is explicit
    /// (including the RNG rolls and the pity state, both passed in rather than read from anywhere
    /// static), so the rarity-band and pity-transition logic can be checked directly without touching
    /// MetaGameState, UnityEngine.Random, or a save file.</summary>
    public static class ChestRoller
    {
        /// <summary>Resolves one chest opening. `rarityRoll01`/`cardCountRoll01`/`coinBonusRoll01` are
        /// caller-supplied [0,1) values -- UnityEngine.Random.value in production, a fixed value in a
        /// test -- so the band math is independently verifiable without seeding or exhausting an RNG
        /// stream. Mutates `pity` in place to the state after this opening; the caller is responsible
        /// for persisting it (MetaGameState does, via its own pity field).
        ///
        /// Chest rolls intentionally do NOT use Pickleball.Sim.DeterministicRandom. That type exists
        /// so a server can re-simulate a *competitive match* and get a bit-identical result from a
        /// recorded seed -- chests are single-player economy RNG with no such requirement, and roping
        /// it in here would blur what that type is for. (Chest rolls are also not currently
        /// server-validated at all -- a compromised client could roll however it likes locally; that's
        /// the same unaddressed anti-cheat gap noted for shot data in Docs/GearProgression.md#8.)</summary>
        public static ChestRollResult Roll(ChestDefinition chest, float rarityRoll01, float cardCountRoll01, float coinBonusRoll01, ref PityState pity)
        {
            Rarity natural = ResolveRarityBand(chest, rarityRoll01);
            Rarity final = natural;
            bool pityTriggered = false;

            if (chest.guaranteesLegendary)
            {
                final = Rarity.Legendary;
            }
            else if (pity.chestsSinceLegendary + 1 >= ChestCatalog.PityLegendaryThreshold)
            {
                pityTriggered = final != Rarity.Legendary;
                final = Rarity.Legendary;
            }
            else if (pity.chestsSinceEpic + 1 >= ChestCatalog.PityEpicThreshold && final < Rarity.Epic)
            {
                final = Rarity.Epic;
                pityTriggered = true;
            }

            // Any Legendary (natural, pity-forced, or guaranteed) resets both counters -- the player
            // got what both promises cover. An Epic that isn't also a Legendary resets only its own
            // counter; the Legendary clock keeps counting since it wasn't satisfied.
            if (final == Rarity.Legendary)
            {
                pity.chestsSinceEpic = 0;
                pity.chestsSinceLegendary = 0;
            }
            else if (final == Rarity.Epic)
            {
                pity.chestsSinceEpic = 0;
                pity.chestsSinceLegendary++;
            }
            else
            {
                pity.chestsSinceEpic++;
                pity.chestsSinceLegendary++;
            }

            int cardCount = chest.cardsMin + Mathf.RoundToInt((chest.cardsMax - chest.cardsMin) * Mathf.Clamp01(cardCountRoll01));
            int coinBonus = chest.coinBonusMin + Mathf.RoundToInt((chest.coinBonusMax - chest.coinBonusMin) * Mathf.Clamp01(coinBonusRoll01));

            return new ChestRollResult { rarity = final, cardCount = cardCount, coinBonus = coinBonus, pityTriggered = pityTriggered };
        }

        /// <summary>Reads the four chances as consecutive bands over [0,1) rather than as four
        /// independent rolls, so they partition the space exactly (no gaps, no overlap) regardless of
        /// float rounding -- whatever's left over after Common+Rare+Epic falls to Legendary.</summary>
        private static Rarity ResolveRarityBand(ChestDefinition chest, float roll01)
        {
            float r = Mathf.Clamp01(roll01);
            if (r < chest.commonChance) return Rarity.Common;
            r -= chest.commonChance;
            if (r < chest.rareChance) return Rarity.Rare;
            r -= chest.rareChance;
            if (r < chest.epicChance) return Rarity.Epic;
            return Rarity.Legendary;
        }
    }
}
#endif
