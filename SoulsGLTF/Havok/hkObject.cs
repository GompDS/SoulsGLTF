using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace DarkSrc.Util.Havok;

[XmlRoot("hkobject")]
public class hkObject
{
    public string Name = "";
    
    public virtual uint Signature => 0x0;
}