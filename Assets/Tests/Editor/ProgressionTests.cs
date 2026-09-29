using NUnit.Framework;
using Pickleball.Sim;

namespace Pickleball.Tests
{
    /// <summary>League promotion/relegation, coin-funded gear upgrades, and the reward ledger.</summary>
    public class ProgressionTests
    {
        // ---------------------------------------------------------------- leagues

        [Test]
        public void LeagueThresholdsAscend()
        {
            League[] leagues = LeagueConfig.Leagues;
            Assert.That(leagues[0].MinPoints, Is.EqualTo(LeagueConfig.MinimumPoints));
            for (int i = 1; i < leagues.Length; i++)
                Assert.That(leagues[i].MinPoints, Is.GreaterThan(leagues[i - 1].MinPoints), leagues[i].Name);
            Assert.That(LeagueRules.IndexFor(LeagueConfig.StartingPoints), Is.Zero);
        }

        [Test]
        public void ReachingTheNextThresholdPromotes()
        {
            int threshold = LeagueRules.PromotionThreshold(0);
            int before = threshold - 1;
            int after = LeagueRules.Apply(before, LeagueConfig.WinPoints);
            Assert.That(LeagueRules.Compare(before, after), Is.EqualTo(LeagueChange.Promoted));
            Assert.That(LeagueRules.IndexFor(after), Is.EqualTo(1));
            Assert.That(LeagueRules.Compare(before, threshold), Is.EqualTo(LeagueChange.Promoted), "exactly the threshold promotes");
        }

        [Test]
        public void FallingBelowTheCurrentMinimumRelegates()
        {
            int minimum = LeagueConfig.Leagues[2].MinPoints;
            Assert.That(LeagueRules.RelegationThreshold(2), Is.EqualTo(minimum));
            Assert.That(LeagueRules.Compare(minimum, minimum - 1), Is.EqualTo(LeagueChange.Relegated));
            int after = LeagueRules.Apply(minimum, -LeagueConfig.LossPoints);
            Assert.That(LeagueRules.IndexFor(after), Is.EqualTo(1));
        }

        [Test]
        public void MovingWithinALeagueIsNoChange()
        {
            int floor = LeagueConfig.Leagues[1].MinPoints;
            Assert.That(LeagueRules.Compare(floor + 50, floor + 50 + LeagueConfig.WinPoints), Is.EqualTo(LeagueChange.None));
            Assert.That(LeagueRules.Compare(floor + 50, floor + 50 - LeagueConfig.LossPoints), Is.EqualTo(LeagueChange.None));
        }

        [Test]
        public void PointsNeverFallBelowTheFloorAndTheBottomLeagueCannotRelegate()
        {
            Assert.That(LeagueRules.Apply(5, -LeagueConfig.LossPoints), Is.EqualTo(LeagueConfig.MinimumPoints));
            Assert.That(LeagueRules.RelegationThreshold(0), Is.EqualTo(-1));
            Assert.That(LeagueRules.Compare(0, LeagueRules.Apply(0, -LeagueConfig.LossPoints)), Is.EqualTo(LeagueChange.None));
        }

        [Test]
        public void TopLeagueHasNoPromotionAndReadsFull()
        {
            int top = LeagueConfig.Leagues.Length - 1;
            Assert.That(LeagueRules.PromotionThreshold(top), Is.EqualTo(-1));
            Assert.That(LeagueRules.Progress01(LeagueConfig.Leagues[top].MinPoints + 10), Is.EqualTo(1f));
        }

        [Test]
        public void ProgressRunsFromTheLeagueFloorToTheNextThreshold()
        {
            int floor = LeagueConfig.Leagues[1].MinPoints, next = LeagueConfig.Leagues[2].MinPoints;
            Assert.That(LeagueRules.Progress01(floor), Is.EqualTo(0f));
            Assert.That(LeagueRules.Progress01((floor + next) / 2), Is.EqualTo(0.5f).Within(0.01f));
        }

        // ---------------------------------------------------------------- gear

        [Test]
        public void EachSlotRaisesOnlyItsOwnStat()
        {
            LoadoutStats baseline = GearRules.Loadout(1, 1, 1);
            LoadoutStats paddle = GearRules.Loadout(5, 1, 1);
            LoadoutStats shoes = GearRules.Loadout(1, 5, 1);
            LoadoutStats grip = GearRules.Loadout(1, 1, 5);

            Assert.That(paddle.power, Is.GreaterThan(baseline.power));
            Assert.That(paddle.speed, Is.EqualTo(baseline.speed));
            Assert.That(paddle.accuracy, Is.EqualTo(baseline.accuracy));

            Assert.That(shoes.speed, Is.GreaterThan(baseline.speed));
            Assert.That(shoes.power, Is.EqualTo(baseline.power));
            Assert.That(shoes.accuracy, Is.EqualTo(baseline.accuracy));

            Assert.That(grip.accuracy, Is.GreaterThan(baseline.accuracy));
            Assert.That(grip.power, Is.EqualTo(baseline.power));
            Assert.That(grip.speed, Is.EqualTo(baseline.speed));
        }

        [Test]
        public void HigherStatsHelpTheirOwnEffect()
        {
            LoadoutStats low = GearRules.Loadout(1, 1, 1), high = GearRules.Loadout(10, 10, 10);
            Assert.That(ShotSim.PowerDurationMultiplier(high.power), Is.LessThan(ShotSim.PowerDurationMultiplier(low.power)));
            Assert.That(ShotSim.AccuracyScatterMultiplier(high.accuracy), Is.LessThan(ShotSim.AccuracyScatterMultiplier(low.accuracy)));
            Assert.That(ShotSim.SpeedMoveMultiplier(high.speed), Is.GreaterThan(ShotSim.SpeedMoveMultiplier(low.speed)));
        }

        [Test]
        public void UpgradeDeductsAndLevelsTogether()
        {
            int level = 3, cost = GearRules.UpgradeCost(3), coins = cost + 5;
            Assert.That(GearRules.TryUpgrade(ref coins, ref level), Is.EqualTo(PurchaseResult.Purchased));
            Assert.That(coins, Is.EqualTo(5));
            Assert.That(level, Is.EqualTo(4));
        }

        [Test]
        public void RefusedUpgradeTakesNothing()
        {
            int level = 3, coins = GearRules.UpgradeCost(3) - 1, before = coins;
            Assert.That(GearRules.TryUpgrade(ref coins, ref level), Is.EqualTo(PurchaseResult.NotEnoughCoins));
            Assert.That(coins, Is.EqualTo(before));
            Assert.That(level, Is.EqualTo(3));
        }

        [Test]
        public void MaxLevelCannotBeBoughtPast()
        {
            int level = GearConfig.MaxLevel, coins = 1000000;
            Assert.That(GearRules.UpgradeCost(level), Is.EqualTo(-1));
            Assert.That(GearRules.TryUpgrade(ref coins, ref level), Is.EqualTo(PurchaseResult.MaxLevel));
            Assert.That(coins, Is.EqualTo(1000000));
            Assert.That(level, Is.EqualTo(GearConfig.MaxLevel));
        }

        [Test]
        public void RepeatedPurchaseBuysTheNextLevelNotTheSameOneTwice()
        {
            int level = 1, coins = GearRules.UpgradeCost(1) + GearRules.UpgradeCost(2);
            GearRules.TryUpgrade(ref coins, ref level);
            GearRules.TryUpgrade(ref coins, ref level);
            Assert.That(level, Is.EqualTo(3));
            Assert.That(coins, Is.Zero);
            Assert.That(GearRules.TryUpgrade(ref coins, ref level), Is.EqualTo(PurchaseResult.NotEnoughCoins));
            Assert.That(level, Is.EqualTo(3));
        }

        [Test]
        public void UpgradeCostsRiseWithLevel()
        {
            for (int level = 2; level < GearConfig.MaxLevel; level++)
                Assert.That(GearRules.UpgradeCost(level), Is.GreaterThan(GearRules.UpgradeCost(level - 1)));
            Assert.That(GearConfig.UpgradeCosts.Length, Is.EqualTo(GearConfig.MaxLevel - GearConfig.StartLevel));
        }

        // ---------------------------------------------------------------- reward ledger

        [Test]
        public void ARewardIdCreditsOnlyOnce()
        {
            var ledger = new RewardLedger();
            Assert.That(ledger.TryRecord("match:abc"), Is.True);
            Assert.That(ledger.TryRecord("match:abc"), Is.False);
            Assert.That(ledger.HasCredited("match:abc"), Is.True);
            Assert.That(ledger.TryRecord(""), Is.False);
            Assert.That(ledger.TryRecord(null), Is.False);
        }

        [Test]
        public void LedgerSurvivesAReload()
        {
            var ledger = new RewardLedger();
            ledger.TryRecord("ad:1");
            var restored = new RewardLedger();
            restored.Load(ledger.Entries);
            Assert.That(restored.TryRecord("ad:1"), Is.False);
        }

        [Test]
        public void LedgerForgetsOldestFirstAtCapacity()
        {
            var ledger = new RewardLedger(2);
            ledger.TryRecord("a");
            ledger.TryRecord("b");
            ledger.TryRecord("c");
            Assert.That(ledger.HasCredited("a"), Is.False);
            Assert.That(ledger.HasCredited("b"), Is.True);
            Assert.That(ledger.HasCredited("c"), Is.True);
        }

        [Test]
        public void AdAllowanceResetsEachDay()
        {
            Assert.That(AdRewardRules.RemainingToday("2026-09-28", EconomyConfig.AdRewardsPerDay, "2026-09-28"), Is.Zero);
            Assert.That(AdRewardRules.RemainingToday("2026-09-28", 2, "2026-09-28"), Is.EqualTo(EconomyConfig.AdRewardsPerDay - 2));
            Assert.That(AdRewardRules.RemainingToday("2026-09-27", EconomyConfig.AdRewardsPerDay, "2026-09-28"), Is.EqualTo(EconomyConfig.AdRewardsPerDay));
        }
    }
}
