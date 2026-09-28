using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using Pickleball.Sim;

namespace Pickleball.Tests
{
    /// <summary>
    /// Regression tests for swings that connected with nothing even though the player did what the
    /// game asked: the timing ring pointing at the last hittable frame, a resting thumb weakening the
    /// flick, and (covered in-editor, not here) touches that began before the player's turn.
    /// </summary>
    public class MisfireTests
    {
        // ShotSim.CalculateTimingScore's Perfect band: the ring shows gold within this of the strike.
        private const float PerfectHalfWindow = 0.08f;
        private const float Reach = 1.6f;
        private const float MoveSpeed = 6f;
        private const float MinSwipe = 50f;
        private const float Frame = 1f / 60f;

        private struct Incoming
        {
            public Vector3 Start, Target, Player;
            public float Arc, Duration, Bounce, Spin;
            public bool MustBounce;
        }

        private static readonly Dictionary<string, Incoming> Shots = new Dictionary<string, Incoming>
        {
            ["ReturnOfServe"] = new Incoming { Start = new Vector3(2.5f, 1.2f, 9.35f), Target = new Vector3(-2.6f, 0, -6.5f),
                Arc = 1.8f, Duration = 1.25f, Bounce = 1f, Spin = 0f, Player = new Vector3(-2.5f, 1, -8.85f), MustBounce = true },
            ["ThirdBallDrive"] = new Incoming { Start = new Vector3(1, 1, 5.2f), Target = new Vector3(-2, 0, -7.5f),
                Arc = 0.85f, Duration = 0.85f, Bounce = 1.35f, Spin = 1f, Player = new Vector3(0, 1, -5.2f), MustBounce = true },
            ["KitchenDink"] = new Incoming { Start = new Vector3(0.5f, 0.6f, 3.15f), Target = new Vector3(-1, 0, -1.6f),
                Arc = 1.03f, Duration = 0.95f, Bounce = 0.45f, Spin = -0.5f, Player = new Vector3(0, 1, -3.15f), MustBounce = false },
            ["VolleyedDrive"] = new Incoming { Start = new Vector3(1, 1, 5.2f), Target = new Vector3(0.3f, 0, -7),
                Arc = 0.85f, Duration = 0.8f, Bounce = 1.35f, Spin = 1f, Player = new Vector3(0, 1, -5.2f), MustBounce = false },
            ["SliceToCorner"] = new Incoming { Start = new Vector3(1, 1, 5.2f), Target = new Vector3(-3.4f, 0, -5.5f),
                Arc = 0.9f, Duration = 1.13f, Bounce = 0.75f, Spin = -1f, Player = new Vector3(0, 1, -5.2f), MustBounce = false },
        };

        private static IEnumerable<TestCaseData> EveryShotBothSides()
        {
            foreach (string name in Shots.Keys)
                for (int side = 0; side <= 1; side++)
                    yield return new TestCaseData(name, side).SetName($"{{m}}({name}, side {side})");
        }

        /// <summary>The shots above are written for side 0 (near court); side 1 mirrors them.</summary>
        private static Incoming For(string name, int side)
        {
            Incoming s = Shots[name];
            if (side == 0) return s;
            Vector3 Mirror(Vector3 v) => new Vector3(-v.X, v.Y, -v.Z);
            s.Start = Mirror(s.Start);
            s.Target = Mirror(s.Target);
            s.Player = Mirror(s.Player);
            return s;
        }

        private static void Plan(Incoming s, int side, out Vector3 stance, out float arrival) =>
            BallFlight.PlanContact(s.Start, s.Target, s.Arc, s.Duration, s.Bounce, s.Spin, s.Player, side,
                s.MustBounce, MoveSpeed, Reach, 0f, out stance, out arrival);

        private static bool Window(Incoming s, int side, Vector3 player, float at, out float open, out float close) =>
            BallFlight.ContactWindow(s.Start, s.Target, s.Arc, s.Duration, s.Bounce, s.Spin, player, side,
                s.MustBounce, Reach, at, out open, out close);

        private static bool Find(Incoming s, int side, Vector3 player, float from, float to, out float at) =>
            BallFlight.FindContact(s.Start, s.Target, s.Arc, s.Duration, s.Bounce, s.Spin, player, side,
                s.MustBounce, Reach, from, to, out at, out _);

        // FindContact walks the path in 1/240 s steps.
        private const float ScanTolerance = 1f / 240f + 0.0001f;

        // ---------------------------------------------------------------- contact timing

        [TestCaseSource(nameof(EveryShotBothSides))]
        public void WholePerfectWindowAroundThePlannedStrikeIsHittable(string shot, int side)
        {
            Incoming s = For(shot, side);
            Plan(s, side, out Vector3 stance, out float arrival);

            Assert.That(Window(s, side, stance, arrival, out float open, out float close), Is.True,
                "the planned strike itself must be hittable");
            // Before the fix the strike sat on the last hittable frame after a bounce, so a release
            // in the second half of the gold ring hit nothing.
            Assert.That(close - arrival, Is.GreaterThanOrEqualTo(PerfectHalfWindow), "room after the strike");
            Assert.That(arrival - open, Is.GreaterThanOrEqualTo(PerfectHalfWindow), "room before the strike");
        }

        [TestCaseSource(nameof(EveryShotBothSides))]
        public void LateSwingWithinTheGraceStillFindsTheBall(string shot, int side)
        {
            Incoming s = For(shot, side);
            Plan(s, side, out Vector3 stance, out float arrival);
            Window(s, side, stance, arrival, out _, out float close);

            // A release 60% of the grace after the ball left reach looks back and strikes it where it
            // was last hittable.
            float grace = BallFlight.LateContactGraceSeconds;
            float release = close + grace * 0.6f;
            Assert.That(Find(s, side, stance, release, release - grace, out float struck), Is.True);
            Assert.That(struck, Is.EqualTo(close).Within(ScanTolerance));
        }

        [TestCaseSource(nameof(EveryShotBothSides))]
        public void EarlySwingWithinTheBufferFindsTheBall(string shot, int side)
        {
            Incoming s = For(shot, side);
            Plan(s, side, out Vector3 stance, out float arrival);
            Window(s, side, stance, arrival, out float open, out _);

            float buffer = BallFlight.ContactBufferSeconds;
            float release = open - buffer * 0.8f;
            Assert.That(Find(s, side, stance, release, release + buffer, out float struck), Is.True);
            Assert.That(struck, Is.EqualTo(open).Within(ScanTolerance));
        }

        [Test]
        public void SwingTooEarlyForTheBufferFindsNothing()
        {
            Incoming s = For("ReturnOfServe", 0);
            Plan(s, 0, out Vector3 stance, out float arrival);
            Window(s, 0, stance, arrival, out float open, out _);
            float release = open - BallFlight.ContactBufferSeconds * 1.5f;
            Assert.That(Find(s, 0, stance, release, release + BallFlight.ContactBufferSeconds, out _), Is.False);
        }

        [Test]
        public void SoftShotReboundStaysHittableForAQuarterSecond()
        {
            // A low-energy dink rebounds at the minimum speed. At the old minimum it cleared the
            // contact floor for only ~170 ms.
            var start = new Vector3(0, 0.6f, 3f);
            var target = new Vector3(0, 0, -1.5f);
            float arc = 0.65f, duration = 0.9f;
            BallFlight.Rebound(start, target, arc, duration, 0.45f, -0.5f, out Vector3 end, out _, out float seconds);
            Vector3 atPeak = BallFlight.Sample(target, end, 0f, 0.5f);
            // Stand on the rebound's path with ample reach, so only the ball's height limits contact.
            Assert.That(BallFlight.ContactWindow(start, target, arc, duration, 0.45f, -0.5f,
                new Vector3(atPeak.X, 1, atPeak.Z), 0, true, 10f, duration + seconds * 0.5f,
                out float open, out float close), Is.True);
            Assert.That(close - open, Is.GreaterThanOrEqualTo(0.24f));
        }

        [TestCase(0.0f)]
        [TestCase(0.2f)]
        public void CentringNeverSchedulesAStrikeBeforeThePlayerCanArrive(float elapsed)
        {
            // Far from the ball (a 7 unit run -- from 0.3 s in it is out of reach altogether): the
            // centred strike must still leave time to cover the distance.
            Incoming s = For("ReturnOfServe", 0);
            s.Player = new Vector3(3.5f, 1, -4f);
            BallFlight.PlanContact(s.Start, s.Target, s.Arc, s.Duration, s.Bounce, s.Spin, s.Player, 0,
                s.MustBounce, MoveSpeed, Reach, 0f, out Vector3 stance, out float arrival, elapsed);
            float travel = Vector3.Distance(s.Player, stance);
            Assert.That(travel, Is.LessThanOrEqualTo(MoveSpeed * arrival + Reach * 0.35f + 0.001f));
        }

        // ---------------------------------------------------------------- swipe measurement

        /// <summary>A touch sampled at 60 Hz. Each leg moves the finger by (x, y) reference pixels
        /// over the given seconds; a leg with no movement is a rest, with ±1 px of sensor jitter.</summary>
        private static List<SwipeSample> Touch(params (float x, float y, float seconds)[] legs)
        {
            var samples = new List<SwipeSample> { new SwipeSample(Vector2.Zero, 0f) };
            Vector2 at = Vector2.Zero;
            float time = 0f;
            int n = 0;
            foreach (var (x, y, seconds) in legs)
            {
                var move = new Vector2(x, y);
                Vector2 from = at;
                int frames = (int)Math.Round(seconds / Frame);
                for (int i = 1; i <= frames; i++)
                {
                    time += Frame;
                    n++;
                    at = from + move * (i / (float)frames);
                    Vector2 jitter = move == Vector2.Zero ? new Vector2(n % 2 == 0 ? 1f : -1f, 0f) : Vector2.Zero;
                    samples.Add(new SwipeSample(at + jitter, time));
                }
            }
            return samples;
        }

        private static void Measure(List<SwipeSample> touch, out Vector2 stroke, out float seconds) =>
            SwipeGesture.MeasureStroke(touch, MinSwipe, out stroke, out seconds);

        [TestCase(0.15f)]
        [TestCase(0.3f)]
        [TestCase(0.8f)]
        public void RestingThumbBeforeAFlickDoesNotWeakenOrReclassifyIt(float rest)
        {
            var flick = (0f, 150f, 0.15f); // 1000 ref px/s straight up: a topspin drive
            Measure(Touch(flick), out Vector2 plain, out float plainSeconds);
            Measure(Touch((0f, 0f, rest), flick), out Vector2 rested, out float restedSeconds);

            Assert.That(restedSeconds, Is.EqualTo(plainSeconds).Within(Frame + 0.001f));
            Assert.That(rested.Length(), Is.EqualTo(plain.Length()).Within(SwipeGesture.RestRadius));
            Assert.That(ShotSim.GesturePower(rested.Length(), restedSeconds),
                Is.EqualTo(ShotSim.GesturePower(plain.Length(), plainSeconds)).Within(0.1f));
            // Measured from touch-down, a 150 ms rest already turned this drive into a lob.
            Assert.That(SwipeGesture.Classify(plain, plainSeconds), Is.EqualTo(ShotType.Topspin));
            Assert.That(SwipeGesture.Classify(rested, restedSeconds), Is.EqualTo(ShotType.Topspin));
        }

        [Test]
        public void ThumbCreepWhileWaitingIsNotPartOfTheFlick()
        {
            Measure(Touch((3f, 20f, 0.5f), (0f, 150f, 0.15f)), out Vector2 stroke, out float seconds);
            Assert.That(seconds, Is.EqualTo(0.15f).Within(Frame + 0.001f));
            Assert.That(stroke.Y, Is.EqualTo(150f).Within(SwipeGesture.RestRadius));
        }

        [Test]
        public void HoldingStillBeforeLiftOffDoesNotWeakenTheStroke()
        {
            Measure(Touch((0f, 150f, 0.15f), (0f, 0f, 0.3f)), out Vector2 stroke, out float seconds);
            Assert.That(seconds, Is.EqualTo(0.15f).Within(Frame + 0.001f));
            Assert.That(stroke.Y, Is.EqualTo(150f).Within(SwipeGesture.RestRadius));
        }

        [Test]
        public void SteadyGentlePushIsMeasuredFromTouchDown()
        {
            // 250 ref px/s from the first frame: nothing to trim but a frame at either end.
            Measure(Touch((0f, -100f, 0.4f)), out Vector2 stroke, out float seconds);
            Assert.That(seconds, Is.EqualTo(0.4f).Within(2 * Frame + 0.001f));
            Assert.That(stroke.Length() / seconds, Is.EqualTo(250f).Within(250f * 0.05f));
            Assert.That(SwipeGesture.Classify(stroke, seconds), Is.EqualTo(ShotType.Dink));
        }

        [Test]
        public void PushSlowerThanTheRestThresholdStillRegisters()
        {
            // 100 ref px/s reads as "resting" frame to frame; the whole-touch fallback keeps it a swipe.
            Measure(Touch((0f, -70f, 0.7f)), out Vector2 stroke, out float seconds);
            Assert.That(stroke.Length(), Is.GreaterThanOrEqualTo(MinSwipe));
            Assert.That(SwipeGesture.Classify(stroke, seconds), Is.EqualTo(ShotType.Dink));
        }

        [Test]
        public void TapIsNotAStroke()
        {
            Measure(Touch((0f, 0f, 0.12f)), out Vector2 stroke, out _);
            Assert.That(stroke.Length(), Is.LessThan(MinSwipe));
        }

        [Test]
        public void CorrectionAfterHoldingKeepsTheOriginalAimOrigin()
        {
            Measure(Touch((120f, 150f, 0.15f), (0f, 0f, 0.3f), (-60f, 30f, 0.08f)),
                out Vector2 stroke, out float seconds);
            // The final thumb position is still right of the origin. The correction alone
            // points left, which previously sent the shot to the opposite side of the court.
            Assert.That(stroke.X, Is.EqualTo(60f).Within(SwipeGesture.RestRadius));
            Assert.That(stroke.Y, Is.EqualTo(180f).Within(SwipeGesture.RestRadius));
            Assert.That(seconds, Is.LessThan(0.28f));
            Assert.That(AimedX(stroke), Is.GreaterThan(0f));
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void LiftOffJitterDoesNotReverseAimAtDifferentFrameRates(int fps)
        {
            var touch = new List<SwipeSample>();
            for (int i = 0; i <= fps; i++)
            {
                float t = i / (float)fps;
                float progress = Math.Min(t / 0.2f, 1f);
                touch.Add(new SwipeSample(new Vector2(90f, 150f) * progress, t));
            }
            touch.Add(new SwipeSample(new Vector2(88f, 151f), 1.01f));
            Measure(touch, out Vector2 stroke, out float seconds);
            Assert.That(stroke.X, Is.EqualTo(90f).Within(SwipeGesture.RestRadius));
            Assert.That(seconds, Is.EqualTo(0.2f).Within(1f / fps));
        }

        // ---------------------------------------------------------------- aim vs shot type

        private static Vector2 Stroke(float degreesOffVertical, float length = 200f, bool down = false)
        {
            double a = degreesOffVertical * Math.PI / 180.0;
            return new Vector2((float)Math.Sin(a) * length, (float)Math.Cos(a) * length * (down ? -1f : 1f));
        }

        /// <summary>Where a full-power stroke at this angle is aimed, in court X.</summary>
        private static float AimedX(Vector2 stroke) =>
            ShotSim.PredictTargetPosition(Vector3.Zero, SwipeGesture.AimDirection(stroke), true, ShotType.Topspin,
                CourtDimensions.PlayBounds).X;

        [TestCase(0f)]
        [TestCase(15f)]
        [TestCase(30f)]
        [TestCase(45f)]
        [TestCase(60f)]
        public void AngledDriveStaysADrive(float degrees)
        {
            // Before, anything past ~53 degrees became a Slice -- including every aim at the sideline.
            Assert.That(SwipeGesture.Classify(Stroke(degrees), 0.1f), Is.EqualTo(ShotType.Topspin));
        }

        [Test]
        public void FortyFiveDegreeSwipeReachesTheSideline()
        {
            // The old mapping put a 45 degree swipe at x = 3 of 5; wider needed a slice.
            Assert.That(AimedX(Stroke(45f)), Is.GreaterThanOrEqualTo(3.9f));
            Assert.That(AimedX(Stroke(-45f)), Is.LessThanOrEqualTo(-3.9f));
        }

        [Test]
        public void AimOnlyWidensAndKeepsItsSide()
        {
            Assert.That(SwipeGesture.AimDirection(Stroke(0f)).X, Is.EqualTo(0f).Within(0.0001f));
            float previous = 0f;
            for (float degrees = 5f; degrees <= 90f; degrees += 5f)
            {
                float x = SwipeGesture.AimDirection(Stroke(degrees)).X;
                Assert.That(x, Is.GreaterThanOrEqualTo(Vector2.Normalize(Stroke(degrees)).X - 0.0001f), $"{degrees} deg");
                Assert.That(x, Is.GreaterThanOrEqualTo(previous), $"{degrees} deg");
                Assert.That(SwipeGesture.AimDirection(Stroke(-degrees)).X, Is.EqualTo(-x).Within(0.0001f));
                Assert.That(SwipeGesture.AimDirection(Stroke(degrees, down: true)).X, Is.EqualTo(x).Within(0.0001f));
                previous = x;
            }
            Assert.That(SwipeGesture.AimDirection(Stroke(33f, 57f)).Length(), Is.EqualTo(1f).Within(0.0001f));
        }

        [TestCase(75f)]
        [TestCase(90f)]
        public void FlatSidewaysSwipeIsASlice(float degrees)
        {
            Assert.That(SwipeGesture.Classify(Stroke(degrees), 0.1f), Is.EqualTo(ShotType.Slice));
        }

        [Test]
        public void QuickDinkFlickAtALowBallStaysADink()
        {
            // 100 ref px down in 80 ms reads as the smash gesture. At a low ball it used to become a
            // full-power Topspin that sent the "dink" deep.
            ShotType gesture = SwipeGesture.Classify(new Vector2(0f, -100f), 0.08f);
            Assert.That(gesture, Is.EqualTo(ShotType.Smash));
            Assert.That(SwipeGesture.ForContactHeight(gesture, 0.3f), Is.EqualTo(ShotType.Dink));
            Assert.That(SwipeGesture.ForContactHeight(gesture, SwipeGesture.SmashMinHeight), Is.EqualTo(ShotType.Smash));
            foreach (ShotType other in new[] { ShotType.Flat, ShotType.Topspin, ShotType.Slice, ShotType.Lob, ShotType.Dink })
                Assert.That(SwipeGesture.ForContactHeight(other, 0.3f), Is.EqualTo(other));
        }

        // ---------------------------------------------------------------- reachability & ball path

        [Test]
        public void PlanContactSaysWhetherTheBallCanBeReached()
        {
            Incoming s = For("ReturnOfServe", 0);
            Assert.That(BallFlight.PlanContact(s.Start, s.Target, s.Arc, s.Duration, s.Bounce, s.Spin, s.Player, 0,
                s.MustBounce, MoveSpeed, Reach, 0f, out _, out _), Is.True);
            // 7 units away with the ball already 0.4 s into its flight: the ring must not count down.
            Assert.That(BallFlight.PlanContact(s.Start, s.Target, s.Arc, s.Duration, s.Bounce, s.Spin,
                new Vector3(3.5f, 1, -4f), 0, s.MustBounce, MoveSpeed, Reach, 0f, out _, out _, 0.4f), Is.False);
        }

        [Test]
        public void PositionAtFollowsTheFlightThenTheReboundThenStops()
        {
            Incoming s = For("KitchenDink", 0);
            BallFlight.Rebound(s.Start, s.Target, s.Arc, s.Duration, s.Bounce, s.Spin, out Vector3 end, out float h, out float sec);

            Assert.That(BallFlight.PositionAt(s.Start, s.Target, s.Arc, s.Duration, s.Bounce, s.Spin, s.Duration * 0.5f, out Vector3 mid), Is.True);
            Assert.That(Vector3.Distance(mid, BallFlight.Sample(s.Start, s.Target, s.Arc, 0.5f)), Is.LessThan(0.0001f));
            Assert.That(BallFlight.PositionAt(s.Start, s.Target, s.Arc, s.Duration, s.Bounce, s.Spin, s.Duration + sec * 0.5f, out Vector3 peak), Is.True);
            Assert.That(Vector3.Distance(peak, BallFlight.Sample(s.Target, end, h, 0.5f)), Is.LessThan(0.0001f));
            Assert.That(BallFlight.PositionAt(s.Start, s.Target, s.Arc, s.Duration, s.Bounce, s.Spin, s.Duration + sec + 0.01f, out _), Is.False);
        }
    }
}
