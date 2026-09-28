using System;
using NUnit.Framework;
using Pickleball.Sim;

namespace Pickleball.Tests
{
    /// <summary>Tours are online and carry the stakes; practice against the AI is free and pays
    /// nothing. See MatchModes.</summary>
    public class MatchModeTests
    {
        // MIAMI BEACH: entry 50, win 220 coins / 28 trophies.
        private const int StageEntry = 50;
        private const int StageCoins = 220;
        private const int StageTrophies = 28;

        [TestCase(true)]
        [TestCase(false)]
        public void PracticePaysNothingWinOrLose(bool won)
        {
            MatchPayout payout = MatchModes.Payout(MatchMode.Practice, won, StageCoins, StageTrophies,
                won ? 7 : 1, won ? 1 : 7, 9, 20);
            Assert.That(payout.Coins, Is.EqualTo(0));
            Assert.That(payout.Trophies, Is.EqualTo(0));
            Assert.That(payout.SeasonXp, Is.EqualTo(0));
            Assert.That(payout.HasPerformanceBonus, Is.False);
        }

        [Test]
        public void PracticeIsFreeToEnterAndOffline()
        {
            Assert.That(MatchModes.EntryFee(MatchMode.Practice, StageEntry), Is.EqualTo(0));
            Assert.That(MatchModes.IsOnline(MatchMode.Practice), Is.False);
        }

        [Test]
        public void ToursAreOnlineAndChargeTheStageEntry()
        {
            Assert.That(MatchModes.EntryFee(MatchMode.Tour, StageEntry), Is.EqualTo(StageEntry));
            Assert.That(MatchModes.IsOnline(MatchMode.Tour), Is.True);
        }

        [Test]
        public void TourWinPaysTheStageReward()
        {
            MatchPayout close = MatchModes.Payout(MatchMode.Tour, true, StageCoins, StageTrophies, 7, 5, 0, 0);
            Assert.That(close.Coins, Is.EqualTo(StageCoins));
            Assert.That(close.Trophies, Is.EqualTo(StageTrophies));
            Assert.That(close.SeasonXp, Is.EqualTo(40));

            // A wider margin pays more coins, capped at +25%; trophies don't change.
            MatchPayout rout = MatchModes.Payout(MatchMode.Tour, true, StageCoins, StageTrophies, 7, 0, 0, 0);
            Assert.That(rout.Coins, Is.GreaterThan(close.Coins).And.AtMost((int)Math.Round(StageCoins * 1.25f)));
            Assert.That(rout.Trophies, Is.EqualTo(StageTrophies));
        }

        [Test]
        public void TourWinPaysAPerformanceBonus()
        {
            MatchPayout payout = MatchModes.Payout(MatchMode.Tour, true, StageCoins, StageTrophies, 7, 5, 3, 8);
            Assert.That(payout.Coins, Is.EqualTo(StageCoins + 3 * 4 + 30));
            Assert.That(payout.HasPerformanceBonus, Is.True);
            Assert.That(payout.SeasonXp, Is.EqualTo(43));
        }

        [Test]
        public void TourLossCostsAboutHalfTheStageTrophies()
        {
            MatchPayout payout = MatchModes.Payout(MatchMode.Tour, false, StageCoins, StageTrophies, 3, 7, 5, 12);
            Assert.That(payout.Coins, Is.EqualTo(0));
            Assert.That(payout.Trophies, Is.EqualTo(-(int)Math.Round(StageTrophies * 0.55f)));
            Assert.That(payout.SeasonXp, Is.EqualTo(10));
            Assert.That(payout.HasPerformanceBonus, Is.False);
        }

        [Test]
        public void TourForfeitCostsMoreThanLosing()
        {
            MatchPayout loss = MatchModes.Payout(MatchMode.Tour, false, StageCoins, StageTrophies, 4, 7, 0, 0);
            // Quitting while ahead is still scored as a loss.
            MatchPayout forfeit = MatchModes.ForfeitPayout(MatchMode.Tour, StageTrophies, 6, 2);
            Assert.That(forfeit.Trophies, Is.EqualTo(loss.Trophies - MatchModes.ForfeitSurcharge));
            Assert.That(forfeit.Coins, Is.EqualTo(0));
        }

        [Test]
        public void LeavingPracticeCostsNothing()
        {
            MatchPayout forfeit = MatchModes.ForfeitPayout(MatchMode.Practice, StageTrophies, 0, 6);
            Assert.That(forfeit.Trophies, Is.EqualTo(0));
            Assert.That(forfeit.SeasonXp, Is.EqualTo(0));
        }

        [Test]
        public void PracticeLevelsGetHarderAndShowTheirOwnName()
        {
            float previous = -1f;
            foreach (PracticeLevel level in Enum.GetValues(typeof(PracticeLevel)))
            {
                float skill = MatchModes.PracticeSkill(level);
                Assert.That(skill, Is.InRange(0f, 1f));
                Assert.That(skill, Is.GreaterThan(previous), level + " should be harder than the level before it");
                // The match card labels the AI from its skill; it must read as the level picked.
                Assert.That(MatchModes.SkillLabel(skill), Is.EqualTo(level.ToString().ToUpperInvariant()));
                previous = skill;
            }
        }
    }
}
