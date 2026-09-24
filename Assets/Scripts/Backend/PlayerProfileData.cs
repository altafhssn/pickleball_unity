using System;
using System.Collections.Generic;

namespace Pickleball.Backend
{
    /// <summary>Serializable snapshot of everything MetaGameState.CaptureSnapshot/ApplySnapshot
    /// round-trips through a profile backend. Field defaults mirror MetaGameState's historical
    /// hardcoded values, so a first-run profile (nothing saved yet) looks identical to what the game
    /// always shipped with.</summary>
    [Serializable]
    public class BagSlotProfileData
    {
        public string bagId = "";
        public string state = "Empty";
        public long unlockCompleteUnixSeconds = 0;
    }

    /// <summary>One owned gear item's progress, keyed by its catalog id rather than by its position in
    /// MetaGameState.Gear. Schema 0 stored these as eight index-aligned parallel lists, which silently
    /// mapped the wrong stats onto the wrong item the moment the catalog grew or was reordered -- see
    /// PlayerProfileData.Migrate.
    ///
    /// The stat fields default to -1 meaning "not stored, keep whatever the catalog says". Schema 0
    /// saves predate some of these lists entirely, and ApplySnapshot has always left the catalog value
    /// standing in that case; -1 preserves that exactly. CaptureSnapshot always writes real values, so
    /// -1 only ever appears on a migrated legacy profile.</summary>
    [Serializable]
    public class GearProfileEntry
    {
        public string gearId = "";
        public int level = 1;
        public int cardsCollected = 0;
        public int cardsNeeded = -1;
        public int power = -1;
        public int spin = -1;
        /// <summary>Wire name for the live GearItem.speed field (the display name became "Speed"
        /// alongside the 6-stat model). Kept as "agility" here deliberately: this is a JSON key in
        /// every existing save file, and JsonUtility has no concept of a field alias -- renaming it
        /// would silently drop every player's saved Speed progress back to the catalog default on
        /// their next load, the same way an unrecognized key already degrades gracefully by design
        /// (see the -1 sentinel contract below).</summary>
        public int agility = -1;
        public int serve = -1;
        /// <summary>Added alongside the 6-stat model. A v1 save written before that change has no
        /// "control"/"stamina" key in its JSON, so JsonUtility leaves these at their -1 default on
        /// load -- same "not stored, keep the catalog value" contract as the four stats above.</summary>
        public int control = -1;
        public int stamina = -1;
        public int upgradeCostCoins = -1;
    }

    [Serializable]
    public class PlayerProfileData
    {
        /// <summary>Bumped whenever the on-disk shape changes in a way Migrate has to repair.
        /// 0 = the original parallel-list gear format (the field did not exist yet, and JsonUtility
        /// leaves an absent field at its initializer, which is why schemaVersion must default to 0).
        /// 1 = id-keyed gear entries.</summary>
        public const int CurrentSchemaVersion = 1;

        /// <summary>The gear ids schema 0 stored positionally, in the exact order MetaGameState.Gear
        /// declared them at the time. This is a frozen description of an old file format -- never
        /// reorder it, never extend it, and never regenerate it from the live catalog, or every
        /// pre-migration save starts mapping onto the wrong items.</summary>
        public static readonly string[] LegacyGearIdOrder =
        {
            "carbon_pro",
            "swift_step",
            "starter_grip",
            "warrior_grip",
        };

        public int schemaVersion = 0;

        public string playerName = "ALTAF";
        public int coins = 12480;
        public int gems = 240;
        public int trophies = 2140;
        public int playerLevel = 24;
        public int overallRating = 812;
        public int seasonTier = 12;
        public int seasonXP = 340;
        public int currentTourIndex = 3;

        public List<GearProfileEntry> gear = new List<GearProfileEntry>();
        public List<string> equippedGearIds = new List<string>();

        // ---- Schema 0 gear format. Read by Migrate, never written again. ----
        public List<int> gearLevels = new List<int>();
        public List<int> gearCardsCollected = new List<int>();
        public List<int> gearCardsNeeded = new List<int>();
        public List<int> gearPower = new List<int>();
        public List<int> gearSpin = new List<int>();
        public List<int> gearAgility = new List<int>();
        public List<int> gearServe = new List<int>();
        public List<int> gearUpgradeCosts = new List<int>();

        public List<BagSlotProfileData> bagSlots = new List<BagSlotProfileData>();
        public string lastLeagueResultWeekKey = "";
        public List<string> unlockedFeatureIds = new List<string>();

        /// <summary>Global chest pity progress (Docs/GearProgression.md#6-acquisition-b). Added
        /// alongside the chest system -- a v1 save from before that has neither key in its JSON, so
        /// JsonUtility leaves both at 0 via the field initializer, which is exactly correct: a
        /// returning player's pity clock simply starts counting from here, nothing to migrate.</summary>
        public int chestsSinceEpic = 0;
        public int chestsSinceLegendary = 0;

        public List<int> tourWinsCurrent = new List<int>();
        public List<bool> tourCleared = new List<bool>();

        public float musicVolume = 0.62f;
        public float effectsVolume = 0.88f;
        public bool hapticsOn = true;
        public float swipeSensitivity = 0.5f;
        public bool leftHanded = false;

        /// <summary>Career match wins. Only ever increments (a loss does not decrement it), so it is a
        /// safe monotonic input for the vanity player level -- see MetaGameState.PlayerLevel, which is
        /// now computed rather than a stored counter that nothing advanced.</summary>
        public int careerWins = 0;

        /// <summary>Season-pass reward tiers the player has already claimed on the free track. A list
        /// rather than a set because JsonUtility can't serialize a HashSet; MetaGameState dedupes on
        /// load. Absent in a pre-this-change save -> empty -> nothing claimed yet, which is correct.</summary>
        public List<int> claimedSeasonPassTiers = new List<int>();

        /// <summary>UTC day key ("yyyy-MM-dd") of the last claimed daily reward, and the running
        /// consecutive-day streak. Empty/0 on an older save -> the daily reward is immediately
        /// claimable, streak starts at 1.</summary>
        public string lastDailyRewardDayKey = "";
        public int dailyRewardStreak = 0;

        /// <summary>Brings a just-loaded profile up to CurrentSchemaVersion in place. Idempotent, and a
        /// no-op on anything already current, so ProfileService can call it unconditionally after every
        /// load. Deliberately free of any UnityEngine dependency -- it is the one piece of save handling
        /// that has to be verifiable on its own.
        ///
        /// One-way by design: once a v1 profile is written, an older build reading it back finds the
        /// schema 0 lists empty and falls back to the catalog's default gear levels. Currency, trophies
        /// and tour progress still survive that downgrade; gear progress does not.</summary>
        public void Migrate()
        {
            if (schemaVersion >= CurrentSchemaVersion) return;

            if (gear == null) gear = new List<GearProfileEntry>();

            // Only rebuild from the legacy lists when there is nothing id-keyed to trust already.
            if (gear.Count == 0 && gearLevels != null && gearLevels.Count > 0)
            {
                int count = Math.Min(gearLevels.Count, LegacyGearIdOrder.Length);
                for (int i = 0; i < count; i++)
                {
                    gear.Add(new GearProfileEntry
                    {
                        gearId = LegacyGearIdOrder[i],
                        level = gearLevels[i],
                        cardsCollected = At(gearCardsCollected, i, 0),
                        cardsNeeded = At(gearCardsNeeded, i, -1),
                        power = At(gearPower, i, -1),
                        spin = At(gearSpin, i, -1),
                        agility = At(gearAgility, i, -1),
                        serve = At(gearServe, i, -1),
                        upgradeCostCoins = At(gearUpgradeCosts, i, -1),
                    });
                }
            }

            gearLevels = new List<int>();
            gearCardsCollected = new List<int>();
            gearCardsNeeded = new List<int>();
            gearPower = new List<int>();
            gearSpin = new List<int>();
            gearAgility = new List<int>();
            gearServe = new List<int>();
            gearUpgradeCosts = new List<int>();

            schemaVersion = CurrentSchemaVersion;
        }

        private static int At(List<int> list, int index, int fallback)
        {
            return list != null && index < list.Count ? list[index] : fallback;
        }
    }
}
