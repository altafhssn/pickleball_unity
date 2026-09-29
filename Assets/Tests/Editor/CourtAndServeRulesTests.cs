using System.Numerics;
using NUnit.Framework;
using Pickleball.Sim;

namespace Pickleball.Tests
{
    public class CourtAndServeRulesTests
    {
        [TestCase(true, true, 6, 4, true)]
        [TestCase(false, true, 5, 4, false)]
        [TestCase(true, false, 5, 4, false)]
        [TestCase(false, false, 5, 5, true)]
        public void SinglesAwardsPointsOnlyToTheServer(bool winner, bool server, int expectedPlayer, int expectedOpponent, bool scored)
        {
            int player = 5, opponent = 4;
            Assert.That(RallyRules.ResolveRally(winner, ref server, ref player, ref opponent), Is.EqualTo(scored));
            Assert.That(player, Is.EqualTo(expectedPlayer));
            Assert.That(opponent, Is.EqualTo(expectedOpponent));
            Assert.That(server, Is.EqualTo(winner));
        }

        [Test]
        public void ReceivingAtGamePointNeedsSideOutThenAServicePoint()
        {
            int player = 6, opponent = 5;
            bool serving = false;
            RallyRules.ResolveRally(true, ref serving, ref player, ref opponent);
            Assert.That(player, Is.EqualTo(6));
            Assert.That(RallyRules.IsMatchOver(player, opponent, 7, out _), Is.False);
            RallyRules.ResolveRally(true, ref serving, ref player, ref opponent);
            Assert.That(RallyRules.IsMatchOver(player, opponent, 7, out bool won), Is.True);
            Assert.That(won, Is.True);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void KitchenBoundaryIsPartOfTheNonVolleyZone(int side)
        {
            float sign = side == 0 ? -1 : 1;
            Assert.That(RallyRules.IsInKitchen(0, sign * CourtDimensions.KitchenDepth, side), Is.True);
            Assert.That(RallyRules.IsInKitchen(0, sign * (CourtDimensions.KitchenDepth + 0.01f), side), Is.False);
        }

        [Test]
        public void OnlyFirstTwoReturnsRequireABounce()
        {
            Assert.That(RallyRules.MustBounce(0), Is.False);
            Assert.That(RallyRules.MustBounce(1), Is.True);
            Assert.That(RallyRules.MustBounce(2), Is.True);
            Assert.That(RallyRules.MustBounce(3), Is.False);
        }

        [TestCase(0, true)]
        [TestCase(1, false)]
        [TestCase(2, true)]
        [TestCase(7, false)]
        public void ServeSideFollowsScoreParity(int score, bool expectedRight)
        {
            Assert.That(ServeRules.ServesFromRight(score), Is.EqualTo(expectedRight));
        }

        [Test]
        public void NearRightServeStartsBehindBaselineAndTargetsOppositeBox()
        {
            Vector3 server = ServeRules.ServerPosition(0, true);
            Vector3 target = ServeRules.BuildTarget(0, true, 0f, 0.5f);

            Assert.That(server.X, Is.GreaterThan(0f));
            Assert.That(server.Z, Is.LessThan(-CourtDimensions.HalfLength));
            Assert.That(target.X, Is.LessThan(0f));
            Assert.That(target.Z, Is.GreaterThan(CourtDimensions.KitchenDepth));
            Assert.That(ServeRules.IsInCorrectServiceCourt(target, 0, true), Is.True);
        }

        [Test]
        public void FarRightServeMirrorsNearSide()
        {
            Vector3 server = ServeRules.ServerPosition(1, true);
            Vector3 target = ServeRules.BuildTarget(1, true, 0f, 0.5f);

            Assert.That(server.X, Is.LessThan(0f));
            Assert.That(server.Z, Is.GreaterThan(CourtDimensions.HalfLength));
            Assert.That(target.X, Is.GreaterThan(0f));
            Assert.That(target.Z, Is.LessThan(-CourtDimensions.KitchenDepth));
            Assert.That(ServeRules.IsInCorrectServiceCourt(target, 1, true), Is.True);
        }

        [Test]
        public void ServeValidatorRejectsKitchenWrongBoxAndBaselineMiss()
        {
            Assert.That(ServeRules.IsInCorrectServiceCourt(new Vector3(-2f, 0f, 2f), 0, true), Is.False);
            Assert.That(ServeRules.IsInCorrectServiceCourt(new Vector3(2f, 0f, 6f), 0, true), Is.False);
            Assert.That(ServeRules.IsInCorrectServiceCourt(new Vector3(-2f, 0f, 9.1f), 0, true), Is.False);
        }

        [Test]
        public void PaintedSidelineAndGameplayBoundAgree()
        {
            Assert.That(ShotSim.IsInBounds(new Vector3(CourtDimensions.HalfWidth, 0f, 5f),
                CourtDimensions.PlayBounds), Is.True);
            Assert.That(ShotSim.IsInBounds(new Vector3(CourtDimensions.HalfWidth + 0.01f, 0f, 5f),
                CourtDimensions.PlayBounds), Is.False);
        }

        [TestCase(7, 6, true)]
        [TestCase(6, 7, false)]
        [TestCase(7, 0, true)]
        [TestCase(0, 7, false)]
        public void FirstToSevenWinsWithoutATwoPointMargin(int player, int opponent, bool playerWins)
        {
            Assert.That(RallyRules.IsMatchOver(player, opponent, 7, out bool playerWon), Is.True);
            Assert.That(playerWon, Is.EqualTo(playerWins));
        }

        [TestCase(6, 6)]
        [TestCase(6, 5)]
        [TestCase(0, 0)]
        public void MatchContinuesUntilSomeoneReachesSeven(int player, int opponent)
        {
            Assert.That(RallyRules.IsMatchOver(player, opponent, 7, out _), Is.False);
        }

        [Test]
        public void ReceiverWinningARallyScoresNothing()
        {
            // At 6-6 with the opponent serving, a player rally win is a side out, not the match.
            int player = 6, opponent = 6;
            bool playerServing = false;
            Assert.That(RallyRules.ResolveRally(true, ref playerServing, ref player, ref opponent), Is.False);
            Assert.That(RallyRules.IsMatchOver(player, opponent, 7, out _), Is.False);
            Assert.That(RallyRules.ResolveRally(true, ref playerServing, ref player, ref opponent), Is.True);
            Assert.That(RallyRules.IsMatchOver(player, opponent, 7, out bool won), Is.True);
            Assert.That(won, Is.True);
        }
    }
}
