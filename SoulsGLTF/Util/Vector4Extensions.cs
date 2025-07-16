using System.Numerics;

namespace SoulsGLTF.Util;

public static class Vector4Extensions
{
    public static Vector4 Parse(string str)
    {
        str = str[1..^1];
        string[] strNums = str.Split(' ');
        return new Vector4(float.Parse(strNums[0]), float.Parse(strNums[1]), float.Parse(strNums[2]), float.Parse(strNums[3]));
    }
}