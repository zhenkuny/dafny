using System.Numerics;
using System;

namespace InputGen {
    public partial class Gen {
        public static BigInteger GetRandomUint() {
            int length = 2;
            Random random = new Random();
            byte[] data = new byte[length];
            random.NextBytes(data);
            data [data.Length - 1] &= 0x0;
            return new BigInteger(data);
        }

        // public BigInteger getRandom(int length){
        //     Random random = new Random();
        //     byte[] data = new byte[length];
        //     random.NextBytes(data);
        //     return new BigInteger(data);
        // }
    }
}