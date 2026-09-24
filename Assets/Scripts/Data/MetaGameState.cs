using System;
using System.Collections.Generic;
using UnityEngine;
using Pickleball.UI;
using Pickleball.Backend;
using Sim = Pickleball.Sim;

namespace Pickleball.Data
{
    public enum GearType { Paddle, Shoes, Grip, Accessory }

    public class GearItem
    {
        public string id;
        public string name;
        public GearType type;
        /// <summary>Procedural icon id. Was an emoji codepoint, which never rasterised in the
        /// project's font and left every gear slot and card drawing a blank square.</summary>
        public IconId icon;
        public Rarity rarity;
        /// <summary>Ladder rung, 1-10 (Docs/GearProgression.md#3-item-ladders). Drives ItemPowerScore
        /// and therefore every stat and upgrade cost via GearProgressionCurve -- see GearCatalog.
        /// Not shown directly in UI; rarity is the tier's display band.</summary>
        public int tier;
        public int level;
        public int power;
        public int spin;
        /// <summary>Named `speed` on the live item -- matches GearProgressionCurve/LoadoutStats and
        /// the Speed stat throughout Docs/GearProgression.md. The persisted field stays
        /// GearProfileEntry.agility (see its declaration): that's a frozen wire-format name, not a
        /// live one, and renaming it would silently drop every existing save's Speed progress back to
        /// the catalog default on next load.</summary>
        public int speed;
        public int serve;
        /// <summary>New stats in the 6-stat model (Docs/GearProgression.md#1-stats). Both are 0 on
        /// any item whose slot doesn't grant them -- see GearProgressionCurve.ComputeStats -- not a
        /// display filter, the item genuinely carries no value there.</summary>
        public int control;
        public int stamina;
        public int cardsCollected;
        public int cardsNeeded;
        public int upgradeCostCoins;
        public bool isStarter;
    }

    public enum BagSlotState { Empty, Ready, Unlocking, Sealed }

    public class BagSlot
    {
        public string bagId;
        public BagSlotState state;
        public long unlockCompleteUnixSeconds;
    }

    public class RewardEntry
    {
        public IconId icon;
        public string name;
        public Rarity rarity;
        public int amount;
        public string gearId;
    }

    public class RewardBundle
    {
        /// <summary>The chest's display name (e.g. "MATCH BAG") -- ChestOpeningScreen shows this as
        /// its title instead of a hardcoded string, now that a bag can be more than one chest type.</summary>
        public string chestName;
        public readonly List<RewardEntry> entries = new List<RewardEntry>();
    }

    public class TourInfo
    {
        public string name;
        public IconId icon;
        public int entryCoins;
        public int rewardCoins;
        public int rewardTrophies;
        public int winsRequired;
        public int winsCurrent;
        public bool cleared;
        public int trophiesNeeded;
    }

    public class LeagueRow
    {
        public string name;
        public int trophies;
        public bool isPlayer;
    }

    /// <summary>
    /// Meta-game state for the Kitchen Line Arcade screens. Acts as a read-through cache in front of
    /// whatever ProfileService.Instance's backend is (PlayFab once configured, otherwise a local
    /// JSON file) -- see the Kitchen Line Netcode plan, phase 1. Every screen keeps reading/writing
    /// these same static members exactly as before; the persistence is invisible to them.
    /// Gear/Tours/League catalog content (name, icon, costs, requirements) stays hardcoded here, since
    /// nothing currently mutates it -- only the player-progress fields (coins/gems/trophies/season/gear
    /// level/tour wins) round-trip through CaptureSnapshot/ApplySnapshot.
    /// </summary>
    public static class MetaGameState
    {
        private static int coins = 12480;
        public static int Coins { get => coins; private set { coins = value; RequestSave(); } }

        private static int gems = 240;
        public static int Gems { get => gems; private set { gems = value; RequestSave(); } }

        private static int trophies = 2140;
        public static int Trophies { get => trophies; private set { trophies = value; RequestSave(); } }

        private static string playerName = "ALTAF";
        public static string PlayerName { get => playerName; set { playerName = value; RequestSave(); } }

        /// <summary>Career wins -- monotonic, a loss never lowers it. Drives PlayerLevel and is the
        /// input the vanity level needs that trophies (which go down) cannot safely be.</summary>
        private static int careerWins = 0;
        public static int CareerWins => careerWins;
        public static void RecordCareerWin() { careerWins++; RequestSave(); }

        /// <summary>Was a flat stored counter seeded at 24 that nothing in the game ever incremented --
        /// a decal on the lobby avatar, not a progression signal. Now computed from things that
        /// actually advance: the season tier and career wins. No setter, nothing to persist (like
        /// OverallRating); CaptureSnapshot still writes the current value for a human reading the JSON.</summary>
        public static int PlayerLevel => 1 + seasonTier + careerWins / 3;

        /// <summary>Computed live from equipped gear (Docs/GearProgression.md#7-overall-rating-and-matchmaking)
        /// -- was a flat stored counter bumped by +4 on every upgrade, which drifted from the actual
        /// loadout the moment a player equipped a different already-owned item (OVR simply didn't
        /// change) and never reflected gear at all for a fresh save. No setter and nothing to persist:
        /// CaptureSnapshot still writes the current value into PlayerProfileData.overallRating for a
        /// human reading the raw JSON, but ApplySnapshot never reads it back.</summary>
        public static int OverallRating
        {
            get
            {
                List<GearItem> equipped = new List<GearItem>();
                foreach (string id in equippedGearIds)
                {
                    GearItem item = FindGear(id);
                    if (item != null) equipped.Add(item);
                }
                return GearProgressionCurve.ComputeOverallRating(equipped);
            }
        }

        private static int seasonTier = 12;
        public static int SeasonTier { get => seasonTier; set { seasonTier = value; RequestSave(); } }

        private static int seasonXP = 340;
        public static int SeasonXP { get => seasonXP; set { seasonXP = value; RequestSave(); } }

        public static int SeasonXPPerTier = 500;

        /// <summary>Fraction of the way to the next tier, always 0..1. The lobby and pass meters used
        /// to divide raw SeasonXP by SeasonXPPerTier with no clamp, so once XP accrued past a tier
        /// (which it always did -- nothing rolled it over) both bars filled past 100% and kept going.</summary>
        public static float SeasonTierProgress01 =>
            Mathf.Clamp01((float)seasonXP / Mathf.Max(1, SeasonXPPerTier));

        /// <summary>Adds season XP and rolls it over into whole tiers. This is the only thing that
        /// advances SeasonTier -- before it, match results added to SeasonXP and nothing ever
        /// compared it to the per-tier cost or bumped the tier.</summary>
        public static void AddSeasonXp(int amount)
        {
            if (amount <= 0) return;
            seasonXP += amount;
            while (seasonXP >= SeasonXPPerTier)
            {
                seasonXP -= SeasonXPPerTier;
                seasonTier++;
            }
            RequestSave();
        }

        /// <summary>Season-pass free-track tiers already claimed. The "CLAIM FREE" button used to call
        /// AddCoins(500) with no record of the claim, so returning to the screen and tapping again
        /// paid again, without limit.</summary>
        private static readonly HashSet<int> claimedSeasonPassTiers = new HashSet<int>();
        public static bool IsSeasonPassTierClaimed(int tier) => claimedSeasonPassTiers.Contains(tier);

        /// <summary>Claims the current season tier's free reward exactly once. Returns the granted
        /// bundle (also queued for the lobby banner) or null if this tier was already claimed.</summary>
        public static int SeasonRewardCoins(int tier) => 200 + Mathf.Max(1, tier) * 25;
        public static int SeasonRewardGems(int tier) => tier % 3 == 0 ? 15 : 0;

        public static RewardBundle ClaimSeasonPassReward(int tier = -1)
        {
            if (tier < 0) tier = seasonTier;
            if (tier < 1 || tier > seasonTier) return null;
            if (!claimedSeasonPassTiers.Add(tier)) return null;

            int coins = SeasonRewardCoins(tier);
            int gems = SeasonRewardGems(tier);

            RewardBundle bundle = new RewardBundle { chestName = "SEASON TIER " + tier };
            bundle.entries.Add(new RewardEntry { icon = IconId.Coin, name = "COINS", rarity = Rarity.Common, amount = coins });
            AddCoins(coins);
            if (gems > 0)
            {
                bundle.entries.Add(new RewardEntry { icon = IconId.Gem, name = "GEMS", rarity = Rarity.Common, amount = gems });
                AddGems(gems);
            }
            pendingHomeRewardFeedback = bundle;
            RequestSave();
            return bundle;
        }

        private static int currentTourIndex = 3;
        public static int CurrentTourIndex { get => currentTourIndex; set { currentTourIndex = value; RequestSave(); } }

        private static readonly List<string> equippedGearIds = new List<string>();
        public static IReadOnlyList<string> EquippedGearIds => equippedGearIds;

        private static readonly List<BagSlot> bagSlots = new List<BagSlot>();
        public static IReadOnlyList<BagSlot> BagSlots => bagSlots;

        private static string lastLeagueResultWeekKey = "";
        private static RewardBundle pendingHomeRewardFeedback;

        /// <summary>The payout from the most recently finished match. Written by
        /// ScreenManager.HandleMatchEnded (which also applies it) so GameplayHUD's result modal can
        /// show the real numbers instead of its own hardcoded copy. Transient -- not persisted.</summary>
        public static MatchReward LastMatchReward;
        private static readonly HashSet<string> unlockedFeatureIds = new HashSet<string>();

        private static PityState pityState;
        public static int ChestsSinceEpic => pityState.chestsSinceEpic;
        public static int ChestsSinceLegendary => pityState.chestsSinceLegendary;

        /// <summary>The full 40-item ladder (Docs/GearProgression.md#3-item-ladders) -- 10 rungs
        /// across each of the 4 slots, generated from GearCatalog rather than hand-typed. carbon_pro,
        /// swift_step, starter_grip and warrior_grip are the same 4 ids the game always shipped with;
        /// their historical level/cards-collected values are preserved as GearCatalog's seed state,
        /// so returning players are unaffected -- ApplySnapshot overwrites these from the save anyway.</summary>
        public static readonly List<GearItem> Gear = GearCatalog.Build();

        public static readonly List<TourInfo> Tours = new List<TourInfo>
        {
            new TourInfo { name = "SUNRISE PARK", icon = IconId.Sun, entryCoins = 50, rewardCoins = 120, rewardTrophies = 10, winsRequired = 15, winsCurrent = 15, cleared = true, trophiesNeeded = 0 },
            new TourInfo { name = "RIVERSIDE COURTS", icon = IconId.Wave, entryCoins = 50, rewardCoins = 160, rewardTrophies = 16, winsRequired = 20, winsCurrent = 20, cleared = true, trophiesNeeded = 0 },
            new TourInfo { name = "GARDEN CLUB", icon = IconId.Leaf, entryCoins = 50, rewardCoins = 190, rewardTrophies = 22, winsRequired = 22, winsCurrent = 22, cleared = true, trophiesNeeded = 0 },
            new TourInfo { name = "MIAMI BEACH", icon = IconId.Palm, entryCoins = 50, rewardCoins = 220, rewardTrophies = 28, winsRequired = 25, winsCurrent = 16, cleared = false, trophiesNeeded = 0 },
            new TourInfo { name = "DESERT OPEN", icon = IconId.Dune, entryCoins = 60, rewardCoins = 260, rewardTrophies = 32, winsRequired = 25, winsCurrent = 0, cleared = false, trophiesNeeded = 2600 },
            new TourInfo { name = "CENTRE COURT", icon = IconId.Tent, entryCoins = 70, rewardCoins = 320, rewardTrophies = 40, winsRequired = 30, winsCurrent = 0, cleared = false, trophiesNeeded = 3400 },
        };

        public static readonly List<LeagueRow> League = new List<LeagueRow>
        {
            new LeagueRow { name = "K. NAKAMURA", trophies = 2412 },
            new LeagueRow { name = "R. VEGA", trophies = 2208 },
            new LeagueRow { name = "M. OKONKWO", trophies = 2186 },
            new LeagueRow { name = "ALTAF · YOU", trophies = 2140, isPlayer = true },
            new LeagueRow { name = "S. DUBOIS", trophies = 2090 },
            new LeagueRow { name = "J. TANAKA", trophies = 2044 },
            new LeagueRow { name = "P. SILVA", trophies = 1988 },
        };

        private static bool leagueDrifted;

        /// <summary>Standings with the player's row synced to the live trophy count and the whole
        /// board re-sorted -- LeagueScreen used to draw the seed list verbatim, so the player's own
        /// row stayed frozen at 2,140 no matter how many ranked matches they won, and never changed
        /// rank. The seven rivals also drift a few trophies once per app run so the board isn't
        /// identical every visit.</summary>
        public static List<LeagueRow> BuildLeagueStandings()
        {
            if (!leagueDrifted)
            {
                leagueDrifted = true;
                foreach (LeagueRow r in League)
                {
                    if (!r.isPlayer) r.trophies = Mathf.Max(200, r.trophies + UnityEngine.Random.Range(-28, 29));
                }
            }

            List<LeagueRow> rows = new List<LeagueRow>();
            foreach (LeagueRow r in League)
            {
                rows.Add(new LeagueRow
                {
                    name = r.isPlayer ? PlayerName + " · YOU" : r.name,
                    trophies = r.isPlayer ? Trophies : r.trophies,
                    isPlayer = r.isPlayer,
                });
            }
            rows.Sort((a, b) => b.trophies.CompareTo(a.trophies));
            return rows;
        }

        /// <summary>Trophy-band tier name -- the LeagueScreen crest and the matchmaking cards showed a
        /// hardcoded "PLATINUM II" regardless of the actual trophy count.</summary>
        public static string LeagueTierName(int trophies)
        {
            string band;
            if (trophies < 1000) band = "SILVER";
            else if (trophies < 1600) band = "GOLD";
            else if (trophies < 2200) band = "PLATINUM";
            else if (trophies < 2800) band = "DIAMOND";
            else if (trophies < 3400) band = "MASTER";
            else return "CHAMPION";

            int within = Mathf.Clamp(((trophies % 600) / 200), 0, 2); // 0,1,2 -> III,II,I
            string[] numerals = { "III", "II", "I" };
            return band + " " + numerals[within];
        }

        private static float musicVolume = 0.62f;
        public static float MusicVolume { get => musicVolume; set { musicVolume = value; RequestSave(); } }

        private static float effectsVolume = 0.88f;
        public static float EffectsVolume { get => effectsVolume; set { effectsVolume = value; RequestSave(); } }

        private static bool hapticsOn = true;
        public static bool HapticsOn { get => hapticsOn; set { hapticsOn = value; RequestSave(); } }

        private static float swipeSensitivity = 0.5f;
        public static float SwipeSensitivity { get => swipeSensitivity; set { swipeSensitivity = value; RequestSave(); } }

        private static bool leftHanded = false;
        public static bool LeftHanded { get => leftHanded; set { leftHanded = value; RequestSave(); } }

        public static void AddCoins(int amount) { Coins = Mathf.Max(0, Coins + amount); }

        public static bool SpendCoins(int amount)
        {
            if (Coins < amount) return false;
            Coins -= amount;
            return true;
        }

        public static void AddGems(int amount) { Gems = Mathf.Max(0, Gems + amount); }

        public static bool SpendGems(int amount)
        {
            if (amount < 0 || Gems < amount) return false;
            Gems -= amount;
            return true;
        }

        public static void AddTrophies(int amount) { Trophies = Mathf.Max(0, Trophies + amount); }

        public static bool IsMaxLevel(GearItem item)
        {
            return item != null && item.level >= GearProgressionCurve.MaxLevel;
        }

        public static bool CanUpgrade(GearItem item)
        {
            return item != null && !IsMaxLevel(item) && item.cardsCollected >= item.cardsNeeded && Coins >= item.upgradeCostCoins;
        }

        public static bool TryUpgradeGear(GearItem item)
        {
            if (!CanUpgrade(item) || !SpendCoins(item.upgradeCostCoins)) return false;
            item.cardsCollected -= item.cardsNeeded;
            // Recomputes stats and the next upgrade's cost from GearProgressionCurve rather than a
            // flat per-level add -- Docs/GearProgression.md#4-the-power-curve. Shared with GearCatalog's
            // own seeding so a fresh item and an upgraded one are never computed differently.
            GearCatalog.ApplyLevel(item, item.level + 1);
            // OverallRating is now computed live from equipped gear -- see its declaration -- so
            // upgrading (which changes the equipped item's own tier/level inputs) already moves it
            // with no explicit bump needed here.
            RequestSave();
            return true;
        }

        public static GearItem FindGear(string id)
        {
            return Gear.Find(g => g.id == id);
        }

        public static GearItem GetEquipped(GearType type)
        {
            foreach (string id in equippedGearIds)
            {
                GearItem item = FindGear(id);
                if (item != null && item.type == type) return item;
            }
            return null;
        }

        /// <summary>Sums the equipped loadout's six stats into the plain struct ShotSim's
        /// diminishing-returns bindings consume (Docs/GearProgression.md#1-stats). Returns
        /// Sim.LoadoutStats.Neutral (all zero) with no gear equipped -- every ShotSim binding treats
        /// that as a true no-op, so callers never need to special-case an empty loadout.
        ///
        /// Returns the real, unclamped total. GearLoadoutScreen's ATTRIBUTES panel reuses this and
        /// clamps each value to 0-100 itself for its 10-tick display -- clamping in here would
        /// silently cap a maxed loadout's actual effect on the sim.</summary>
        public static Sim.LoadoutStats GetLoadoutStats()
        {
            Sim.LoadoutStats stats = new Sim.LoadoutStats();
            foreach (string id in equippedGearIds)
            {
                GearItem item = FindGear(id);
                if (item == null) continue;
                stats.power += item.power;
                stats.spin += item.spin;
                stats.control += item.control;
                stats.speed += item.speed;
                stats.serve += item.serve;
                stats.stamina += item.stamina;
            }
            return stats;
        }

        public static bool IsEquipped(GearItem item)
        {
            return item != null && equippedGearIds.Contains(item.id);
        }

        public static void Equip(GearItem item)
        {
            if (item == null) return;
            equippedGearIds.RemoveAll(id =>
            {
                GearItem current = FindGear(id);
                return current == null || current.type == item.type;
            });
            equippedGearIds.Add(item.id);
            RequestSave();
        }

        public static bool HasStarterEquipment()
        {
            foreach (string id in equippedGearIds)
            {
                GearItem item = FindGear(id);
                if (item != null && item.isStarter) return true;
            }
            return false;
        }

        public static bool AreBagSlotsFull()
        {
            RefreshBagTimers();
            return bagSlots.Count > 0 && bagSlots.TrueForAll(s => s.state != BagSlotState.Empty);
        }

        public static bool HasReadyBag()
        {
            RefreshBagTimers();
            return bagSlots.Exists(s => s.state == BagSlotState.Ready);
        }

        public static void StartBagUnlock(int index)
        {
            if (index < 0 || index >= bagSlots.Count || bagSlots[index].state != BagSlotState.Sealed) return;
            if (bagSlots.Exists(s => s.state == BagSlotState.Unlocking)) return;
            // Was always a hardcoded 2h regardless of the slot's actual bagId -- the only caller
            // (LobbyScreen) never passed the old durationSeconds parameter, so every chest type
            // silently unlocked on the same timer. Now reads the real duration from ChestCatalog.
            ChestDefinition? chest = ChestCatalog.Find(bagSlots[index].bagId);
            int durationSeconds = (chest ?? ChestCatalog.Fallback).unlockSeconds;
            bagSlots[index].state = BagSlotState.Unlocking;
            bagSlots[index].unlockCompleteUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + durationSeconds;
            RequestSave();
        }

        public static void AddMatchBag() { AddBag("match_bag"); }

        /// <summary>Granted on clearing a tour stage -- see RecordTourWin. Docs/GearProgression.md#6-acquisition-b.</summary>
        public static void AddTourCrate() { AddBag("tour_crate"); }

        /// <summary>Granted once per week from the startup league-result flow -- see
        /// ScreenManager.CompleteStartupResults. Docs/GearProgression.md#6-acquisition-b.</summary>
        public static void AddLeagueChest() { AddBag("league_chest"); }

        /// <summary>Granted on a successful shop purchase (90 gems) -- see ShopScreen's Epic Chest
        /// deal. Docs/GearProgression.md#6-acquisition-b.</summary>
        public static void AddEpicChest() { AddBag("epic_chest"); }

        /// <summary>Granted by ShopScreen's standing "PREMIUM" IAP offer -- real money, not gems, so
        /// unlike AddEpicChest there is no currency deduction to gate on here; the buy button spends
        /// nothing in-game. Docs/GearProgression.md#6-acquisition-b.</summary>
        public static void AddLegendaryChest() { AddBag("legendary_chest"); }

        /// <summary>Common body for every "grant a sealed chest into the first empty bag slot" entry
        /// point. Silently no-ops if every slot is full -- same behavior AddMatchBag always had; a
        /// win/purchase/reward that arrives while the player's bags are full is simply not banked,
        /// there is nowhere to queue it.</summary>
        private static void AddBag(string chestId)
        {
            BagSlot empty = bagSlots.Find(s => s.state == BagSlotState.Empty);
            if (empty == null) return;
            empty.bagId = chestId;
            empty.state = BagSlotState.Sealed;
            empty.unlockCompleteUnixSeconds = 0;
            RequestSave();
        }

        /// <summary>Grants a shop purchase's contents and queues the lobby reward banner, mirroring
        /// OpenReadyBag's own grant loop. Coins/gems are applied here and gear entries add cards to
        /// the named item -- nothing else grants a store bundle. pendingHomeRewardFeedback then drives
        /// LobbyScreen.BuildRewardFeedback on the next HOME navigation. Used by ShopScreen's featured
        /// bundle and coin daily-deals, which previously spent currency (or real money) and granted
        /// nothing -- Docs/GearProgression.md#16.</summary>
        public static void GrantStoreBundle(RewardBundle bundle)
        {
            if (bundle == null) return;
            foreach (RewardEntry reward in bundle.entries)
            {
                if (!string.IsNullOrEmpty(reward.gearId)) ApplyGearCardReward(reward);
                else if (reward.icon == IconId.Coin) AddCoins(reward.amount);
                else if (reward.icon == IconId.Gem) AddGems(reward.amount);
            }
            pendingHomeRewardFeedback = bundle;
            RequestSave();
        }

        /// <summary>Adds card progress to a gear item -- unless the item is already maxed, in which
        /// case the cards would just be destroyed on arrival (TryUpgradeGear refuses past level 10 and
        /// nothing else consumes them). Converts them to coins at a rarity-scaled rate instead and
        /// rewrites `reward` in place so the lobby banner reports what was actually granted.</summary>
        private static void ApplyGearCardReward(RewardEntry reward)
        {
            GearItem gear = FindGear(reward.gearId);
            if (gear == null) return;

            if (!IsMaxLevel(gear))
            {
                gear.cardsCollected += reward.amount;
                return;
            }

            int coins = reward.amount * CoinsPerSurplusCard(gear.rarity);
            reward.icon = IconId.Coin;
            reward.name = gear.name + " MAXED → COINS";
            reward.rarity = Rarity.Common;
            reward.gearId = null;
            reward.amount = coins;
            AddCoins(coins);
        }

        private static int CoinsPerSurplusCard(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Legendary: return 400;
                case Rarity.Epic: return 150;
                case Rarity.Rare: return 55;
                default: return 20;
            }
        }

        /// <summary>The player's equipped item in a slot, or that slot's historical starter if nothing
        /// is equipped -- so a slot-targeted card grant (ShopScreen's PADDLE/SHOES deals) always has a
        /// concrete target.</summary>
        public static GearItem EquippedOrDefault(GearType type)
        {
            GearItem equipped = GetEquipped(type);
            if (equipped != null) return equipped;
            switch (type)
            {
                case GearType.Paddle: return FindGear("carbon_pro");
                case GearType.Shoes: return FindGear("swift_step");
                case GearType.Grip: return FindGear("starter_grip");
                default: return Gear.Find(g => g.type == type);
            }
        }

        /// <summary>Advances the current tour's win count on a tour-mode match win and grants a Tour
        /// Crate the moment it's cleared -- Docs/GearProgression.md#6-acquisition-b's "Tour Crate:
        /// clear tour stage". Previously nothing in the game ever advanced TourInfo.winsCurrent or set
        /// .cleared at all: HandleMatchEnded granted coins/trophies/XP/a Match Bag on every tour win,
        /// but tour progress itself was static seed data that never actually moved through play. A
        /// no-op on an already-cleared tour (replaying one doesn't re-grant its one-time chest) or once
        /// this call itself reaches the requirement.</summary>
        public static void RecordTourWin()
        {
            TourInfo tour = CurrentTour;
            if (tour.cleared) return;
            tour.winsCurrent = Mathf.Min(tour.winsCurrent + 1, tour.winsRequired);
            if (tour.winsCurrent >= tour.winsRequired)
            {
                tour.cleared = true;
                AddTourCrate();
                // Clearing a stage is one of the game's few non-store gem sources -- gems otherwise
                // only ever entered play by being bought (audit 2026-08-27).
                AddGems(20);
            }
            RequestSave();
        }

        public static RewardBundle OpenReadyBag(int index = -1)
        {
            RefreshBagTimers();
            BagSlot ready = index < 0 ? bagSlots.Find(s => s.state == BagSlotState.Ready)
                : index < bagSlots.Count && bagSlots[index].state == BagSlotState.Ready ? bagSlots[index] : null;
            if (ready == null) return null;

            ChestDefinition chest = ChestCatalog.Find(ready.bagId) ?? ChestCatalog.Fallback;

            ready.bagId = "";
            ready.state = BagSlotState.Empty;
            ready.unlockCompleteUnixSeconds = 0;

            // Chest rolls deliberately use UnityEngine.Random rather than Pickleball.Sim.DeterministicRandom
            // -- see ChestRoller.Roll's own doc comment for why.
            ChestRollResult roll = ChestRoller.Roll(chest, UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value, ref pityState);

            RewardBundle bundle = new RewardBundle { chestName = chest.name };

            if (roll.coinBonus > 0)
                bundle.entries.Add(new RewardEntry { icon = IconId.Coin, name = "COINS", rarity = Rarity.Common, amount = roll.coinBonus });

            GearItem picked = PickRandomGearOfRarity(roll.rarity);
            if (picked != null)
                bundle.entries.Add(new RewardEntry { icon = picked.icon, name = picked.name, rarity = picked.rarity, amount = roll.cardCount, gearId = picked.id });

            foreach (RewardEntry reward in bundle.entries)
            {
                if (!string.IsNullOrEmpty(reward.gearId)) ApplyGearCardReward(reward);
                else if (reward.icon == IconId.Coin) AddCoins(reward.amount);
            }
            pendingHomeRewardFeedback = bundle;
            RequestSave();
            return bundle;
        }

        /// <summary>Picks a catalog item from a rarity band, weighted hard toward gear the player has
        /// equipped or already started leveling. A flat uniform pick across all 10 items in the band
        /// (Docs/GearProgression.md#3-item-ladders) divided every drop by ten and meant cards almost
        /// never landed on something the player was actually building -- the core reason the card
        /// economy could not complete (audit 2026-08-27). Returns null only if the catalog is empty.</summary>
        private static GearItem PickRandomGearOfRarity(Rarity rarity)
        {
            List<GearItem> candidates = Gear.FindAll(g => g.rarity == rarity);
            if (candidates.Count == 0) return null;

            float[] weights = new float[candidates.Count];
            float total = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                GearItem g = candidates[i];
                float w = 1f;
                if (IsEquipped(g)) w += 9f;
                else if (g.cardsCollected > 0 || g.level > 1) w += 4f;
                else if (GetEquipped(g.type) == null) w += 1.5f; // nothing in that slot -> a real candidate to equip
                weights[i] = w;
                total += w;
            }

            float roll = UnityEngine.Random.value * total;
            for (int i = 0; i < candidates.Count; i++)
            {
                roll -= weights[i];
                if (roll <= 0f) return candidates[i];
            }
            return candidates[candidates.Count - 1];
        }

        public static RewardBundle ConsumeHomeRewardFeedback()
        {
            RewardBundle bundle = pendingHomeRewardFeedback;
            pendingHomeRewardFeedback = null;
            return bundle;
        }

        public static void RefreshBagTimers()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            bool changed = false;
            foreach (BagSlot slot in bagSlots)
            {
                if (slot.state == BagSlotState.Unlocking && slot.unlockCompleteUnixSeconds > 0 && slot.unlockCompleteUnixSeconds <= now)
                {
                    slot.state = BagSlotState.Ready;
                    slot.unlockCompleteUnixSeconds = 0;
                    changed = true;
                }
            }
            if (changed) RequestSave();
        }

        public static string BagTimerLabel(BagSlot slot)
        {
            if (slot == null) return "";
            if (slot.state == BagSlotState.Ready) return "OPEN";
            if (slot.state == BagSlotState.Empty) return "EMPTY";
            if (slot.state == BagSlotState.Sealed) return "START";
            long left = Math.Max(0, slot.unlockCompleteUnixSeconds - DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            return string.Format("{0}h {1:00}m", left / 3600, (left % 3600) / 60);
        }

        public static bool ShouldShowStartupResults => lastLeagueResultWeekKey != CurrentWeekKey();

        public static void MarkStartupResultsSeen()
        {
            lastLeagueResultWeekKey = CurrentWeekKey();
            RequestSave();
        }

        public static bool IsFeatureUnlocked(string featureId)
        {
            return !string.IsNullOrEmpty(featureId) && unlockedFeatureIds.Contains(featureId);
        }

        public static void UnlockFeature(string featureId)
        {
            if (string.IsNullOrEmpty(featureId) || !unlockedFeatureIds.Add(featureId)) return;
            RequestSave();
        }

        private static string CurrentWeekKey()
        {
            DateTime epoch = new DateTime(2020, 1, 6, 0, 0, 0, DateTimeKind.Utc);
            long week = (long)(DateTime.UtcNow.Date - epoch).TotalDays / 7;
            return week.ToString();
        }

        /// <summary>
        /// Which season the calendar is in. Seasons are one month long and numbered from the same
        /// 2020-01-06 epoch the weekly league reset uses.
        ///
        /// Derived rather than stored: nothing on the profile tracks a season number, and adding one
        /// would change the save format for a value the calendar already answers. The season-complete
        /// screen only needs to know when this number changes, and it remembers the last one it
        /// showed in PlayerPrefs — see ScreenManager.MaybeShowSeasonComplete.
        /// </summary>
        public static int CurrentSeasonNumber
        {
            get
            {
                // Anchored to the game's own season 1, not to the 2020 epoch the weekly league
                // reset uses — counting from 2020 made a fresh install open on "SEASON 80". Move
                // this to the real launch month when there is one.
                DateTime epoch = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                DateTime now = DateTime.UtcNow;
                int months = (now.Year - epoch.Year) * 12 + (now.Month - epoch.Month);
                return Mathf.Max(1, months + 1);
            }
        }

        // ---- Daily reward -------------------------------------------------------------------------
        // The lobby had bag timers and nothing else time-based -- no login grant, no refresh cadence,
        // no reason to open the app on a given day. This is the same day-key shape as the weekly
        // startup-results check above, one grade finer.
        private static string lastDailyRewardDayKey = "";
        private static int dailyRewardStreak = 0;
        public static int DailyRewardStreak => dailyRewardStreak;

        private static string CurrentDayKey() => DateTime.UtcNow.ToString("yyyy-MM-dd");

        public static bool CanClaimDailyReward => lastDailyRewardDayKey != CurrentDayKey();

        /// <summary>The 1-7 position in the current reward week -- what the lobby shows as "DAY n" and
        /// what ClaimDailyReward pays against. A broken streak resets this to 1.</summary>
        public static int DailyRewardDay => ((Mathf.Max(0, dailyRewardStreak - 1)) % 7) + 1;

        /// <summary>Grants today's login reward once per UTC day. Coins scale with the day-in-week,
        /// gems land on day 3, 5 and 7, and a claimed day 7 also drops a Match Bag. Returns the bundle
        /// (queued for the lobby banner) or null if already claimed today.</summary>
        public static RewardBundle ClaimDailyReward()
        {
            if (!CanClaimDailyReward) return null;

            string yesterday = DateTime.UtcNow.Date.AddDays(-1).ToString("yyyy-MM-dd");
            dailyRewardStreak = (lastDailyRewardDayKey == yesterday) ? dailyRewardStreak + 1 : 1;
            lastDailyRewardDayKey = CurrentDayKey();

            int day = DailyRewardDay;
            int coins = 100 + day * 40;
            int gems = (day == 7) ? 25 : (day == 3 || day == 5) ? 6 : 0;

            RewardBundle bundle = new RewardBundle { chestName = "DAILY REWARD · DAY " + day };
            bundle.entries.Add(new RewardEntry { icon = IconId.Coin, name = "COINS", rarity = Rarity.Common, amount = coins });
            AddCoins(coins);
            if (gems > 0)
            {
                bundle.entries.Add(new RewardEntry { icon = IconId.Gem, name = "GEMS", rarity = Rarity.Common, amount = gems });
                AddGems(gems);
            }
            if (day == 7) AddMatchBag();

            pendingHomeRewardFeedback = bundle;
            RequestSave();
            return bundle;
        }

        // ---- Bag unlock skip (gem sink) ---------------------------------------------------------
        /// <summary>Gems to finish an unlocking bag now. ~1 gem per 6 remaining minutes, min 1 -- the
        /// standard "skip the timer" sink, and gems badly needed a second one (the 90-gem Epic Chest
        /// deal was the only in-game gem use).</summary>
        public static int BagSkipGemCost(BagSlot slot)
        {
            if (slot == null || slot.state != BagSlotState.Unlocking) return 0;
            long left = Math.Max(0, slot.unlockCompleteUnixSeconds - DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            return Mathf.Max(1, Mathf.CeilToInt(left / 360f));
        }

        public static bool TrySkipBagUnlock(int index)
        {
            if (index < 0 || index >= bagSlots.Count) return false;
            BagSlot slot = bagSlots[index];
            if (slot.state != BagSlotState.Unlocking) return false;
            int cost = BagSkipGemCost(slot);
            if (!SpendGems(cost)) return false;
            slot.state = BagSlotState.Ready;
            slot.unlockCompleteUnixSeconds = 0;
            RequestSave();
            return true;
        }

        public static TourInfo CurrentTour
        {
            get { return Tours[Mathf.Clamp(CurrentTourIndex, 0, Tours.Count - 1)]; }
        }

        public static int NextLockedTourIndex()
        {
            for (int i = 0; i < Tours.Count; i++)
            {
                if (!Tours[i].cleared && i != CurrentTourIndex) return i;
            }
            return -1;
        }

        private static void RequestSave()
        {
            if (ProfileService.Instance != null) ProfileService.Instance.RequestSave();
        }

        /// <summary>Reads the live static fields into a serializable snapshot for ProfileService to
        /// persist. Also doubles as the "first run" default profile, since the field initializers
        /// above are exactly the historical hardcoded values this class used to ship with.</summary>
        public static PlayerProfileData CaptureSnapshot()
        {
            var data = new PlayerProfileData
            {
                schemaVersion = PlayerProfileData.CurrentSchemaVersion,
                playerName = playerName,
                coins = coins,
                gems = gems,
                trophies = trophies,
                playerLevel = PlayerLevel, // informational only -- computed from seasonTier + careerWins, never read back
                overallRating = OverallRating, // informational only -- see its declaration; never read back
                seasonTier = seasonTier,
                seasonXP = seasonXP,
                currentTourIndex = currentTourIndex,
                musicVolume = musicVolume,
                effectsVolume = effectsVolume,
                hapticsOn = hapticsOn,
                swipeSensitivity = swipeSensitivity,
                leftHanded = leftHanded,
                careerWins = careerWins,
                lastDailyRewardDayKey = lastDailyRewardDayKey,
                dailyRewardStreak = dailyRewardStreak,
            };
            data.claimedSeasonPassTiers.AddRange(claimedSeasonPassTiers);

            foreach (GearItem g in Gear)
            {
                data.gear.Add(new GearProfileEntry
                {
                    gearId = g.id,
                    level = g.level,
                    cardsCollected = g.cardsCollected,
                    cardsNeeded = g.cardsNeeded,
                    power = g.power,
                    spin = g.spin,
                    agility = g.speed,
                    serve = g.serve,
                    control = g.control,
                    stamina = g.stamina,
                    upgradeCostCoins = g.upgradeCostCoins,
                });
            }

            data.equippedGearIds.AddRange(equippedGearIds);
            foreach (BagSlot slot in bagSlots)
            {
                data.bagSlots.Add(new BagSlotProfileData
                {
                    bagId = slot.bagId,
                    state = slot.state.ToString(),
                    unlockCompleteUnixSeconds = slot.unlockCompleteUnixSeconds,
                });
            }
            data.lastLeagueResultWeekKey = lastLeagueResultWeekKey;
            data.unlockedFeatureIds.AddRange(unlockedFeatureIds);
            data.chestsSinceEpic = pityState.chestsSinceEpic;
            data.chestsSinceLegendary = pityState.chestsSinceLegendary;

            foreach (TourInfo t in Tours)
            {
                data.tourWinsCurrent.Add(t.winsCurrent);
                data.tourCleared.Add(t.cleared);
            }

            return data;
        }

        /// <summary>Restores a loaded (or freshly-seeded) profile into the live static fields, bypassing
        /// the property setters so restoring a save doesn't immediately re-trigger a save.</summary>
        public static void ApplySnapshot(PlayerProfileData data)
        {
            if (data == null) return;

            playerName = data.playerName;
            coins = data.coins;
            gems = data.gems;
            trophies = data.trophies;
            // data.playerLevel and data.overallRating are deliberately not read back -- both are now
            // computed live (from seasonTier + careerWins, and from equipped gear respectively).
            seasonTier = data.seasonTier;
            seasonXP = data.seasonXP;
            currentTourIndex = data.currentTourIndex;
            musicVolume = data.musicVolume;
            effectsVolume = data.effectsVolume;
            hapticsOn = data.hapticsOn;
            swipeSensitivity = data.swipeSensitivity;
            leftHanded = data.leftHanded;
            careerWins = Mathf.Max(0, data.careerWins);
            lastDailyRewardDayKey = data.lastDailyRewardDayKey ?? "";
            dailyRewardStreak = Mathf.Max(0, data.dailyRewardStreak);
            claimedSeasonPassTiers.Clear();
            if (data.claimedSeasonPassTiers != null)
            {
                foreach (int tier in data.claimedSeasonPassTiers) claimedSeasonPassTiers.Add(tier);
            }

            // Matched by id, so catalog growth and reordering are both safe: an entry naming gear that
            // no longer exists is dropped, and gear the save has never heard of keeps its catalog
            // defaults, which is exactly how a newly added item should arrive. A -1 stat means the
            // (migrated schema 0) profile never stored that field -- leave the catalog value alone.
            if (data.gear != null)
            {
                foreach (GearProfileEntry entry in data.gear)
                {
                    if (entry == null) continue;
                    GearItem item = FindGear(entry.gearId);
                    if (item == null) continue;

                    item.level = entry.level;
                    item.cardsCollected = entry.cardsCollected;
                    if (entry.cardsNeeded >= 0) item.cardsNeeded = entry.cardsNeeded;
                    if (entry.power >= 0) item.power = entry.power;
                    if (entry.spin >= 0) item.spin = entry.spin;
                    if (entry.agility >= 0) item.speed = entry.agility;
                    if (entry.serve >= 0) item.serve = entry.serve;
                    if (entry.control >= 0) item.control = entry.control;
                    if (entry.stamina >= 0) item.stamina = entry.stamina;
                    if (entry.upgradeCostCoins >= 0) item.upgradeCostCoins = entry.upgradeCostCoins;
                }
            }

            equippedGearIds.Clear();
            if (data.equippedGearIds != null) equippedGearIds.AddRange(data.equippedGearIds);
            if (equippedGearIds.Count == 0)
            {
                equippedGearIds.Add("carbon_pro");
                equippedGearIds.Add("swift_step");
                equippedGearIds.Add("starter_grip");
            }

            bagSlots.Clear();
            if (data.bagSlots != null)
            {
                foreach (BagSlotProfileData source in data.bagSlots)
                {
                    BagSlotState parsed;
                    if (!Enum.TryParse(source.state, out parsed)) parsed = BagSlotState.Empty;
                    bagSlots.Add(new BagSlot { bagId = source.bagId, state = parsed, unlockCompleteUnixSeconds = source.unlockCompleteUnixSeconds });
                }
            }
            if (bagSlots.Count == 0)
            {
                bagSlots.Add(new BagSlot { bagId = "welcome_bag", state = BagSlotState.Ready });
                bagSlots.Add(new BagSlot { bagId = "match_bag", state = BagSlotState.Unlocking, unlockCompleteUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 8040 });
                bagSlots.Add(new BagSlot { bagId = "match_bag", state = BagSlotState.Sealed });
                bagSlots.Add(new BagSlot { bagId = "", state = BagSlotState.Empty });
            }
            lastLeagueResultWeekKey = data.lastLeagueResultWeekKey ?? "";
            pityState = new PityState { chestsSinceEpic = data.chestsSinceEpic, chestsSinceLegendary = data.chestsSinceLegendary };
            unlockedFeatureIds.Clear();
            if (data.unlockedFeatureIds != null)
            {
                foreach (string featureId in data.unlockedFeatureIds) unlockedFeatureIds.Add(featureId);
            }
            if (unlockedFeatureIds.Count == 0)
            {
                unlockedFeatureIds.Add("home");
                unlockedFeatureIds.Add("gear");
                unlockedFeatureIds.Add("league");
                unlockedFeatureIds.Add("shop");
            }

            for (int i = 0; i < Tours.Count && i < data.tourWinsCurrent.Count; i++)
            {
                Tours[i].winsCurrent = data.tourWinsCurrent[i];
                Tours[i].cleared = data.tourCleared[i];
            }
        }
    }
}
