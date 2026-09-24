using System;

namespace Pickleball.Sim
{
    /// <summary>An original eight-bar instrumental loop. No external recordings or game RNG.</summary>
    public static class AudioSynthesis
    {
        public const int SampleRate = 22050;
        public const double BeatsPerMinute = 108;

        public static float[] CreateMusicLoop()
        {
            double beat = 60 / BeatsPerMinute;
            var samples = new float[(int)Math.Round(32 * beat * SampleRate)];
            int[] roots = { 48, 45, 41, 43, 48, 45, 41, 43 };
            int[] melody = { 72, 76, 79, 76, 69, 72, 76, 72, 69, 72, 77, 72, 67, 71, 74, 79,
                             76, 79, 84, 79, 76, 72, 69, 72, 77, 76, 72, 69, 74, 71, 67, 71 };
            var random = new Random(20260910);
            for (int bar = 0; bar < 8; bar++)
            {
                int root = roots[bar];
                int third = bar % 4 == 1 ? 3 : 4;
                for (int step = 0; step < 8; step++)
                {
                    double time = (bar * 4 + step * 0.5) * beat;
                    int chordTone = step % 3 == 0 ? root + 12 : step % 3 == 1 ? root + 12 + third : root + 19;
                    Tone(samples, time, beat * 0.8, chordTone, 0.055f, false);
                    Drum(samples, time, 0.045, 0.018f, 2, random);
                }
                for (int step = 0; step < 4; step++)
                {
                    double time = (bar * 4 + step) * beat;
                    Tone(samples, time, beat * 0.72, melody[bar * 4 + step], 0.075f, false);
                    if ((step & 1) == 0)
                    {
                        Tone(samples, time, beat * 1.2, root - 12, 0.15f, true);
                        Drum(samples, time, 0.16, 0.18f, 0, random);
                    }
                    else Drum(samples, time, 0.1, 0.06f, 1, random);
                }
            }
            for (int i = 0; i < samples.Length; i++) samples[i] *= 2.5f;
            return samples;
        }

        private static void Tone(float[] samples, double start, double duration, int note, float volume, bool bass)
        {
            double frequency = 440 * Math.Pow(2, (note - 69) / 12.0);
            int offset = (int)Math.Round(start * SampleRate);
            int count = (int)(duration * SampleRate);
            for (int i = 0; i < count; i++)
            {
                double t = (double)i / SampleRate;
                double p = (double)i / count;
                double envelope = Math.Min(1, t / 0.006) * Math.Exp(-p * (bass ? 3 : 5)) * (1 - p);
                double phase = 2 * Math.PI * frequency * t;
                double wave = Math.Sin(phase) + (bass ? 0.12 : 0.24) * Math.Sin(phase * 2);
                samples[(offset + i) % samples.Length] += (float)(wave * envelope * volume);
            }
        }

        private static void Drum(float[] samples, double start, double duration, float volume, int kind, Random random)
        {
            int offset = (int)Math.Round(start * SampleRate);
            int count = (int)(duration * SampleRate);
            double previous = 0;
            for (int i = 0; i < count; i++)
            {
                double t = (double)i / SampleRate;
                double p = (double)i / count;
                double noise = random.NextDouble() * 2 - 1;
                double wave = kind == 0 ? Math.Sin(2 * Math.PI * (48 * t + 1.7 * (1 - Math.Exp(-32 * t))))
                    : kind == 1 ? noise * 0.65 + Math.Sin(2 * Math.PI * 175 * t) * 0.35 : (noise - previous) * 0.5;
                previous = noise;
                double envelope = Math.Min(1, t / 0.0015) * Math.Exp(-p * 7) * (1 - p);
                samples[(offset + i) % samples.Length] += (float)(wave * envelope * volume);
            }
        }
    }
}
