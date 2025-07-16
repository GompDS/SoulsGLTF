using System.Xml;
using DarkSrc.Util;
using DarkSrc.Util.Havok;
using SoulsAssetPipeline.XmlStructs;
using SoulsGLTF.Util;

namespace SoulsGLTF.Havok;

public class hkaAnimationBinding : hkReferencedObject
{
    public enum BlendHintEnum
    {
        NORMAL = 0,
        ADDITIVE = 1,
        ADDITIVE_DEPRECATED = 2
    }
    
    public string OriginalSkeletonName { get; set; } = "";
    public hkaAnimation Animation { get; set; }
    public short[] TransformTrackToBoneIndices { get; set; }
    public short[] FloatTrackToFloatSlotIndices { get; set; }
    public short[] PartitionIndices { get; set; }
    public BlendHintEnum BlendHint { get; set; } = BlendHintEnum.NORMAL;
    
    public override uint Signature => 0xfaf9150;
    
    public override XmlNode? ReadXml(XmlNode node)
    {
        XmlNode exitNode = node;
        
        foreach (XmlNode childNode in node.ChildNodes)
        {
            string paramName = childNode.SafeGetAttribute("name");
            switch (paramName)
            {
                case "originalSkeletonName":
                    OriginalSkeletonName = childNode.InnerText;
                    break;
                case "animation":
                    Animation = new hkaAnimation() { Name = childNode.InnerText };
                    break;
                case "transformTrackToBoneIndices":
                    TransformTrackToBoneIndices = new short[int.Parse(childNode.SafeGetAttribute("numelements"))];
                    HKXUtils.StringToShortArray(TransformTrackToBoneIndices, childNode.InnerText);
                    break;
                case "floatTrackToFloatSlotIndices":
                    FloatTrackToFloatSlotIndices = new short[int.Parse(childNode.SafeGetAttribute("numelements"))];
                    HKXUtils.StringToShortArray(FloatTrackToFloatSlotIndices, childNode.InnerText);
                    break;
                case "partitionIndices":
                    PartitionIndices = new short[int.Parse(childNode.SafeGetAttribute("numelements"))];
                    HKXUtils.StringToShortArray(PartitionIndices, childNode.InnerText);
                    break;
                case "blendHint":
                    BlendHint = Enum.Parse<BlendHintEnum>(childNode.InnerText);
                    break;
            }
        }

        exitNode = exitNode.NextSibling;

        return exitNode;
    }
}