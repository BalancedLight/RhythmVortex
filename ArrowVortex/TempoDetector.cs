using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace RDPlaySongVortex.ArrowVortex
{
    public class TempoDetector
    {
        private const double MinimumBPM = 89.0;
        private const double MaximumBPM = 205.0;
        private const int IntervalDelta = 10;
        private const int IntervalDownsample = 3;

        public static List<TempoResult> CalculateBPM(List<Onset> onsets, float[] samples, int sampleRate, System.Threading.CancellationToken cancellationToken = default)
        {
            if (onsets.Count < 2) return new List<TempoResult>();

            IntervalTester test = new IntervalTester(sampleRate, onsets);
            GapData gapdata = new GapData(test.maxInterval, IntervalDownsample, onsets, sampleRate);

            FillCoarseIntervals(test, gapdata, cancellationToken);

            // Polyfit and normalize
            int numCoarseIntervals = (test.numIntervals + IntervalDelta - 1) / IntervalDelta;
            double[] x = new double[numCoarseIntervals];
            double[] y = new double[numCoarseIntervals];
            int idx = 0;
            for (int i = 0; i < test.numIntervals; i += IntervalDelta)
            {
                if (idx < numCoarseIntervals)
                {
                    x[idx] = test.minInterval + i;
                    y[idx] = test.fitness[i];
                    idx++;
                }
            }

            double[] coefs = Polyfit(x, y, 3);
            
            // Normalize
            for (int i = 0; i < test.numIntervals; i += IntervalDelta)
            {
                double interval = test.minInterval + i;
                double poly = coefs[0] + coefs[1] * interval + coefs[2] * interval * interval + coefs[3] * interval * interval * interval;
                test.fitness[i] -= poly;
            }

            // Refine intervals
            double maxFitness = 0.001;
            for (int i = 0; i < test.numIntervals; i += IntervalDelta)
            {
                if (test.fitness[i] > maxFitness) maxFitness = test.fitness[i];
            }

            // Lower threshold to get more candidates
            double fitnessThreshold = maxFitness * 0.2;
            for (int i = 0; i < test.numIntervals; i += IntervalDelta)
            {
                if (test.fitness[i] > fitnessThreshold)
                {
                    int begin = Math.Max(0, i - IntervalDelta);
                    int end = Math.Min(test.numIntervals, i + IntervalDelta);
                    FillIntervalRange(test, gapdata, begin, end, cancellationToken);
                    
                    // Normalize refined
                    for (int j = begin; j < end; j++)
                    {
                        double interval = test.minInterval + j;
                        double poly = coefs[0] + coefs[1] * interval + coefs[2] * interval * interval + coefs[3] * interval * interval * interval;
                        test.fitness[j] -= poly;
                    }
                }
            }

            // High precision pass
            gapdata = new GapData(test.maxInterval, 0, onsets, sampleRate);
            
            List<TempoResult> results = new List<TempoResult>();
            
            // Find peaks in fitness
            for (int i = 1; i < test.numIntervals - 1; i++)
            {
                if (test.fitness[i] > test.fitness[i-1] && test.fitness[i] > test.fitness[i+1] && test.fitness[i] > fitnessThreshold)
                {
                    double bpm = (sampleRate * 60.0) / (test.minInterval + i);
                    results.Add(new TempoResult { bpm = bpm, fitness = test.fitness[i] });
                }
            }

            // Sort by fitness
            results.Sort((a, b) => b.fitness.CompareTo(a.fitness));

            // Remove duplicates
            RemoveDuplicates(results);

            // Round BPMs
            RoundBPMValues(test, gapdata, results);

            return results;
        }

        private static void FillCoarseIntervals(IntervalTester test, GapData gapdata, System.Threading.CancellationToken token)
        {
            Parallel.For(0, (test.numIntervals + IntervalDelta - 1) / IntervalDelta, (k, state) =>
            {
                if (token.IsCancellationRequested) state.Stop();
                int i = k * IntervalDelta;
                if (i < test.numIntervals)
                {
                    int interval = test.minInterval + i;
                    test.fitness[i] = GetConfidenceForInterval(gapdata, interval);
                }
            });
            token.ThrowIfCancellationRequested();
        }

        private static void FillIntervalRange(IntervalTester test, GapData gapdata, int begin, int end, System.Threading.CancellationToken token)
        {
            Parallel.For(begin, end, (i, state) =>
            {
                if (token.IsCancellationRequested) state.Stop();
                int interval = test.minInterval + i;
                test.fitness[i] = GetConfidenceForInterval(gapdata, interval);
            });
            token.ThrowIfCancellationRequested();
        }

        private static double GetConfidenceForInterval(GapData gapdata, int interval)
        {
            int downsample = gapdata.downsample;
            int reducedInterval = interval >> downsample;
            if (reducedInterval <= 0) return 0;

            // histogram
            double[] wrappedOnsets = new double[reducedInterval];
            
            foreach (var onset in gapdata.onsets)
            {
                int pos = (int)(onset.time * gapdata.sampleRate);
                int wrapped = (pos >> downsample) % reducedInterval;
                wrappedOnsets[wrapped] += onset.strength;
            }

            // Find best alignment
            double highestConfidence = 0.0;
            
            foreach (var onset in gapdata.onsets)
            {
                int pos = (int)(onset.time * gapdata.sampleRate);
                int gapPos = (pos >> downsample) % reducedInterval;
                
                double confidence = GapConfidence(gapdata, wrappedOnsets, gapPos, reducedInterval);
                if (confidence > highestConfidence) highestConfidence = confidence;
            }

            return highestConfidence;
        }

        private static double GapConfidence(GapData gapdata, double[] wrappedOnsets, int gapPos, int interval)
        {
            int windowSize = gapdata.windowSize;
            int halfWindowSize = windowSize / 2;
            double area = 0.0;

            int beginOnset = gapPos - halfWindowSize;
            int endOnset = gapPos + halfWindowSize;

            // Handle wrapping
            for (int i = beginOnset; i < endOnset; ++i)
            {
                int idx = i;
                if (idx < 0) idx += interval;
                if (idx >= interval) idx -= interval;
                
                // Safety check
                if (idx >= 0 && idx < interval)
                {
                    int windowIdx = i - beginOnset;
                    if (windowIdx >= 0 && windowIdx < windowSize)
                    {
                        area += wrappedOnsets[idx] * gapdata.window[windowIdx];
                    }
                }
            }

            return area;
        }
        
        class IntervalTester
        {
            public int minInterval;
            public int maxInterval;
            public int numIntervals;
            public double[] fitness;
            public List<Onset> onsets;
            public int sampleRate;

            public IntervalTester(int sampleRate, List<Onset> onsets)
            {
                this.sampleRate = sampleRate;
                this.onsets = onsets;
                minInterval = (int)(sampleRate * 60.0 / MaximumBPM + 0.5);
                maxInterval = (int)(sampleRate * 60.0 / MinimumBPM + 0.5);
                numIntervals = maxInterval - minInterval;
                fitness = new double[numIntervals];
            }
        }

        class GapData
        {
            public int downsample;
            public int windowSize;
            public double[] window;
            public List<Onset> onsets;
            public int sampleRate;

            public GapData(int maxInterval, int downsample, List<Onset> onsets, int sampleRate)
            {
                this.downsample = downsample;
                this.onsets = onsets;
                this.sampleRate = sampleRate;
                this.windowSize = 2048 >> downsample;
                this.window = new double[windowSize];
                for(int i = 0; i < windowSize; ++i)
                {
                    window[i] = 0.54 - 0.46 * Math.Cos(2 * Math.PI * i / (windowSize - 1));
                }
            }
        }

        private static double[] Polyfit(double[] x, double[] y, int degree)
        {
            int n = x.Length;
            if (n == 0) return new double[degree + 1];

            double[,] X = new double[n, degree + 1];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j <= degree; j++)
                {
                    X[i, j] = Math.Pow(x[i], j);
                }
            }

            double[,] XtX = new double[degree + 1, degree + 1];
            double[] Xty = new double[degree + 1];

            for (int r = 0; r <= degree; r++)
            {
                for (int c = 0; c <= degree; c++)
                {
                    double sum = 0;
                    for (int i = 0; i < n; i++) sum += X[i, r] * X[i, c];
                    XtX[r, c] = sum;
                }
                double sumY = 0;
                for (int i = 0; i < n; i++) sumY += X[i, r] * y[i];
                Xty[r] = sumY;
            }

            return GaussianElimination(XtX, Xty);
        }

        private static double[] GaussianElimination(double[,] A, double[] b)
        {
            int n = b.Length;
            double[] x = new double[n];
            
            for (int i = 0; i < n; i++)
            {
                int max = i;
                for (int j = i + 1; j < n; j++)
                    if (Math.Abs(A[j, i]) > Math.Abs(A[max, i])) max = j;
                
                for (int k = i; k < n; k++) { double t = A[i, k]; A[i, k] = A[max, k]; A[max, k] = t; }
                { double t = b[i]; b[i] = b[max]; b[max] = t; }

                if (Math.Abs(A[i, i]) < 1e-10) continue;

                for (int j = i + 1; j < n; j++)
                {
                    double factor = A[j, i] / A[i, i];
                    for (int k = i; k < n; k++) A[j, k] -= factor * A[i, k];
                    b[j] -= factor * b[i];
                }
            }

            for (int i = n - 1; i >= 0; i--)
            {
                if (Math.Abs(A[i, i]) < 1e-10) { x[i] = 0; continue; }
                double sum = 0;
                for (int j = i + 1; j < n; j++) sum += A[i, j] * x[j];
                x[i] = (b[i] - sum) / A[i, i];
            }
            return x;
        }

        private static void RemoveDuplicates(List<TempoResult> tempo)
        {
            for (int i = 0; i < tempo.Count; ++i)
            {
                for (int j = i + 1; j < tempo.Count; ++j)
                {
                    double ratio = tempo[i].bpm / tempo[j].bpm;
                    bool isMultiple = Math.Abs(ratio - Math.Round(ratio)) < 0.05;
                    bool isClose = Math.Abs(tempo[i].bpm - tempo[j].bpm) < 1.0;

                    if (isMultiple || isClose)
                    {
                        if (tempo[i].fitness < tempo[j].fitness)
                        {
                            tempo.RemoveAt(i);
                            i--;
                            break;
                        }
                        else
                        {
                            tempo.RemoveAt(j);
                            j--;
                        }
                    }
                }
            }
        }

        private static void RoundBPMValues(IntervalTester test, GapData gapdata, List<TempoResult> tempo)
        {
            for (int i = 0; i < tempo.Count; i++)
            {
                var t = tempo[i];
                double rounded = Math.Round(t.bpm);
                if (Math.Abs(t.bpm - rounded) < 0.1)
                {
                    tempo[i] = new TempoResult { bpm = rounded, fitness = t.fitness };
                }
            }
        }
    }
}
