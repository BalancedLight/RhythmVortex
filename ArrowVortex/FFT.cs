using System;
using System.Numerics;

namespace RDPlaySongVortex.ArrowVortex
{
    // Credits to the Arrow Vortex project for the original implementation
    public static class FFT
    {
        public static void Calculate(Complex[] buffer)
        {
            int n = buffer.Length;
            if (n <= 1) return;

            int m = (int)Math.Log(n, 2);

            // Bit-reverse
            int j = 0;
            for (int i = 0; i < n - 1; i++)
            {
                if (i < j)
                {
                    var temp = buffer[i];
                    buffer[i] = buffer[j];
                    buffer[j] = temp;
                }
                int k = n / 2;
                while (k <= j)
                {
                    j -= k;
                    k /= 2;
                }
                j += k;
            }

            // butterfly
            for (int l = 1; l <= m; l++)
            {
                int le = 1 << l;
                int le2 = le / 2;
                Complex u = 1.0;
                Complex s = Complex.FromPolarCoordinates(1.0, -Math.PI / le2);

                for (int jj = 0; jj < le2; jj++)
                {
                    for (int i = jj; i < n; i += le)
                    {
                        int ip = i + le2;
                        Complex t = buffer[ip] * u;
                        buffer[ip] = buffer[i] - t;
                        buffer[i] = buffer[i] + t;
                    }
                    u *= s;
                }
            }
        }

        public static double[] GetMagnitude(Complex[] buffer)
        {
            double[] mag = new double[buffer.Length / 2];
            for (int i = 0; i < mag.Length; i++)
            {
                mag[i] = buffer[i].Magnitude;
            }
            return mag;
        }
    }
}
