using System;
using System.Collections.Generic;
using UnityEngine;
using Pickleball.Backend;
using Sim = Pickleball.Sim;

namespace Pickleball.Data
{
    /// <summary>What the result card shows for the match that just finished. Transient.</summary>
    public struct MatchResultSummary
    {
        public Sim.MatchMode mode;
        public bool won;
        public int playerScore;
        public int opponentScore;
        /// <summary>Coins this match actually credited -- 0 if its id had already been paid.</summary>
        public int coinsEarned;
        public int coinBalance;
        public int leaguePointsBefore;
        public int leaguePointsAfter;
        public Sim.LeagueChange leagueChange;

        public int LeaguePointsDelta => leaguePointsAfter - leaguePointsBefore;
    }

    /// <summary>
    /// The player's persistent state, as a read-through cache in front of ProfileService's backend
    /// (PlayFab when configured, else a local file). Every mutation requests a debounced save of the
    /// whole profile, so values changed together -- a purchase's deduction and its new level, a
    /// match's coins and its league points -- are always persisted together.
    ///
    /// The rules themselves (payouts, league thresholds, upgrade prices) live in Pickleball.Sim; this
    /// class only applies them to the live profile.
    /// </summary>
    public static class MetaGameState
    {
        private static int coins = Sim.EconomyConfig.StartingCoins;
        public static int Coins => coins;

        private static int leaguePoints = Sim.LeagueConfig.StartingPoints;
        public static int LeaguePoints => leaguePoints;

        private static string playerName = "ALTAF";
        public static string PlayerName { get => playerName; set { playerName = value; RequestSave(); } }

        public static Sim.League CurrentLeague => Sim.LeagueRules.For(leaguePoints);
        public static int CurrentLeagueIndex => Sim.LeagueRules.IndexFor(leaguePoints);

        /// <summary>League name for a league-points balance -- the player's own, or an opponent's.</summary>
        public static string LeagueName(int points) => Sim.LeagueRules.For(points).Name;

        // ---- Gear ----------------------------------------------------------------------------------

        private static int paddleLevel = Sim.GearConfig.StartLevel;
        private static int shoesLevel = Sim.GearConfig.StartLevel;
        private static int gripLevel = Sim.GearConfig.StartLevel;

        public static int GearLevel(Sim.GearSlot slot)
        {
            switch (slot)
            {
                case Sim.GearSlot.Paddle: return paddleLevel;
                case Sim.GearSlot.Shoes: return shoesLevel;
                default: return gripLevel;
            }
        }

        private static void SetGearLevel(Sim.GearSlot slot, int level)
        {
            level = Sim.GearRules.ClampLevel(level);
            switch (slot)
            {
                case Sim.GearSlot.Paddle: paddleLevel = level; break;
                case Sim.GearSlot.Shoes: shoesLevel = level; break;
                default: gripLevel = level; break;
            }
        }

        /// <summary>The passive stats the player's gear grants -- and, since the AI is matched to the
        /// player, the stats the AI plays with too.</summary>
        public static Sim.LoadoutStats GetLoadoutStats() => Sim.GearRules.Loadout(paddleLevel, shoesLevel, gripLevel);

        /// <summary>Buys the next level of a slot's gear. Atomic -- see Sim.GearRules.TryUpgrade: the
        /// balance check, the deduction and the new level are applied together or not at all, and
        /// land in the same save.</summary>
        public static Sim.PurchaseResult TryUpgradeGear(Sim.GearSlot slot)
        {
            int balance = coins;
            int level = GearLevel(slot);
            Sim.PurchaseResult result = Sim.GearRules.TryUpgrade(ref balance, ref level);
            if (result != Sim.PurchaseResult.Purchased) return result;
            coins = balance;
            SetGearLevel(slot, level);
            RequestSave();
            return result;
        }

        private static string outfitId = OutfitCatalog.DefaultId;
        public static string OutfitId => outfitId;
        public static Outfit CurrentOutfit => OutfitCatalog.Find(outfitId);

        /// <summary>Raised when the player picks a different outfit.</summary>
        public static event Action OnOutfitChanged;

        /// <summary>Cosmetic only: no stat anywhere reads the outfit.</summary>
        public static void SelectOutfit(string id)
        {
            if (!OutfitCatalog.Exists(id) || id == outfitId) return;
            outfitId = id;
            RequestSave();
            OnOutfitChanged?.Invoke();
        }

        // ---- Match rewards -------------------------------------------------------------------------

        private static readonly Sim.RewardLedger ledger = new Sim.RewardLedger();

        /// <summary>The most recently settled match, for the result card.</summary>
        public static MatchResultSummary LastMatchResult { get; private set; }

        /// <summary>
        /// Pays out a completed match: coins per Sim.MatchModes.Payout, and league points for a
        /// multiplayer match only. Credited once per <paramref name="matchId"/> -- a second call for
        /// the same match reports the result but pays nothing.
        /// </summary>
        public static MatchResultSummary CompleteMatch(string matchId, Sim.MatchMode mode, bool won,
            int playerScore, int opponentScore)
        {
            return Settle(matchId, mode, won, playerScore, opponentScore, Sim.MatchModes.Payout(mode, won));
        }

        /// <summary>Leaving a match before it ends: a multiplayer loss for league points with no
        /// coins, and nothing at all for an AI match.</summary>
        public static MatchResultSummary ForfeitMatch(string matchId, Sim.MatchMode mode, int playerScore, int opponentScore)
        {
            return Settle(matchId, mode, false, playerScore, opponentScore, Sim.MatchModes.ForfeitPayout(mode));
        }

        private static MatchResultSummary Settle(string matchId, Sim.MatchMode mode, bool won,
            int playerScore, int opponentScore, Sim.MatchPayout payout)
        {
            int pointsBefore = leaguePoints;
            int earned = 0;
            if (ledger.TryRecord("match:" + matchId))
            {
                earned = Mathf.Max(0, payout.Coins);
                coins += earned;
                if (Sim.MatchModes.AffectsLeague(mode))
                    leaguePoints = Sim.LeagueRules.Apply(leaguePoints, payout.LeaguePoints);
                RequestSave();
            }

            MatchResultSummary summary = new MatchResultSummary
            {
                mode = mode,
                won = won,
                playerScore = playerScore,
                opponentScore = opponentScore,
                coinsEarned = earned,
                coinBalance = coins,
                leaguePointsBefore = pointsBefore,
                leaguePointsAfter = leaguePoints,
                leagueChange = Sim.LeagueRules.Compare(pointsBefore, leaguePoints),
            };
            LastMatchResult = summary;
            return summary;
        }

        // ---- Rewarded ads --------------------------------------------------------------------------

        private static string adRewardDayKey = "";
        private static int adRewardsToday;

        private static string TodayKey() => DateTime.UtcNow.ToString("yyyy-MM-dd");

        public static int AdRewardsRemainingToday =>
            Sim.AdRewardRules.RemainingToday(adRewardDayKey, adRewardsToday, TodayKey());

        /// <summary>
        /// Credits one rewarded ad the provider has confirmed was watched to the end. Once per
        /// <paramref name="placementId"/>: a repeated completion callback, or a retry reporting the
        /// same ad, pays nothing. Also refused once today's allowance is used up.
        /// </summary>
        public static bool CreditAdReward(string placementId)
        {
            if (AdRewardsRemainingToday <= 0) return false;
            if (!ledger.TryRecord("ad:" + placementId)) return false;
            string today = TodayKey();
            if (adRewardDayKey != today)
            {
                adRewardDayKey = today;
                adRewardsToday = 0;
            }
            adRewardsToday++;
            coins += Sim.EconomyConfig.AdRewardCoins;
            RequestSave();
            return true;
        }

        // ---- Settings and flags --------------------------------------------------------------------

        private static float musicVolume = 0.62f;
        public static float MusicVolume { get => musicVolume; set { musicVolume = value; RequestSave(); } }

        private static float effectsVolume = 0.88f;
        public static float EffectsVolume { get => effectsVolume; set { effectsVolume = value; RequestSave(); } }

        private static bool hapticsOn = true;
        public static bool HapticsOn { get => hapticsOn; set { hapticsOn = value; RequestSave(); } }

        private static float swipeSensitivity = 0.5f;
        public static float SwipeSensitivity { get => swipeSensitivity; set { swipeSensitivity = value; RequestSave(); } }

        private static readonly HashSet<string> unlockedFeatureIds = new HashSet<string>();

        public static bool IsFeatureUnlocked(string featureId)
        {
            return !string.IsNullOrEmpty(featureId) && unlockedFeatureIds.Contains(featureId);
        }

        public static void UnlockFeature(string featureId)
        {
            if (string.IsNullOrEmpty(featureId) || !unlockedFeatureIds.Add(featureId)) return;
            RequestSave();
        }

        private static void RequestSave()
        {
            if (ProfileService.Instance != null) ProfileService.Instance.RequestSave();
        }

        // ---- Persistence ---------------------------------------------------------------------------

        /// <summary>The profile as last loaded. Its retired-system fields (tours, bags, gems...) are
        /// written back untouched on every save, so that progress is kept rather than dropped.</summary>
        private static PlayerProfileData loadedProfile;

        /// <summary>Reads the live state into a serializable snapshot for ProfileService to persist.
        /// Before any load this doubles as the brand-new player's profile.</summary>
        public static PlayerProfileData CaptureSnapshot()
        {
            PlayerProfileData data = loadedProfile != null
                ? JsonUtility.FromJson<PlayerProfileData>(JsonUtility.ToJson(loadedProfile))
                : new PlayerProfileData();

            data.schemaVersion = PlayerProfileData.CurrentSchemaVersion;
            data.playerName = playerName;
            data.coins = coins;
            data.trophies = leaguePoints;
            data.paddleLevel = paddleLevel;
            data.shoesLevel = shoesLevel;
            data.gripLevel = gripLevel;
            data.outfitId = outfitId;
            data.creditedRewardIds = new List<string>(ledger.Entries);
            data.adRewardDayKey = adRewardDayKey;
            data.adRewardsToday = adRewardsToday;
            data.musicVolume = musicVolume;
            data.effectsVolume = effectsVolume;
            data.hapticsOn = hapticsOn;
            data.swipeSensitivity = swipeSensitivity;
            data.unlockedFeatureIds = new List<string>(unlockedFeatureIds);
            return data;
        }

        /// <summary>Restores a loaded (or freshly seeded) profile into the live state, bypassing
        /// RequestSave so restoring a save doesn't immediately re-trigger one.</summary>
        public static void ApplySnapshot(PlayerProfileData data)
        {
            if (data == null) return;
            loadedProfile = JsonUtility.FromJson<PlayerProfileData>(JsonUtility.ToJson(data));

            playerName = string.IsNullOrEmpty(data.playerName) ? playerName : data.playerName;
            coins = Mathf.Max(0, data.coins);
            leaguePoints = Mathf.Max(Sim.LeagueConfig.MinimumPoints, data.trophies);
            paddleLevel = Sim.GearRules.ClampLevel(data.paddleLevel);
            shoesLevel = Sim.GearRules.ClampLevel(data.shoesLevel);
            gripLevel = Sim.GearRules.ClampLevel(data.gripLevel);
            outfitId = OutfitCatalog.Exists(data.outfitId) ? data.outfitId : OutfitCatalog.DefaultId;
            ledger.Load(data.creditedRewardIds);
            adRewardDayKey = data.adRewardDayKey ?? "";
            adRewardsToday = Mathf.Max(0, data.adRewardsToday);
            musicVolume = data.musicVolume;
            effectsVolume = data.effectsVolume;
            hapticsOn = data.hapticsOn;
            swipeSensitivity = data.swipeSensitivity;
            unlockedFeatureIds.Clear();
            if (data.unlockedFeatureIds != null)
            {
                foreach (string featureId in data.unlockedFeatureIds) unlockedFeatureIds.Add(featureId);
            }
            OnOutfitChanged?.Invoke();
        }
    }
}
