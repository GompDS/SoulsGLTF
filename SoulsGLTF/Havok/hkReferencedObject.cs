using System;
using System.Xml;

namespace DarkSrc.Util.Havok;

public class hkReferencedObject : hkObject
{
    public virtual XmlNode? ReadXml(XmlNode node)
    {
        throw new NotImplementedException();
    }
}