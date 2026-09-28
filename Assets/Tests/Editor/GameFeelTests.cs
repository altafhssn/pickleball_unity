using System.Numerics;
using NUnit.Framework;
using Pickleball.Sim;

namespace Pickleball.Tests
{
    public class GameFeelTests
    {
        [TestCase(ShotType.Flat)]
        [TestCase(ShotType.Topspin)]
        [TestCase(ShotType.Slice)]
        [TestCase(ShotType.Lob)]
        [TestCase(ShotType.Dink)]
        [TestCase(ShotType.Smash)]
        public void SameLengthSlowPushTravelsShorterAndSlower(ShotType type)
        {
            float slow = ShotSim.GesturePower(180f, 0.9f);
            float fast = ShotSim.GesturePower(180f, 0.15f);
            var start = new Vector3(0, 1f, -5f);
            ShotData gentle = Shot(type, start, Vector2.UnitY * slow);
            ShotData strong = Shot(type, start, Vector2.UnitY * fast);
            float shortDistance = Vector3.Distance(start, gentle.targetPosition);
            float longDistance = Vector3.Distance(start, strong.targetPosition);
            Assert.That(shortDistance, Is.LessThan(longDistance));
            Assert.That(shortDistance / gentle.duration, Is.LessThan(longDistance / strong.duration));
            Assert.That(gentle.compositeScore, Is.EqualTo(strong.compositeScore));
        }

        [Test]
        public void PowerDoesNotChangeTheChosenLateralAngle()
        {
            var direction = Vector2.Normalize(new Vector2(0.5f, 1f));
            var a = ShotSim.PredictTargetPosition(Vector3.Zero, direction * 0.2f, true, ShotType.Flat, CourtDimensions.PlayBounds);
            var b = ShotSim.PredictTargetPosition(Vector3.Zero, direction, true, ShotType.Flat, CourtDimensions.PlayBounds);
            Assert.That(a.X, Is.EqualTo(b.X).Within(0.001f));
            Assert.That(a.Z, Is.LessThan(b.Z));
        }

        [Test]
        public void StrongFarSideShotTravelsDeeperToo()
        {
            var soft = ShotSim.PredictTargetPosition(Vector3.Zero, -Vector2.UnitY * 0.2f, false, ShotType.Topspin, CourtDimensions.PlayBounds);
            var hard = ShotSim.PredictTargetPosition(Vector3.Zero, -Vector2.UnitY, false, ShotType.Topspin, CourtDimensions.PlayBounds);
            Assert.That(hard.Z, Is.LessThan(soft.Z));
        }

        [Test]
        public void GesturePowerIsBoundedAndHoldingDoesNotCharge()
        {
            Assert.That(ShotSim.GesturePower(0, 0), Is.Zero);
            Assert.That(ShotSim.GesturePower(300, 0.01f), Is.EqualTo(1));
            Assert.That(ShotSim.GesturePower(300, 2), Is.LessThan(ShotSim.GesturePower(300, 0.5f)));
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void SmallOneFrameFlickStaysSoft(int sampleRate)
        {
            Assert.That(ShotSim.GesturePower(12f, 1f / sampleRate), Is.LessThanOrEqualTo(0.05f));
            Assert.That(ShotSim.GesturePower(50f, 1f / sampleRate), Is.LessThan(0.25f));
        }

        [Test]
        public void IncreasingSwipeLengthGivesUsablePowerRange()
        {
            float small = ShotSim.GesturePower(60f, 0.04f);
            float medium = ShotSim.GesturePower(120f, 0.08f);
            float full = ShotSim.GesturePower(240f, 0.16f);
            Assert.That(small, Is.InRange(0.2f, 0.3f));
            Assert.That(medium, Is.InRange(0.45f, 0.55f));
            Assert.That(full, Is.EqualTo(1f));
        }

        [Test]
        public void SpeedTuningCannotMakeShortSwipesFullPower()
        {
            Assert.That(ShotSim.GesturePower(50f, 0.01f, 200f), Is.LessThan(0.25f));
            Assert.That(ShotSim.GesturePower(240f, 0.4f, 1200f), Is.LessThan(1f));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void ReplannedBounceStaysReachableBeforeSecondBounce(int side)
        {
            float sign = side == 0 ? -1 : 1;
            var start = new Vector3(0, 1, -sign * 6);
            var target = new Vector3(2, 0, sign * 6);
            var player = new Vector3(1.5f, 1, sign * 7);
            BallFlight.PlanContact(start, target, 1, 1.2f, 1, 0, player, side, true,
                6, 1.6f, 0, out var stance, out float arrival, 1.25f);
            BallFlight.Rebound(start, target, 1, 1.2f, 1, 0, out var end, out float height, out float seconds);
            float contactTime = 1.25f + arrival;
            Assert.That(contactTime, Is.LessThan(1.2f + seconds));
            var contact = BallFlight.Sample(target, end, height, (contactTime - 1.2f) / seconds);
            Assert.That(BallFlight.CanContact(stance, contact, side, 1.6f, true, true), Is.True);
            Assert.That(Vector3.Distance(player, stance), Is.LessThanOrEqualTo(6 * arrival + 1.6f * 0.35f));
        }

        private static ShotData Shot(ShotType type, Vector3 start, Vector2 swipe, LoadoutStats stats = default)
        {
            var rng = new DeterministicRandom(42);
            return ShotSim.EvaluateAndBuildShot(0, start, start, start, 0f, swipe, type,
                CourtDimensions.PlayBounds, 2.5f, ref rng, stats);
        }

        [TestCase(ShotType.Dink)]
        [TestCase(ShotType.Slice)]
        [TestCase(ShotType.Lob)]
        public void TouchShotQualityDoesNotRequireAFullPowerSwipe(ShotType type)
        {
            ShotData shot = Shot(type, new Vector3(0, 0.7f, -3f), new Vector2(0, 0.2f));
            Assert.That(shot.compositeScore, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void KitchenDinkIsQuickerAndLowerThanBaselineDrop()
        {
            ShotData close = Shot(ShotType.Dink, new Vector3(0, 0.7f, -2.8f), new Vector2(0, -0.3f));
            ShotData deep = Shot(ShotType.Dink, new Vector3(0, 0.7f, -8f), new Vector2(0, -0.3f));
            Assert.That(close.duration, Is.LessThan(deep.duration));
            Assert.That(close.arcHeight, Is.LessThan(deep.arcHeight));
            Assert.That(close.targetPosition.Z, Is.InRange(0.7f, CourtDimensions.KitchenDepth));
        }

        [TestCase(ShotType.Dink)]
        [TestCase(ShotType.Topspin)]
        [TestCase(ShotType.Flat)]
        [TestCase(ShotType.Slice)]
        public void LowContactShotClearsTheNet(ShotType type)
        {
            ShotData shot = Shot(type, new Vector3(0, 0.2f, -8f), new Vector2(0.1f, -0.4f));
            float t = -shot.startPosition.Z / (shot.targetPosition.Z - shot.startPosition.Z);
            Assert.That(BallFlight.Sample(shot.startPosition, shot.targetPosition, shot.arcHeight, t).Y,
                Is.GreaterThan(BallFlight.NetHeight));
        }

        [Test]
        public void LobBuysTimeAndSmashIsTheFastestPutAway()
        {
            Vector3 start = new Vector3(0, 2f, -3f);
            ShotData lob = Shot(ShotType.Lob, start, Vector2.UnitY);
            ShotData drive = Shot(ShotType.Topspin, start, Vector2.UnitY);
            ShotData smash = Shot(ShotType.Smash, start, Vector2.UnitY);
            Assert.That(lob.duration, Is.GreaterThan(drive.duration));
            Assert.That(lob.arcHeight, Is.GreaterThan(drive.arcHeight));
            Assert.That(smash.duration, Is.LessThan(drive.duration));
        }

        [Test]
        public void SpinAffectsForwardCarryWithoutInventingSidespin()
        {
            Vector3 start = new Vector3(0, 1, -6), target = new Vector3(0, 0, 6);
            BallFlight.Rebound(start, target, 1f, 1f, 1.35f, 1f, out Vector3 top, out _, out _);
            BallFlight.Rebound(start, target, 1f, 1f, 0.75f, -1f, out Vector3 slice, out _, out _);
            Assert.That(top.X, Is.EqualTo(0f));
            Assert.That(slice.X, Is.EqualTo(0f));
            Assert.That(top.Z, Is.GreaterThan(slice.Z));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void RequiredBounceContactIsScheduledAfterLanding(int side)
        {
            float sign = side == 0 ? -1f : 1f;
            Vector3 start = new Vector3(0, 1, -sign * 8), target = new Vector3(0, 0, sign * 6);
            BallFlight.PlanContact(start, target, 1.2f, 1.3f, 1f, 0f,
                new Vector3(0, 1, sign * 6), side, true, 6f, 1.6f, 0f,
                out Vector3 stance, out float arrival);
            Assert.That(arrival, Is.GreaterThan(1.3f));
            BallFlight.Rebound(start, target, 1.2f, 1.3f, 1f, 0f,
                out Vector3 end, out float height, out float seconds);
            Vector3 contact = BallFlight.Sample(target, end, height, (arrival - 1.3f) / seconds);
            Assert.That(BallFlight.CanContact(stance, contact, side, 1.6f, true, true), Is.True);
            Assert.That(arrival, Is.LessThan(1.3f + seconds));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void KitchenVolleyAndDistantContactAreRejected(int side)
        {
            float sign = side == 0 ? -1f : 1f;
            Vector3 player = new Vector3(0, 1, sign * 2.5f);
            Vector3 ball = new Vector3(0, 0.6f, sign * 2f);
            Assert.That(BallFlight.CanContact(player, ball, side, 1.6f, false, false), Is.False);
            Assert.That(BallFlight.CanContact(player, ball, side, 1.6f, true, false), Is.True);
            Assert.That(BallFlight.CanContact(player, new Vector3(4, 1, sign * 7), side, 1.6f, true, false), Is.False);
            Assert.That(BallFlight.CanContact(player, new Vector3(0, 4, sign * 2), side, 1.6f, true, false), Is.False);
        }

        [Test]
        public void ReachableDriveCanBeVolleyedOutsideKitchen()
        {
            BallFlight.PlanContact(new Vector3(0, 1, 6), new Vector3(0, 0, -7), 1f, 1.2f, 1f, 0f,
                new Vector3(0, 1, -3.2f), 0, false, 6f, 1.6f, 0f, out Vector3 stance, out float arrival);
            Assert.That(arrival, Is.LessThan(1.2f));
            Assert.That(stance.Z, Is.LessThan(-CourtDimensions.KitchenDepth));
        }

        [Test]
        public void ReboundRemainsLowAndHasAUsableContactWindow()
        {
            foreach (ShotType type in new[] { ShotType.Dink, ShotType.Topspin, ShotType.Lob, ShotType.Smash })
            {
                ShotData shot = Shot(type, new Vector3(0, 0.7f, -3f), new Vector2(0, 0.4f));
                BallFlight.Rebound(shot.startPosition, shot.targetPosition, shot.arcHeight, shot.duration,
                    shot.bounceMultiplier, shot.spinRate, out _, out float height, out float duration);
                Assert.That(height, Is.InRange(0.18f, 0.8f), type.ToString());
                Assert.That(duration, Is.GreaterThan(0.28f), type.ToString());
            }
        }

        [TestCase(0.08f)]
        [TestCase(0.15f)]
        [TestCase(0.3f)]
        public void DelayedRequiredBounceUsesRemainingArrivalTime(float latency)
        {
            var start = new Vector3(0, 1, 6);
            var target = new Vector3(0, 0, -7);
            var player = new Vector3(0, 1, -6);
            BallFlight.PlanContact(start, target, 1f, 1.2f, 1f, 0f, player, 0, true,
                6f, 1.6f, 0f, out var originalStance, out float originalArrival);
            BallFlight.PlanContact(start, target, 1f, 1.2f, 1f, 0f, player, 0, true,
                6f, 1.6f, 0f, out var delayedStance, out float delayedArrival, latency);
            Assert.That(delayedArrival, Is.EqualTo(originalArrival - latency).Within(.0001f));
            Assert.That(delayedStance, Is.EqualTo(originalStance));
        }

        [Test]
        public void LatePacketNeverPlansContactInThePast()
        {
            BallFlight.PlanContact(new Vector3(0, 1, 6), new Vector3(0, 0, -7), 1f, 1.2f, 1f, 0f,
                new Vector3(0, 1, -6), 0, false, 6f, 1.6f, 0f, out _, out float arrival, 1.5f);
            Assert.That(arrival, Is.GreaterThanOrEqualTo(0f));
        }
    }
}
