using NUnit.Framework;
using Pickleball.Sim;

namespace Pickleball.Tests
{
    /// <summary>Play with AI pays coins for a win and never moves league points; multiplayer splits
    /// its reward pool 70/30 and moves league points both ways. See MatchModes.</summary>
    public class MatchModeTests
    {
        [Test]
        public void OnlyMultiplayerIsOnlineAndRanked()
        {
            Assert.That(MatchModes.IsOnline(MatchMode.Multiplayer), Is.True);
            Assert.That(MatchModes.AffectsLeague(MatchMode.Multiplayer), Is.True);
            Assert.That(MatchModes.IsOnline(MatchMode.AI), Is.False);
            Assert.That(MatchModes.AffectsLeague(MatchMode.AI), Is.False);
        }

        [Test]
        public void BeatingTheAiPaysCoinsAndNoLeaguePoints()
        {
            MatchPayout win = MatchModes.Payout(MatchMode.AI, true);
            Assert.That(win.Coins, Is.EqualTo(EconomyConfig.AiWinCoins).And.GreaterThan(0));
            Assert.That(win.LeaguePoints, Is.Zero);
        }

        [Test]
        public void LosingToTheAiNeverChangesLeaguePoints()
        {
            MatchPayout loss = MatchModes.Payout(MatchMode.AI, false);
            Assert.That(loss.Coins, Is.EqualTo(EconomyConfig.AiLossCoins));
            Assert.That(loss.LeaguePoints, Is.Zero);
            Assert.That(MatchModes.ForfeitPayout(MatchMode.AI).LeaguePoints, Is.Zero);
            Assert.That(MatchModes.ForfeitPayout(MatchMode.AI).Coins, Is.Zero);
        }

        [Test]
        public void MultiplayerWinAddsAndLossSubtractsLeaguePoints()
        {
            Assert.That(MatchModes.Payout(MatchMode.Multiplayer, true).LeaguePoints, Is.EqualTo(LeagueConfig.WinPoints).And.GreaterThan(0));
            Assert.That(MatchModes.Payout(MatchMode.Multiplayer, false).LeaguePoints, Is.EqualTo(-LeagueConfig.LossPoints).And.LessThan(0));
        }

        [Test]
        public void MultiplayerCoinsSplitTheRewardPoolSeventyThirty()
        {
            int winner = MatchModes.Payout(MatchMode.Multiplayer, true).Coins;
            int loser = MatchModes.Payout(MatchMode.Multiplayer, false).Coins;
            Assert.That(winner + loser, Is.EqualTo(EconomyConfig.MultiplayerRewardPool));
            Assert.That(winner * 30, Is.EqualTo(loser * 70), "the configured pool divides exactly 70/30");
        }

        [Test]
        public void MultiplayerForfeitIsALossThatEarnsNothing()
        {
            MatchPayout forfeit = MatchModes.ForfeitPayout(MatchMode.Multiplayer);
            Assert.That(forfeit.LeaguePoints, Is.EqualTo(MatchModes.Payout(MatchMode.Multiplayer, false).LeaguePoints));
            Assert.That(forfeit.Coins, Is.Zero);
        }

        [TestCase(100)]
        [TestCase(10)]
        [TestCase(250)]
        [TestCase(101)]
        [TestCase(7)]
        [TestCase(1)]
        [TestCase(0)]
        public void RewardPoolSplitKeepsTheWholePoolAndTheRatio(int pool)
        {
            Economy.SplitRewardPool(pool, 70, out int winner, out int loser);
            Assert.That(winner + loser, Is.EqualTo(pool));
            Assert.That(loser, Is.EqualTo(pool * 30 / 100), "loser's share rounds down");
            // winner - 0.7 * pool, in tenths of a coin: never under 70%, and less than one coin over.
            Assert.That(winner * 10 - pool * 7, Is.InRange(0, 9));
        }
    }
}
