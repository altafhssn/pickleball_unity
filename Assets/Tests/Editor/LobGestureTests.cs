using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using Pickleball.Sim;

namespace Pickleball.Tests
{
    /// <summary>The lob is an upward-curving swipe -- the only way to play one, with no button. A
    /// straight upward swipe of any speed stays a drive, its power set by the swipe speed. Real thumbs
    /// are never perfectly straight, so ordinary swipes must not read as curves.</summary>
    public class LobGestureTests
    {
        private const float MinSwipe = 50f;
        private const float Frame = 1f / 60f;

        /// <summary>Reference pixels per millimetre on a ~224 dpi tablet (SM-P613) at default sensitivity.</summary>
        private const float Mm = 6.3f;

        /// <summary>
        /// A thumb swipe: a <paramref name="lengthMm"/> stroke at <paramref name="angleDeg"/> off
        /// vertical, bowed by <paramref name="arcDeg"/> of a circle, with a sideways finger roll of
        /// <paramref name="leadMm"/> over the first two frames and a lift-off flick of
        /// <paramref name="tailMm"/> over the last two. Upward unless <paramref name="down"/>.
        /// </summary>
        private static List<SwipeSample> Swipe(float lengthMm, float seconds, float arcDeg,
            float angleDeg = 0f, float leadMm = 0f, float tailMm = 0f, bool down = false, int seed = 1)
        {
            var random = new Random(seed);
            var samples = new List<SwipeSample>();
            float time = 0f;
            samples.Add(new SwipeSample(Vector2.Zero, time));
            for (int i = 1; i <= 2; i++)
            {
                time += Frame;
                samples.Add(new SwipeSample(new Vector2(leadMm * Mm * i / 2f, 0f), time));
            }

            Vector2 start = samples[samples.Count - 1].Position;
            int frames = Math.Max(3, (int)(seconds / Frame));
            double theta = arcDeg * Math.PI / 180.0, angle = angleDeg * Math.PI / 180.0;
            float chord = lengthMm * Mm;
            for (int i = 1; i <= frames; i++)
            {
                double u = i / (double)frames, x, y;
                if (Math.Abs(arcDeg) < 0.01f)
                {
                    x = 0;
                    y = chord * u;
                }
                else
                {
                    double r = chord / (2 * Math.Sin(theta / 2));
                    double a = -theta / 2 + theta * u;
                    x = -r * Math.Cos(theta / 2) + r * Math.Cos(a);
                    y = chord / 2 + r * Math.Sin(a);
                }
                double rx = x * Math.Cos(angle) + y * Math.Sin(angle);
                double ry = -x * Math.Sin(angle) + y * Math.Cos(angle);
                if (down) ry = -ry;
                time += Frame;
                var jitter = new Vector2((float)(random.NextDouble() - 0.5), (float)(random.NextDouble() - 0.5));
                samples.Add(new SwipeSample(start + new Vector2((float)rx, (float)ry) + jitter, time));
            }

            Vector2 lift = samples[samples.Count - 1].Position;
            for (int i = 1; i <= 2; i++)
            {
                time += Frame;
                samples.Add(new SwipeSample(lift + new Vector2(tailMm * Mm * i / 2f, 0f), time));
            }
            return samples;
        }

        private static ShotType Classify(List<SwipeSample> touch, out float bend)
        {
            SwipeGesture.MeasureStroke(touch, MinSwipe, out Vector2 stroke, out float seconds, out bend);
            return SwipeGesture.Classify(stroke, seconds, bend);
        }

        /// <summary>What <see cref="SwipeGesture.MeasureStroke"/> reports for a clean circular arc.</summary>
        private static double ArcBend(double arcDeg) => Math.Tan(0.175 * arcDeg * Math.PI / 180.0) / 2.0;

        [Test]
        public void OrdinaryThumbSwipesAreNeverLobs()
        {
            // Every combination of length, speed, thumb-pivot arc (up to 50 degrees), aim angle, and
            // roll/flick hooks at either end. Measured over the whole path, over a quarter of these
            // read as lobs on a real tablet.
            int seed = 0;
            foreach (float length in new[] { 20f, 30f, 45f })
            foreach (float seconds in new[] { 0.10f, 0.18f, 0.30f })
            foreach (float arc in new[] { 0f, 20f, 35f, 50f })
            foreach (float angle in new[] { 0f, 25f, -25f })
            foreach (float lead in new[] { 0f, 2f, -3f })
            foreach (float tail in new[] { 0f, 3f, -3f })
            {
                var touch = Swipe(length, seconds, arc, angle, lead, tail, seed: ++seed);
                ShotType shot = Classify(touch, out float bend);
                Assert.That(shot, Is.Not.EqualTo(ShotType.Lob),
                    $"{length}mm {seconds}s arc {arc} angle {angle} lead {lead} tail {tail}: bend {bend:F3}");
            }
        }

        [TestCase(0.12f)]
        [TestCase(0.25f)]
        [TestCase(0.5f)]
        public void UpwardCurvingSwipeIsALobAtAnySpeed(float seconds)
        {
            Assert.That(Classify(Swipe(32f, seconds, 120f), out _), Is.EqualTo(ShotType.Lob));
            Assert.That(Classify(Swipe(32f, seconds, -120f), out _), Is.EqualTo(ShotType.Lob));
        }

        [Test]
        public void DeliberateCurveIsStillALobWithRealThumbHooks()
        {
            int seed = 100;
            foreach (float length in new[] { 20f, 30f, 45f })
            foreach (float seconds in new[] { 0.10f, 0.30f })
            foreach (float arc in new[] { 110f, 150f, 180f, -130f })
            foreach (float angle in new[] { 0f, 25f, -25f })
            foreach (float lead in new[] { 0f, 3f, -3f })
            foreach (float tail in new[] { 0f, 3f, -3f })
                Assert.That(Classify(Swipe(length, seconds, arc, angle, lead, tail, seed: ++seed), out float bend),
                    Is.EqualTo(ShotType.Lob), $"{length}mm {seconds}s arc {arc} angle {angle} lead {lead} tail {tail}: bend {bend:F3}");
        }

        [TestCase(0.12f, ShotType.Topspin)]
        [TestCase(0.5f, ShotType.Flat)]
        public void StraightUpwardSwipeIsNeverALob(float seconds, ShotType expected)
        {
            // A slow straight lift used to be the lob gesture; now it is just a soft drive.
            Assert.That(Classify(Swipe(32f, seconds, 0f), out float bend), Is.EqualTo(expected));
            Assert.That(bend, Is.LessThan(0.02f));
        }

        [Test]
        public void PausingToCorrectAimIsNotACurve()
        {
            // Up and to the right, hold, then pull back left: a V-shaped path, but it is aiming.
            var touch = new List<SwipeSample>();
            float time = 0f;
            Vector2 at = Vector2.Zero;
            void Leg(Vector2 move, float seconds)
            {
                Vector2 from = at;
                int frames = (int)Math.Round(seconds / Frame);
                for (int i = 1; i <= frames; i++)
                {
                    time += Frame;
                    at = from + move * (i / (float)frames);
                    touch.Add(new SwipeSample(at, time));
                }
            }
            touch.Add(new SwipeSample(at, time));
            Leg(new Vector2(120f, 150f), 0.15f);
            Leg(Vector2.Zero, 0.3f);
            Leg(new Vector2(-60f, 30f), 0.08f);
            Assert.That(Classify(touch, out float bend), Is.Not.EqualTo(ShotType.Lob));
            Assert.That(bend, Is.Zero);
        }

        [Test]
        public void CurvedDownwardSwipeIsNotALob()
        {
            Assert.That(Classify(Swipe(32f, 0.3f, 120f, down: true), out _), Is.Not.EqualTo(ShotType.Lob));
        }

        [TestCase(180f)]
        [TestCase(120f)]
        [TestCase(90f)]
        [TestCase(0f)]
        public void BendMeasuresHowFarTheMiddleOfThePathBows(float arc)
        {
            SwipeGesture.MeasureStroke(Swipe(32f, 0.3f, arc), MinSwipe, out _, out _, out float bend);
            Assert.That(bend, Is.EqualTo(ArcBend(arc)).Within(0.02f));
        }

        [Test]
        public void LobThresholdSitsBetweenAThumbPivotAndADeliberateArc()
        {
            Assert.That(ArcBend(50), Is.LessThan(SwipeGesture.LobMinBend * 0.65));
            Assert.That(ArcBend(100), Is.GreaterThan(SwipeGesture.LobMinBend));
        }

        [Test]
        public void LobPowerStillComesFromSwipeSpeed()
        {
            SwipeGesture.MeasureStroke(Swipe(32f, 0.5f, 120f), MinSwipe, out Vector2 slowStroke, out float slowSeconds, out _);
            SwipeGesture.MeasureStroke(Swipe(32f, 0.12f, 120f), MinSwipe, out Vector2 fastStroke, out float fastSeconds, out _);
            Assert.That(ShotSim.SwingPace(fastStroke.Length(), fastSeconds),
                Is.GreaterThan(ShotSim.SwingPace(slowStroke.Length(), slowSeconds)));
        }

        [Test]
        public void LobIsHighAndLooping()
        {
            var rng = new DeterministicRandom(7);
            var start = new Vector3(0f, 1f, -5f);
            ShotData lob = ShotSim.EvaluateAndBuildShot(0, start, start, start, 0f, Vector2.UnitY, ShotType.Lob,
                CourtDimensions.PlayBounds, 2.5f, ref rng);
            rng = new DeterministicRandom(7);
            ShotData drive = ShotSim.EvaluateAndBuildShot(0, start, start, start, 0f, Vector2.UnitY, ShotType.Flat,
                CourtDimensions.PlayBounds, 2.5f, ref rng);
            Assert.That(lob.arcHeight, Is.GreaterThan(drive.arcHeight * 2f));
            Assert.That(lob.duration, Is.GreaterThan(drive.duration));
        }
    }
}
