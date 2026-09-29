using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pickleball.Sim
{
    /// <summary>One sampled pointer position of a touch, in reference pixels (DPI- and
    /// sensitivity-scaled), with a real-time timestamp in seconds.</summary>
    public readonly struct SwipeSample
    {
        public readonly Vector2 Position;
        public readonly float Time;

        public SwipeSample(Vector2 position, float time)
        {
            Position = position;
            Time = time;
        }
    }

    /// <summary>
    /// Turns a touch's sampled path into the stroke the player meant, and classifies it. Pure and
    /// engine-free so it can be unit tested; InputManager feeds it samples.
    /// </summary>
    public static class SwipeGesture
    {
        /// <summary>A finger that stays within this many reference pixels for RestSeconds is resting,
        /// not swiping (~120 ref px/s -- far slower than even a gentle dink push).</summary>
        public const float RestRadius = 6f;
        public const float RestSeconds = 0.05f;

        /// <summary>How much the aim widens the stroke's angle off vertical. At 1 (the old mapping) a
        /// 45 degree swipe only reached x = 3 of the 5-wide half court, and anything wider was already
        /// a Slice -- so aiming for the sideline changed the shot. At 1.6 a 45 degree swipe aims at the
        /// sideline and the Slice gesture (below) starts well beyond it.</summary>
        public const float AimGain = 1.6f;

        /// <summary>A stroke is a Slice only when it is this many times wider than it is tall: about
        /// 65 degrees off vertical, a deliberate flat sideways swipe rather than an aimed one.</summary>
        public const float SliceSidewaysRatio = 2.2f;

        /// <summary>Lowest contact height a Smash can be played from.</summary>
        public const float SmashMinHeight = 1.7f;

        /// <summary>How far the middle of a stroke must bow away from a straight line -- as a fraction
        /// of that line's length, see <see cref="Bend"/> -- to read as a curve. A deliberate arc of
        /// about 85 degrees of a circle or more clears it; a thumb pivoting through the 30-50 degrees
        /// it does on an intended-straight swipe reads under 0.08.</summary>
        public const float LobMinBend = 0.13f;

        /// <summary>The share of the path's length at each end that <see cref="Bend"/> ignores. The
        /// finger's roll at touch-down and its flick at lift-off live here: a 3 mm hook on a 20 mm
        /// swipe is about 13% of it, and measured with the ends included such hooks read as a
        /// deliberate curve on over a quarter of ordinary drives.</summary>
        private const float BendEndTrim = 0.15f;

        /// <summary>A lob must also travel at least this far up the screen, in reference pixels.</summary>
        public const float LobMinRise = 60f;

        /// <summary>
        /// Measures the deliberate stroke, excluding preparation and holds. Once a meaningful stroke
        /// starts, retain its origin through pauses and aim corrections. Falls back to the whole
        /// touch for slow gestures shorter than <paramref name="minDistance"/> after trimming.
        /// </summary>
        public static void MeasureStroke(IReadOnlyList<SwipeSample> samples, float minDistance,
            out Vector2 delta, out float seconds)
        {
            MeasureStroke(samples, minDistance, out delta, out seconds, out _);
        }

        /// <summary>
        /// As above, plus <paramref name="bend"/>: how far the middle of the stroke's path bows away
        /// from a straight line (see <see cref="Bend"/>; 0 = straight). The upward-curving swipe that
        /// plays a lob is told apart from a straight drive by this alone. A stroke that pauses and
        /// then corrects its aim is aiming, not curving, so it always reports 0.
        /// </summary>
        public static void MeasureStroke(IReadOnlyList<SwipeSample> samples, float minDistance,
            out Vector2 delta, out float seconds, out float bend)
        {
            delta = Vector2.Zero;
            seconds = 0f;
            bend = 0f;
            if (samples == null || samples.Count < 2) return;

            // The stroke ends where the finger arrived at its lift-off point, so a hold before
            // lifting (or a repeated final sample) doesn't dilute its speed.
            int last = samples.Count - 1;
            int end = last;
            while (end > 0 && Vector2.Distance(samples[end - 1].Position, samples[last].Position) <= RestRadius)
                end--;
            if (end == 0) return;

            int begin = end - 1;
            while (begin > 0)
            {
                // A pause before the first stroke is preparation. A pause after a meaningful
                // stroke is aiming: retain that origin so a correction cannot flip direction.
                if (IsResting(samples, begin) &&
                    Vector2.Distance(samples[begin].Position, samples[0].Position) < minDistance) break;
                begin--;
            }

            delta = samples[end].Position - samples[begin].Position;
            seconds = samples[end].Time - samples[begin].Time;
            bool heldToAim = false;
            // Remove interior holds as well as the lift-off hold. Keeping the full path for
            // direction must not turn a drive into a weak lob when the thumb pauses to aim.
            for (int i = begin; i < end; i++)
            {
                int restEnd = i;
                while (restEnd < end &&
                    Vector2.Distance(samples[restEnd + 1].Position, samples[i].Position) <= RestRadius)
                    restEnd++;
                float rest = samples[restEnd].Time - samples[i].Time;
                if (rest >= RestSeconds &&
                    Vector2.Distance(samples[i].Position, samples[begin].Position) >= minDistance)
                {
                    seconds -= rest;
                    i = restEnd;
                    heldToAim = true;
                }
            }
            if (delta.Length() < minDistance)
            {
                Vector2 whole = samples[end].Position - samples[0].Position;
                if (whole.Length() >= minDistance)
                {
                    delta = whole;
                    seconds = samples[end].Time - samples[0].Time;
                    begin = 0;
                }
            }
            bend = heldToAim ? 0f : Bend(samples, begin, end);
        }

        /// <summary>
        /// The sagitta of the path's middle: take the points at <see cref="BendEndTrim"/> and
        /// 1 - BendEndTrim of the way along the path (by length), and return how far the halfway point
        /// sits from the line between them, divided by that line's length. The ends are left out so
        /// the roll and flick of a real thumb don't count, and a single point is used rather than the
        /// deepest one so a jitter spike can't either. A circular arc of angle t reads
        /// (1 - cos(0.35t)) / (2 sin(0.35t)): 0.08 at 50 degrees, 0.14 at 90, 0.31 at 180.
        /// </summary>
        private static float Bend(IReadOnlyList<SwipeSample> samples, int begin, int end)
        {
            float total = 0f;
            for (int i = begin; i < end; i++) total += Vector2.Distance(samples[i].Position, samples[i + 1].Position);
            if (total < 0.0001f) return 0f;

            Vector2 a = PointAlong(samples, begin, end, total * BendEndTrim);
            Vector2 b = PointAlong(samples, begin, end, total * (1f - BendEndTrim));
            Vector2 middle = PointAlong(samples, begin, end, total * 0.5f);
            Vector2 chord = b - a;
            float length = chord.Length();
            if (length < 0.0001f) return 0f;
            Vector2 normal = new Vector2(-chord.Y, chord.X) / length;
            return SimMath.Abs(Vector2.Dot(middle - a, normal)) / length;
        }

        /// <summary>The point <paramref name="distance"/> along the path from sample
        /// <paramref name="begin"/>, interpolated between samples.</summary>
        private static Vector2 PointAlong(IReadOnlyList<SwipeSample> samples, int begin, int end, float distance)
        {
            for (int i = begin; i < end; i++)
            {
                Vector2 from = samples[i].Position, to = samples[i + 1].Position;
                float step = Vector2.Distance(from, to);
                if (distance <= step && step > 0f) return from + (to - from) * (distance / step);
                distance -= step;
            }
            return samples[end].Position;
        }

        /// <summary>True if the finger stayed within RestRadius of sample <paramref name="index"/> for
        /// the RestSeconds leading up to it, or ever since touch-down if that is more recent.</summary>
        private static bool IsResting(IReadOnlyList<SwipeSample> samples, int index)
        {
            SwipeSample at = samples[index];
            for (int i = index; i >= 0; i--)
            {
                if (Vector2.Distance(samples[i].Position, at.Position) > RestRadius) return false;
                if (at.Time - samples[i].Time >= RestSeconds) return true;
            }
            return true;
        }

        /// <summary>Classifies a straight stroke (in reference pixels) into a shot type.</summary>
        public static ShotType Classify(Vector2 referenceDelta, float duration) =>
            Classify(referenceDelta, duration, 0f);

        /// <summary>
        /// Classifies a stroke into a shot type. Direction and speed pick the drive family; an
        /// upward stroke that curves (<paramref name="bend"/> at least <see cref="LobMinBend"/>) is
        /// the lob. Speed is never what makes a lob: a slow straight lift is just a soft drive, and
        /// its power still comes from the swipe speed like any other shot.
        /// </summary>
        public static ShotType Classify(Vector2 referenceDelta, float duration, float bend)
        {
            duration = SimMath.Max(0.01f, duration);
            float speed = referenceDelta.Length() / duration; // reference pixels per second
            float absX = SimMath.Abs(referenceDelta.X);
            float deltaY = referenceDelta.Y;

            // Hard, decisive downward chop -> Smash. Keeping this threshold clearly above the dink
            // window makes a routine soft downward swipe much less likely to become an attack.
            if (deltaY < -70f && speed > 1150f)
            {
                return ShotType.Smash;
            }

            // Dink: a shallow downward push. The old -20px boundary made normal thumb-sized soft
            // gestures fall through to Flat.
            if (deltaY < -14f)
            {
                return ShotType.Dink;
            }

            bool sideways = absX > SimMath.Abs(deltaY) * SliceSidewaysRatio && absX > 60f;

            // Lob: an upward swipe that curves, however short or slow. There is no lob button; this
            // is the only way to play one.
            if (deltaY > LobMinRise && bend >= LobMinBend && !sideways)
            {
                return ShotType.Lob;
            }

            // Any other compact slow touch is a soft push -> Dink.
            if (referenceDelta.Length() < 125f && speed < 720f)
            {
                return ShotType.Dink;
            }

            // A flat sideways swipe -> Slice. Angled swipes are aim (see AimDirection), not a slice.
            if (sideways)
            {
                return ShotType.Slice;
            }

            // Fast, aggressive upward swipe -> Topspin Drive
            if (deltaY > 60f && speed > 800f)
            {
                return ShotType.Topspin;
            }

            return ShotType.Flat;
        }

        /// <summary>The shot a gesture plays at a given contact height. A Smash needs a high ball; a
        /// hard downward flick at a low one is still a downward -- soft -- stroke, so it plays as a
        /// Dink. It used to become a full-power Topspin, which sent quick dink flicks long.</summary>
        public static ShotType ForContactHeight(ShotType gesture, float contactHeight)
        {
            return gesture == ShotType.Smash && contactHeight < SmashMinHeight ? ShotType.Dink : gesture;
        }

        /// <summary>The unit aim direction for a stroke: its angle off the vertical axis widened by
        /// AimGain (capped at flat sideways), keeping its left/right and up/down sense. Only the
        /// horizontal component places the ball, so this is purely an aim-sensitivity curve.</summary>
        public static Vector2 AimDirection(Vector2 stroke)
        {
            float length = stroke.Length();
            if (length < 0.0001f) return Vector2.Zero;
            double offVertical = Math.Atan2(SimMath.Abs(stroke.X), SimMath.Abs(stroke.Y));
            double aim = Math.Min(Math.PI * 0.5, offVertical * AimGain);
            float x = (float)Math.Sin(aim) * (stroke.X < 0f ? -1f : 1f);
            float y = (float)Math.Cos(aim) * (stroke.Y < 0f ? -1f : 1f);
            return new Vector2(x, y);
        }
    }
}
