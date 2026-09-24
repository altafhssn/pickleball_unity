using System;
using System.IO;
using NUnit.Framework;
using Pickleball.Sim;

namespace Pickleball.Tests
{
    public class AudioAndSaveTests
    {
        [Test]
        public void MusicHasHeadroomAndAQuietLoopSeam()
        {
            float[] samples = AudioSynthesis.CreateMusicLoop();
            double energy = 0;
            float peak = 0;
            foreach (float sample in samples)
            {
                Assert.That(float.IsNaN(sample) || float.IsInfinity(sample), Is.False);
                peak = Math.Max(peak, Math.Abs(sample));
                energy += sample * sample;
            }
            Assert.That(peak, Is.LessThan(0.9f));
            Assert.That(Math.Sqrt(energy / samples.Length), Is.GreaterThan(0.02));
            Assert.That(Math.Abs(samples[0] - samples[samples.Length - 1]), Is.LessThan(0.01f));
            Assert.That(samples.Length / (double)AudioSynthesis.SampleRate, Is.InRange(17, 18));
        }

        [Test]
        public void MusicGenerationIsRepeatable()
        {
            Assert.That(AudioSynthesis.CreateMusicLoop(), Is.EqualTo(AudioSynthesis.CreateMusicLoop()));
        }

        [Test]
        public void SaveReplacementRetainsLastCompleteVersion()
        {
            string directory = Path.Combine(Path.GetTempPath(), "pickleball-save-test-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "profile.json");
            try
            {
                AtomicFileStore.Write(path, "first complete save");
                AtomicFileStore.Write(path, "second complete save");
                Assert.That(File.ReadAllText(path), Is.EqualTo("second complete save"));
                Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo("first complete save"));
                Assert.That(File.Exists(path + ".tmp"), Is.False);
                File.WriteAllText(path + ".tmp", "interrupted next save");
                Assert.That(File.ReadAllText(path), Is.EqualTo("second complete save"));
                AtomicFileStore.Write(path, "third complete save");
                Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo("second complete save"));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}
