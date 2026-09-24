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

        /// <summary>
        /// The deliberate stroke within a touch: from the last moment the finger was resting to the
        /// moment it reached the point it was lifted from. Measuring from touch-down instead counted
        /// any time the thumb rested on the glass waiting for the ball as part of the swing, so a fast
        /// flick after a short rest read as a slow push -- a weak shot, usually classified as a Lob.
        /// Falls back to the whole touch when the last stroke alone is shorter than
        /// <paramref name="minDistance"/>, so nothing that registered as a swipe before stops registering.
        /// </summary>
        public static void MeasureStroke(IReadOnlyList<SwipeSample> samples, float minDistance,
            out Vector2 delta, out float seconds)
        {
            delta = Vector2.Zero;
            seconds = 0f;
            if (samples == null || samples.Count < 2) return;

            // The stroke ends where the finger arrived at its lift-off point, so a hold before
            // lifting (or a repeated final sample) doesn't dilute its speed.
            int last = samples.Count - 1;
            int end = last;
            while (end > 0 && Vector2.Distance(samples[end - 1].Position, samples[last].Position) <= RestRadius)
                end--;
            if (end == 0) return;

            int begin = end - 1;
            while (begin > 0 && !IsResting(samples, begin)) begin--;

            delta = samples[end].Position - samples[begin].Position;
            seconds = samples[end].Time - samples[begin].Time;
            if (delta.Length() < minDistance)
            {
                Vector2 whole = samples[end].Position - samples[0].Position;
                if (whole.Length() >= minDistance)
                {
                    delta = whole;
                    seconds = samples[end].Time - samples[0].Time;
                }
            }
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

        /// <summary>Classifies a stroke (in reference pixels) into a shot type.</summary>
        public static ShotType Classify(Vector2 referenceDelta, float duration)
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

            // Dink: accept a shallow downward push, or any compact slow touch. The old -20px / 600
            // px/s boundary made normal thumb-sized soft gestures fall through to Flat.
            if (deltaY < -14f || (referenceDelta.Length() < 125f && speed < 720f))
            {
                return ShotType.Dink;
            }

            // A flat sideways swipe -> Slice. Angled swipes are aim (see AimDirection), not a slice.
            if (absX > SimMath.Abs(deltaY) * SliceSidewaysRatio && absX > 60f)
            {
                return ShotType.Slice;
            }

            // Lob: a deliberate upward lift. A slightly shorter/slightly quicker lift still counts,
            // while a fast upward flick remains a topspin drive.
            if (deltaY > 88f && duration > 0.16f && speed < 1050f)
            {
                return ShotType.Lob;
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
