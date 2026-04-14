using System;
using System.IO;
using System.Media;

namespace AlgorithmVisualizer.Helpers
{
    public static class SoundHelper
    {
        public static void PlaySineTone(double frequency, int durationMs, double volume = 0.1)
        {
            if (frequency < 100) frequency = 100;

            // Parametry audio
            int sampleRate = 44100;
            short bitsPerSample = 16;
            short channels = 1;
            int sampleCount = (int)(sampleRate * (durationMs / 1000.0));
            int dataSize = sampleCount * channels * (bitsPerSample / 8);

            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                // Nagłówek pliku WAV (RIFF)
                writer.Write("RIFF".ToCharArray());
                writer.Write(36 + dataSize);
                writer.Write("WAVE".ToCharArray());
                writer.Write("fmt ".ToCharArray());
                writer.Write(16);
                writer.Write((short)1); // PCM
                writer.Write(channels);
                writer.Write(sampleRate);
                writer.Write(sampleRate * channels * (bitsPerSample / 8));
                writer.Write((short)(channels * (bitsPerSample / 8)));
                writer.Write(bitsPerSample);
                writer.Write("data".ToCharArray());
                writer.Write(dataSize);

                // Generowanie fali sinusoidalnej
                double amplitude = short.MaxValue * volume;
                for (int i = 0; i < sampleCount; i++)
                {
                    short sample = (short)(amplitude * Math.Sin(2 * Math.PI * frequency * i / sampleRate));
                    writer.Write(sample);
                }

                ms.Position = 0;
                using (SoundPlayer player = new SoundPlayer(ms))
                {
                    player.Play(); // PlaySync by blokowało, Play() jest lepsze w Task.Run
                }
            }
        }
    }
}