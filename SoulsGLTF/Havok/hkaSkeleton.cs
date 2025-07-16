using System.Numerics;
using System.Xml;
using DarkSrc.Util;
using DarkSrc.Util.Havok;
using SoulsAssetPipeline.XmlStructs;
using SoulsGLTF.Util;

namespace SoulsGLTF.Havok;

public class hkaSkeleton : hkReferencedObject
{
    public struct Bone
    {
        public string Name;
        public bool LockTranslation;
    }

    /// <summary>
    /// Struct representing the base bose of bone in a skeleton.
    /// Rotation is given as a quaternion.
    /// </summary>
    public struct BonePose
    {
        public Vector4 Translation;
        public Vector4 Rotation;
        public Vector4 Scale;
    }
    
    public string SkeletonName { get; set; } = "";
    public short[] ParentIndices { get; set; }
    public Bone[] Bones { get; set; }
    public BonePose[] ReferencePose { get; set; }

    public virtual XmlNode? ReadXml(XmlNode node)
    {
        XmlNode exitNode = node;
        
        foreach (XmlNode childNode in node.ChildNodes)
        {
            string paramName = childNode.SafeGetAttribute("name");
            switch (paramName)
            {
                case "name":
                    SkeletonName = childNode.InnerText;
                    break;
                case "parentIndices":
                    ParentIndices = new short[int.Parse(childNode.SafeGetAttribute("numelements"))];
                    HKXUtils.StringToShortArray(ParentIndices, childNode.InnerText);
                    break;
                case "bones":
                    int bonesCount = int.Parse(childNode.SafeGetAttribute("numelements"));
                    Bones = new Bone[bonesCount];
                    for (int i = 0; i < bonesCount; i++)
                    {
                        Bones[i] = ReadBoneXml(childNode.ChildNodes[i]);
                    }
                    break;
                case "referencePose":
                    int bonePoseCount = int.Parse(childNode.SafeGetAttribute("numelements"));
                    Vector4[] vectorSoup = new Vector4[bonePoseCount * 3];
                    HKXUtils.StringToVector4Array(vectorSoup, childNode.InnerText);

                    ReferencePose = new BonePose[bonePoseCount];
                    for (int i = 0; i < ReferencePose.Length; i++)
                    {
                        int soupIndex = i * 3;
                        ReferencePose[i] = new BonePose()
                        {
                            Translation = vectorSoup[soupIndex],
                            Rotation = vectorSoup[soupIndex + 1],
                            Scale = vectorSoup[soupIndex + 2]
                        };
                    }
                    break;
            }
        }

        exitNode = exitNode.NextSibling;

        return exitNode;
    }

    private Bone ReadBoneXml(XmlNode boneNode)
    {
        string boneName = "";
        bool lockTranslation = false;
        
        foreach (XmlNode childNode in boneNode.ChildNodes)
        {
            string paramName = childNode.SafeGetAttribute("name");
            switch (paramName)
            {
                case "name":
                    boneName = childNode.InnerText;
                    break;
                case "lockTranslation":
                    lockTranslation = bool.Parse(childNode.InnerText);
                    break;
            }
        }
        
        return new Bone() { Name = boneName, LockTranslation = lockTranslation };
    }

}