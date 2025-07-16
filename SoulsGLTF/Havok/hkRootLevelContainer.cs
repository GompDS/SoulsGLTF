using System.Xml;
using DarkSrc.Util.Havok;
using SoulsAssetPipeline.XmlStructs;

namespace SoulsGLTF.Havok;

public class hkRootLevelContainer : hkObject
{
    public class NamedVariant
    {
        public string Name { get; set; } = "";
        public string ClassName { get; set; } = "";
        public hkReferencedObject? Variant;
    }
    
    public override uint Signature => 0x2772c11e;
    public List<NamedVariant> NamedVariants = new();

    public hkRootLevelContainer(XmlNode node)
    {
        Name = node.SafeGetAttribute("name");
        XmlNode? namedVariantsNode = node.FirstChild;
        if (namedVariantsNode != null)
        {
            XmlNode? nextVariant = namedVariantsNode.FirstChild;
            while (nextVariant != null)
            {
                XmlNode? variantNode = nextVariant.FirstChild;
                if (variantNode != null)
                {
                    NamedVariant namedVariant = new NamedVariant();
                    namedVariant.Name = variantNode.InnerText;
                    variantNode = variantNode.NextSibling;
                    namedVariant.ClassName = variantNode.InnerText;
                    variantNode = variantNode.NextSibling;
                    if (namedVariant.ClassName == "hkaAnimationContainer")
                    {
                        namedVariant.Variant = new hkaAnimationContainer();
                    }

                    if (namedVariant.Variant != null)
                    {
                        namedVariant.Variant.Name = variantNode.InnerText;
                    }
                    
                    NamedVariants.Add(namedVariant);
                }
                
                nextVariant = nextVariant.NextSibling;
            }
        }
    }
}