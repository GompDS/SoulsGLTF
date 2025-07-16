using System.Numerics;
using System.Xml;
using DarkSrc.Util;
using DarkSrc.Util.Havok;
using SoulsAssetPipeline.XmlStructs;
using SoulsGLTF.Util;

namespace SoulsGLTF.Havok;

public class hkaAnimatedReferenceFrame : hkReferencedObject
{
    public Vector4 Up { get; set; }
    public Vector4 Forward { get; set; }
    public double Duration { get; set; }
    public Vector4[] ReferenceFrameSamples { get; set; }

    public override XmlNode? ReadXml(XmlNode node)
    {
        XmlNode exitNode = node;
        
        foreach (XmlNode childNode in node.ChildNodes)
        {
            string paramName = childNode.SafeGetAttribute("name");
            switch (paramName)
            {
                case "up":
                    Up = Vector4Extensions.Parse(childNode.InnerText);
                    break;
                case "forward":
                    Forward = Vector4Extensions.Parse(childNode.InnerText);
                    break;
                case "duration":
                    Duration = double.Parse(childNode.InnerText);
                    break;
                case "referenceFrameSamples":
                    ReferenceFrameSamples = new Vector4[int.Parse(childNode.SafeGetAttribute("numelements"))];
                    HKXUtils.StringToVector4Array(ReferenceFrameSamples, childNode.InnerText);
                    break;
            }
        }

        exitNode = exitNode.NextSibling;

        return exitNode;
    }
}