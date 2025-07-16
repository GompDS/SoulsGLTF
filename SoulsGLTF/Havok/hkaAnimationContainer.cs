using System.Xml;
using DarkSrc.Util.Havok;
using SoulsAssetPipeline.XmlStructs;

namespace SoulsGLTF.Havok;

public class hkaAnimationContainer : hkReferencedObject
{
    public override uint Signature => 0x26859f4c;

    public hkaSkeleton[] Skeletons { get; set; }
    public hkaAnimation[] Animations { get; set; }
    public hkaAnimationBinding[] Bindings { get; set; }
    
    public override XmlNode? ReadXml(XmlNode node)
    {
        XmlNode nextSibling = node.NextSibling;
        
        foreach (XmlNode childNode in node.ChildNodes)
        {
            string arrayName = childNode.SafeGetAttribute("name");
            int arrayNumElements = int.Parse(childNode.SafeGetAttribute("numelements"));
            if (arrayName == "skeletons")
            {
                Skeletons = new hkaSkeleton[arrayNumElements];
                for (int i = 0; i < arrayNumElements; i++)
                {
                    hkaSkeleton skeleton = new hkaSkeleton();
                    skeleton.Name = nextSibling.SafeGetAttribute("name");
                    nextSibling = skeleton.ReadXml(nextSibling);
                    
                    Skeletons[i] = skeleton;
                }
            }
            else if (arrayName == "animations")
            {
                Animations = new hkaAnimation[arrayNumElements];
                for (int i = 0; i < arrayNumElements; i++)
                {
                    hkaAnimation? animation = null;
                    if (nextSibling.SafeGetAttribute("class") == "hkaSplineCompressedAnimation")
                    {
                        animation = new hkaSplineCompressedAnimation();
                        animation.Name = nextSibling.SafeGetAttribute("name");
                        nextSibling = animation.ReadXml(nextSibling);
                    }

                    if (animation != null)
                    {
                        Animations[i] = animation;
                    }
                }
            }
            else if (arrayName == "bindings")
            {
                Bindings = new hkaAnimationBinding[arrayNumElements];
                for (int i = 0; i < arrayNumElements; i++)
                {
                    hkaAnimationBinding binding = new hkaAnimationBinding();
                    binding.Name = nextSibling.SafeGetAttribute("name");
                    nextSibling = binding.ReadXml(nextSibling);
                    binding.Animation = Animations.First(x => x.Name == binding.Animation.Name);
                    
                    Bindings[i] = binding;
                }
            }
        }
        
        return node.NextSibling;
    }
    
}