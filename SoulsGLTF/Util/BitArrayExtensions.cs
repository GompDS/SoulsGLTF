using System.Collections;

namespace SoulsGLTF.Util;

public static class BitArrayExtensions
{
    public static byte[] GetAsBytes(this BitArray bitArray, int index, int length)
    {
        byte[] bytes = new byte[(length + (8 - length % 8)) / 8];

        bool[] bits = new bool[length];
        for (int i = 0; i < length; i++) bits[i] = bitArray[index + i];

        BitArray subsetArray = new BitArray(bits);
        subsetArray.CopyTo(bytes, 0);
        
        return bytes;
    }
}