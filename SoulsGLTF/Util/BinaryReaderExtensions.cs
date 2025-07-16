namespace SoulsGLTF.Util;

public static class BinaryReaderExtensions
{
    public static byte PeekByte(this BinaryReader reader)
    {
        byte peekedByte = reader.ReadByte();
        reader.BaseStream.Position--;
        return peekedByte;
    }
}