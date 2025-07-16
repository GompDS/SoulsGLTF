using System.Collections;
using System.Numerics;
using System.Xml;
using DarkSrc.Util;
using SoulsAssetPipeline.XmlStructs;
using SoulsGLTF.Util;

namespace SoulsGLTF.Havok;

public class hkaSplineCompressedAnimation : hkaAnimation
{
    public enum RotationQuantizationEnum
    {
        POLAR_32 = 0,
        SMALLEST3_40 = 1,
        SMALLEST3_48 = 2,
        SMALLEST3_24 = 3,
        STRAIGHT_16 = 4,
        UNCOMPRESSED_128 = 5
    }

    public enum ScalarQuantizationEnum
    {
        BITS_8 = 0,
        BITS_16 = 1
    }
    
    public int NumFrames { get; set; }
    public int NumBlocks { get; set; }
    public int MaskAndQuantizationSize { get; set; }
    public double BlockDuration { get; set; }
    public double BlockInverseDuration { get; set; }
    public double FrameDuration { get; set; }
    public uint[] BlockOffsets { get; set; }
    public uint[] FloatBlockOffsets { get; set; }
    public uint[] TransformOffsets { get; set; }
    public uint[] FloatOffsets { get; set; }
    public byte[] Data { get; set; }
    public bool BigEndian { get; set; }
    
    public override uint Signature => 0x8c3b5f7e;
    
    public override XmlNode? ReadXml(XmlNode node)
    {
        XmlNode exitNode = node; 
        exitNode = base.ReadXml(node);
        
        foreach (XmlNode childNode in node.ChildNodes)
        {
            string paramName = childNode.SafeGetAttribute("name");
            switch (paramName)
            {
                case "numFrames":
                    NumFrames = int.Parse(childNode.InnerText);
                    break;
                case "numBlocks":
                    NumBlocks = int.Parse(childNode.InnerText);
                    break;
                case "maskAndQuantizationSize":
                    MaskAndQuantizationSize = int.Parse(childNode.InnerText);
                    break;
                case "blockDuration":
                    BlockDuration = double.Parse(childNode.InnerText);
                    break;
                case "blockInverseDuration":
                    BlockInverseDuration = double.Parse(childNode.InnerText);
                    break;
                case "frameDuration":
                    FrameDuration = double.Parse(childNode.InnerText);
                    break;
                case "blockOffsets":
                    BlockOffsets = new uint[int.Parse(childNode.SafeGetAttribute("numelements"))];
                    HKXUtils.StringToUintArray(BlockOffsets, childNode.InnerText);
                    break;
                case "floatBlockOffsets":
                    FloatBlockOffsets = new uint[int.Parse(childNode.SafeGetAttribute("numelements"))];
                    HKXUtils.StringToUintArray(FloatBlockOffsets, childNode.InnerText);
                    break;
                case "transformOffsets":
                    TransformOffsets = new uint[int.Parse(childNode.SafeGetAttribute("numelements"))];
                    HKXUtils.StringToUintArray(TransformOffsets, childNode.InnerText);
                    break;
                case "floatOffsets":
                    FloatOffsets = new uint[int.Parse(childNode.SafeGetAttribute("numelements"))];
                    HKXUtils.StringToUintArray(FloatOffsets, childNode.InnerText);
                    break;
                case "data":
                    Data = new byte[int.Parse(childNode.SafeGetAttribute("numelements"))];
                    HKXUtils.StringToByteArray(Data, childNode.InnerText);
                    break;
                case "endian":
                    BigEndian = childNode.InnerText != "0";
                    break;
            }
        }

        return exitNode;
    }

    public class hkaKeyFrameCollection : ICollection<hkaKeyFrame>
    {
        public ScalarQuantizationEnum TranslationQuantizationType;
        public RotationQuantizationEnum RotationQuantizationType;
        public ScalarQuantizationEnum ScaleQuantizationType;
        public ScalarQuantizationEnum FloatQuantizationType;
        
        private List<hkaKeyFrame> _collection = new List<hkaKeyFrame>();
        
        public hkaKeyFrame this[int index] => _collection[index];
        
        public IEnumerator<hkaKeyFrame> GetEnumerator() => _collection.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void Add(hkaKeyFrame item) => _collection.Add(item);
        public void Clear() => _collection.Clear();
        public bool Contains(hkaKeyFrame item) => _collection.Contains(item);
        public void CopyTo(hkaKeyFrame[] array, int arrayIndex) => _collection.CopyTo(array, arrayIndex);
        public bool Remove(hkaKeyFrame item) => _collection.Remove(item);
        public int Count => _collection.Count;
        public bool IsReadOnly { get; }

        public void SetQuantizationTypes(byte mask)
        {
            TranslationQuantizationType = (ScalarQuantizationEnum)((mask >> 0) & 0x03);
            RotationQuantizationType = (RotationQuantizationEnum)((mask >> 2) & 0x0F);
            ScaleQuantizationType = (ScalarQuantizationEnum)((mask >> 6) & 0x03);
        }
    }

    public class hkaKeyFrame
    {
        public uint FrameIndex;
        public Quaternion Rotation;
    }

    public Quaternion UnpackCompressedQuaternion(BinaryReader br, RotationQuantizationEnum quantizationType)
    {
        Quaternion quaternion;

        if (quantizationType == RotationQuantizationEnum.SMALLEST3_40)
        {
            quaternion = UnpackQuaternion40(br);
        }
        else
        {
            quaternion = new Quaternion();
            
            quaternion.X = br.ReadSingle();
            quaternion.Y = br.ReadSingle();
            quaternion.Z = br.ReadSingle();
            quaternion.W = br.ReadSingle();
        }
        
        return quaternion;
    }

    public Quaternion UnpackQuaternion40(BinaryReader br)
    {
        byte[] readBytes = br.ReadBytes(5);
        
        BitArray bits = new BitArray(readBytes);
        
        short val1 = BitConverter.ToInt16(bits.GetAsBytes(0, 13), 0);
        short val2 = BitConverter.ToInt16(bits.GetAsBytes(13, 13), 0);
        short val3 = BitConverter.ToInt16(bits.GetAsBytes(26, 12), 0);
        byte val4 = bits.GetAsBytes(38, 2)[0];
        
        float[] quaternionValues = new float[4];

        quaternionValues[val4] = 1.0f;
        
        float[] vectorValues = new float[] { val1 / 8191f, val2 / 8191f, val3 / 4096f };

        int k = 0;
        for (int i = 0; i < 4; i++)
        {
            if (i != val4)
            {
                quaternionValues[i] = vectorValues[k];
                k++;
            }
        }
        
        return new Quaternion(quaternionValues[0], quaternionValues[1], quaternionValues[2], quaternionValues[3]);
    }

    public void UnpackData(out List<hkaKeyFrameCollection> data)
    {
        data = new List<hkaKeyFrameCollection>();
        
        using (MemoryStream ms = new MemoryStream(Data))
        {
            BinaryReader br = new(ms);

            for (int block = 0; block < BlockOffsets.Length; block++)
            {
                hkaKeyFrameCollection keyFrameCollection = new();
                data.Add(keyFrameCollection);
                // Read until start of block
                br.BaseStream.Seek(BlockOffsets[block], SeekOrigin.Begin);
                // skip mask stuff (for now)
                int boneCount = MaskAndQuantizationSize / 4;
                for (int i = 0; i < boneCount; i++)
                {
                    keyFrameCollection.SetQuantizationTypes(br.ReadByte());
                    br.ReadByte();
                    br.ReadByte();
                    br.ReadByte();
                }

                br.ReadSingle();
                
                // read actual keyframe data
                int collection = 0;
                while (br.PeekChar() > -1)
                {
                    // Header
                    ushort numKeyframes = br.ReadUInt16();
                    ushort maxNumDuplicateKeyframes = br.ReadUInt16();
                    while (br.PeekChar() == 0)
                    {
                        br.ReadByte();
                    }

                    // Keyframe timestamps
                    for (int i = 0; i <= numKeyframes; i++)
                    {
                        uint timeStamp = br.ReadByte();
                        timeStamp += (uint)(255 * block);
                        if (keyFrameCollection.Count(x => x.FrameIndex == timeStamp) <= maxNumDuplicateKeyframes)
                        {
                            keyFrameCollection.Add(new hkaKeyFrame() { FrameIndex = timeStamp + (uint)(255 * block) });
                        }
                    }

                    int nextByte = br.PeekByte();
                    while (nextByte == 0)
                    {
                        br.ReadByte();
                        nextByte = br.PeekByte();
                    }

                    // Keyframe data
                    for (int i = 0; i <= numKeyframes; i++)
                    {
                        keyFrameCollection[i].Rotation = UnpackCompressedQuaternion(br, data[collection].RotationQuantizationType);
                    }

                    collection++;
                }
            }
        }
    }
}