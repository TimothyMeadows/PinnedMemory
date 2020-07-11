using System;

namespace PinnedMemory.Examples
{
    class Program
    {
        static void Main(string[] args)
        {
            // WARNING: It's unsafe to output pinned memory as a string, even using bitconverter however for the sake of learning this is done below.
            // DO NOT DO THIS IN YOUR APPLICATION, you should store your pinned data in it's native form so it will remain locked, and pinned in place
            // strings can't be pinned due to there nature, however, for example an array of char[] can be provided it's not converted back to string

            // This example shows how even if you have supplied bytes, they will be zero'd at creation. This is often the biggest mistake people make with this library.
            using (var pinZero = new PinnedMemory<byte>(new byte[] {63, 61, 77, 20, 63, 61, 77, 20, 63, 61, 77}))
            {
                Console.WriteLine(BitConverter.ToString(pinZero.ToArray()));
            }

            // This example shows how you can populate pinned memory on creation without those bytes being zero'd. They will still be zero'd on free!
            using var pin = new PinnedMemory<byte>(new byte[] {63, 61, 77, 20, 63, 61, 77, 20, 63, 61, 77}, false);
            Console.WriteLine(BitConverter.ToString(pin.ToArray()));
            pin.Dispose();

            // This second write should be all zero's as the memory has been freed
            Console.WriteLine(BitConverter.ToString(pin.ToArray()));

            // This example shows how even if you have supplied bytes, they will be zero'd at creation. This is often the biggest mistake people make with this library.
            PinnedMemory<byte> pinPopulated;
            using (pinPopulated = new PinnedMemory<byte>(new byte[11]))
            {
                pinPopulated[0] = 65;
                //pinPopulated.Write(0, 63);
                pinPopulated[1] = 61;
                //pinPopulated.Write(1, 61);
                pinPopulated[2] = 77;
                //pinPopulated.Write(2, 77);
                pinPopulated[3] = 20;
                //pinPopulated.Write(3, 20);
                pinPopulated[4] = 63;
                //pinPopulated.Write(4, 63);
                pinPopulated[5] = 61;
                //pinPopulated.Write(5, 61);
                pinPopulated[6] = 77;
                //pinPopulated.Write(6, 77);
                pinPopulated[7] = 20;
                //pinPopulated.Write(7, 20);
                pinPopulated[8] = 63;
                //pinPopulated.Write(8, 63);
                pinPopulated[9] = 61;
                //pinPopulated.Write(9, 61);
                pinPopulated[10] = 77;
                //pinPopulated.Write(10, 77);

                Console.WriteLine(BitConverter.ToString(pinPopulated.ToArray()));
            }

            // This second write should be all zero's as the memory has been freed
            Console.WriteLine(BitConverter.ToString(pinPopulated.ToArray()));

            
        }
    }
}
