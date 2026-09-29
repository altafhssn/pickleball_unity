using System;
using System.Collections.Generic;

namespace Pickleball.Backend
{
    /// <summary>Serializable snapshot of everything MetaGameState.CaptureSnapshot/ApplySnapshot
    /// round-trips through a profile backend. Field defaults are a brand-new player's profile.</summary>
    [Serializable]
    public class BagSlotProfileData
    {
        public string bagId = "";
        public string state = "Empty";
        public long unlockCompleteUnixSeconds = 0;
    }

    /// <summary>One item's progress from the retired 40-item gear ladder, keyed by catalog id. Read
    /// only by <see cref="PlayerProfileData.Migrate"/>, which seeds the schema 2 slot levels from the
    /// items a player had equipped. -1 means "not stored".</summary>
    [Serializable]
    public class GearProfileEntry
    {
        public string gearId = "";
        public int level = 1;
        public int cardsCollected = 0;
        public int cardsNeeded = -1;
        public int power = -1;
        public int spin = -1;
        public int agility = -1;
        public int serve = -1;
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
        /// 1 = id-keyed gear entries.
        /// 2 = launch scope: one upgrade level per gear slot, a cosmetic outfit, league points,
        ///     a reward ledger and the rewarded-ad allowance.</summary>
        public const int CurrentSchemaVersion = 2;

        /// <summary>The gear ids schema 0 stored positionally, in the exact order MetaGameState.Gear
        /// declared them at the time. A frozen description of an old file format -- never reorder or
        /// extend it.</summary>
        public static readonly string[] LegacyGearIdOrder =
        {
            "carbon_pro",
            "swift_step",
            "starter_grip",
            "warrior_grip",
        };

        public int schemaVersion = 0;

        public string playerName = "ALTAF";
        public int coins = Sim.EconomyConfig.StartingCoins;

        /// <summary>League points. The JSON key keeps its old name: it is the same number the retired
        /// trophy count was, and renaming it would reset every existing save's rank.</summary>
        public int trophies = Sim.LeagueConfig.StartingPoints;

        // ---- Schema 2 ----
        /// <summary>Gear slot levels. 0 means "not stored" -- an older save -- and Migrate seeds it.</summary>
        public int paddleLevel = Sim.GearConfig.StartLevel;
        public int shoesLevel = Sim.GearConfig.StartLevel;
        public int gripLevel = Sim.GearConfig.StartLevel;
        public string outfitId = "";
        /// <summary>Ids of every reward already credited (Sim.RewardLedger), oldest first.</summary>
        public List<string> creditedRewardIds = new List<string>();
        /// <summary>UTC day ("yyyy-MM-dd") that <see cref="adRewardsToday"/> counts.</summary>
        public string adRewardDayKey = "";
        public int adRewardsToday = 0;

        public float musicVolume = 0.62f;
        public float effectsVolume = 0.88f;
        public bool hapticsOn = true;
        public float swipeSensitivity = 0.5f;
        public List<string> unlockedFeatureIds = new List<string>();

        // ---- Retired systems ----
        // Tours, bags and chests, gems, the season pass, the daily reward and the 40-item gear ladder
        // are outside the launch scope. Nothing reads these any more, but MetaGameState writes them
        // back exactly as they were loaded, so a save keeps that progress rather than silently losing it.
        public int gems = 0;
        public int playerLevel = 1;
        public int overallRating = 0;
        public int seasonTier = 0;
        public int seasonXP = 0;
        public int currentTourIndex = 0;
        public List<GearProfileEntry> gear = new List<GearProfileEntry>();
        public List<string> equippedGearIds = new List<string>();
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
        public int chestsSinceEpic = 0;
        public int chestsSinceLegendary = 0;
        public List<int> tourWinsCurrent = new List<int>();
        public List<bool> tourCleared = new List<bool>();
        public bool leftHanded = false;
        public int careerWins = 0;
        public List<int> claimedSeasonPassTiers = new List<int>();
        public string lastDailyRewardDayKey = "";
        public int dailyRewardStreak = 0;

        /// <summary>Brings a just-loaded profile up to CurrentSchemaVersion in place. Idempotent, and a
        /// no-op on anything already current, so ProfileService can call it unconditionally after every
        /// load. Deliberately free of any UnityEngine dependency.</summary>
        public void Migrate()
        {
            if (schemaVersion >= CurrentSchemaVersion) return;

            if (gear == null) gear = new List<GearProfileEntry>();

            // Schema 0 -> 1: rebuild id-keyed entries from the parallel lists.
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
                    });
                }
            }

            // Schema 1 -> 2: a save written before slot levels existed has no paddleLevel key, which
            // JsonUtility cannot tell apart from the default -- so the schema number decides. Each slot
            // starts at the level of the item the player had equipped there, so upgrades already paid
            // for are not lost. The old ladder's levels run 1-10 like the new ones.
            if (schemaVersion < 2)
            {
                paddleLevel = EquippedLevel("paddle_", "carbon_pro");
                shoesLevel = EquippedLevel("shoes_", "swift_step");
                gripLevel = EquippedLevel("grip_", "starter_grip", "warrior_grip");
            }

            schemaVersion = CurrentSchemaVersion;
        }

        /// <summary>Level of the equipped retired-ladder item whose id starts with
        /// <paramref name="prefix"/> or is one of <paramref name="originalIds"/>; the start level if none.</summary>
        private int EquippedLevel(string prefix, params string[] originalIds)
        {
            if (equippedGearIds == null || gear == null) return Sim.GearConfig.StartLevel;
            foreach (string id in equippedGearIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                if (!id.StartsWith(prefix, StringComparison.Ordinal) && Array.IndexOf(originalIds, id) < 0) continue;
                GearProfileEntry entry = gear.Find(g => g != null && g.gearId == id);
                return Sim.GearRules.ClampLevel(entry != null ? entry.level : Sim.GearConfig.StartLevel);
            }
            return Sim.GearConfig.StartLevel;
        }

        private static int At(List<int> list, int index, int fallback)
        {
            return list != null && index < list.Count ? list[index] : fallback;
        }
    }
}
