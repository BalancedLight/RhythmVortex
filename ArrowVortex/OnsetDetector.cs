using System;
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;

namespace RDPlaySongVortex.ArrowVortex
{
    // Credits to the Arrow Vortex project for the original implementation
    public class OnsetDetector
    {
        private const int WindowSize = 1024;
        private const int HopSize = 256; // 4x overlap

        public static List<Onset> Detect(float[] samples, int sampleRate, System.Threading.CancellationToken token = default)
        {
            List<Onset> onsets = new List<Onset>();
            int numFrames = (samples.Length - WindowSize) / HopSize;
            if (numFrames <= 0) return onsets;

            double[] window = new double[WindowSize];
            for (int i = 0; i < WindowSize; i++)
            {
                window[i] = 0.54 - 0.46 * Math.Cos(2 * Math.PI * i / (WindowSize - 1));
            }

            double[] prevMag = new double[WindowSize / 2];
            double[] spectralFlux = new double[numFrames];

            // Calculate Spectral Flux
            for (int i = 0; i < numFrames; i++)
            {
                if (i % 100 == 0 && token.IsCancellationRequested) token.ThrowIfCancellationRequested();

                Complex[] fftBuffer = new Complex[WindowSize];
                int startSample = i * HopSize;

                for (int j = 0; j < WindowSize; j++)
                {
                    if (startSample + j < samples.Length)
                        fftBuffer[j] = samples[startSample + j] * window[j];
                }

                FFT.Calculate(fftBuffer);
                double[] mag = FFT.GetMagnitude(fftBuffer);

                double flux = 0;
                for (int j = 0; j < mag.Length; j++)
                {
                    double diff = mag[j] - prevMag[j];
                    if (diff > 0) flux += diff;
                    prevMag[j] = mag[j];
                }
                spectralFlux[i] = flux;
            }

            // Peak Picking
            // Adaptive thresholding
            int thresholdWindow = 10;
            double multiplier = 1.5;
            
            for (int i = thresholdWindow; i < numFrames - thresholdWindow; i++)
            {
                // Local average
                double sum = 0;
                for (int j = i - thresholdWindow; j <= i + thresholdWindow; j++)
                {
                    sum += spectralFlux[j];
                }
                double mean = sum / (2 * thresholdWindow + 1);

                if (spectralFlux[i] > mean * multiplier && 
                    spectralFlux[i] > spectralFlux[i-1] && 
                    spectralFlux[i] > spectralFlux[i+1])
                {
                    // Found onset
                    double time = (double)(i * HopSize) / sampleRate;
                    onsets.Add(new Onset { time = time, strength = (float)spectralFlux[i] });
                }
            }

            return onsets;
        }
    }
}
