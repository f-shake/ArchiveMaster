namespace ArchiveMaster.Services;

public class MersenneTwister
{
    private const int N = 624;
    private const int M = 397;
    private const uint MATRIX_A = 0x9908b0df;
    private const uint UPPER_MASK = 0x80000000;
    private const uint LOWER_MASK = 0x7fffffff;

    private uint[] mt = new uint[N];
    private int mti = N + 1;

    // 构造函数，初始化种子
    public MersenneTwister(uint seed)
    {
        mt[0] = seed;
        for (mti = 1; mti < N; mti++)
        {
            mt[mti] = (1812433253U * (mt[mti - 1] ^ (mt[mti - 1] >> 30)) + (uint)mti);
        }
    }

    // 返回一个随机数
    public uint Next()
    {
        uint y;
        if (mti >= N)
        {
            int kk;
            for (kk = 0; kk < N - M; kk++)
            {
                y = (mt[kk] & UPPER_MASK) | (mt[kk + 1] & LOWER_MASK);
                mt[kk] = mt[kk + M] ^ (y >> 1) ^ (y & 1U) * MATRIX_A;
            }
            for (; kk < N - 1; kk++)
            {
                y = (mt[kk] & UPPER_MASK) | (mt[kk + 1] & LOWER_MASK);
                mt[kk] = mt[kk + (M - N)] ^ (y >> 1) ^ (y & 1U) * MATRIX_A;
            }
            y = (mt[N - 1] & UPPER_MASK) | (mt[0] & LOWER_MASK);
            mt[N - 1] = mt[M - 1] ^ (y >> 1) ^ (y & 1U) * MATRIX_A;

            mti = 0;
        }

        y = mt[mti++];
        y ^= (y >> 11);
        y ^= (y << 7) & 0x9d2c5680;
        y ^= (y << 15) & 0xefc60000;
        y ^= (y >> 18);

        return y;
    }

    // 返回一个指定范围内的随机数
    public int Next(int minValue, int maxValue)
    {
        return (int)(Next() % (uint)(maxValue - minValue)) + minValue;
    }
}