using System.Numerics;

namespace SoulsGLTF.Util;

public static class HKXUtils
{
    public static void StringToUintArray(uint[] array, string rawString)
    {
        if (rawString.Length == 0) return;
        rawString = rawString.Replace("\r", "").Replace("\n", " ");
        string[] strNums = rawString.Split(' ');
        for (int i = 0; i < strNums.Length; i++)
        {
            array[i] = uint.Parse(strNums[i]);
        }
    }
    
    public static void StringToShortArray(short[] array, string rawString)
    {
        if (rawString.Length == 0) return;
        rawString = rawString.Replace("\r", "").Replace("\n", " ");
        string[] strNums = rawString.Split(' ')[1..];
        for (int i = 0; i < strNums.Length; i++)
        {
            array[i] = (short)ushort.Parse(strNums[i]);
        }
    }
    
    public static void StringToByteArray(byte[] array, string rawString)
    {
        if (rawString.Length == 0) return;
        rawString = rawString.Replace("\r", "").Replace("\n", " ");
        string[] strNums = rawString.Split(' ')[1..];
        for (int i = 0; i < strNums.Length; i++)
        {
            array[i] = byte.Parse(strNums[i]);
        }
    }

    public static void StringToVector4Array(Vector4[] array, string rawString)
    {
        if (rawString.Length == 0) return;
        rawString = rawString.Replace("\r", "").Replace("\n", "").Replace("(", "").Replace(") ", ")");
        string[] strVectors = rawString.Split(')')[..^1];
        for (int i = 0; i < strVectors.Length; i++)
        {
            string[] strNums = strVectors[i].Split(' ');
            Vector4 v = new Vector4(float.Parse(strNums[0]), float.Parse(strNums[1]), float.Parse(strNums[2]), float.Parse(strNums[3]));
            array[i] = v;
        }
    }
}