using System.Diagnostics;
using System.Xml;
using System.Xml.Serialization;
using DarkSrc.Util.Havok;
using SoulsAssetPipeline.XmlStructs;
using SoulsGLTF.Util;

namespace SoulsGLTF.Havok;

[XmlRoot("hkpackfile")]
public class hkPackFile
{
    public int ClassVersion;
    public string ContentsVersion = "";
    public hkRootLevelContainer? RootLevelContainer;
    
    public static bool IsRead(string filePath, out hkPackFile? packFile)
    {
        packFile = null;

        XmlDocument doc = new XmlDocument();
        doc.Load(filePath);

        return _isReadInternal(doc, out packFile);
    }
    
    public static bool IsRead(byte[] bytes, out hkPackFile? packFile, byte[]? compendiumBytes = null)
    {
        packFile = null;

        Directory.CreateDirectory("$temp");
        File.WriteAllBytes("$temp/pack.hkx", bytes);
        string cwd = AppDomain.CurrentDomain.BaseDirectory;
        Process hkxPack = new Process()
        {
            StartInfo = new ProcessStartInfo("ExternalTools/hkxpack-souls/hkxpack-souls.exe")
            {
                Arguments = "$temp/pack.hkx",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        hkxPack.Start();
        hkxPack.WaitForExit();
        
        // Copy compendium
        /*if (compendiumBytes != null)
        {
            File.WriteAllBytes($"{cwd}$temp\\compendium.hkx", compendiumBytes);
        }*/
        
        // FileConvert
        /*Process fileConvert = new Process()
        {
            StartInfo = new ProcessStartInfo("ExternalTools/FileConvert.exe")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };*/

        /*if (File.Exists($"{cwd}$temp\\compendium.hkx"))
        {
            fileConvert.StartInfo.Arguments =
                $"-x --compendium {cwd}$temp\\compendium.hkx {cwd}$temp\\pack.hkx {cwd}$temp\\pack.xml";
        }
        else
        {
            fileConvert.StartInfo.Arguments = $"-x {cwd}$temp\\pack.hkx {cwd}$temp\\pack.xml";
        }
        
        fileConvert.Start();
        fileConvert.WaitForExit();*/

        if (!File.Exists("$temp/pack.xml"))
        {
            return false;
        }
        
        XmlDocument doc = new XmlDocument();
        doc.Load("$temp/pack.xml");
        
        DirectoryUtils.DeleteRecursive("$temp");
        

        return _isReadInternal(doc, out packFile);
    }
    
    private static bool _isReadInternal(XmlDocument doc, out hkPackFile? packFile)
    {
        packFile = null;
        
        XmlNode? rootNode = doc.FirstChild;
        if (rootNode == null) return false;
        
        XmlNode? packFileNode = rootNode.NextSibling;
        
        if (packFileNode?.Name is not "hkpackfile" and not "hktagfile") return false;
        
        packFile = new hkPackFile
        {
            ClassVersion = int.Parse(packFileNode.SafeGetAttribute("classversion")),
            ContentsVersion = packFileNode.SafeGetAttribute("contentsversion")
        };

        XmlNode? nextSection = packFileNode.FirstChild;
        while (nextSection != null)
        {
            string sectionName = nextSection.SafeGetAttribute("name");
            if (sectionName == "__data__")
            {
                XmlNode? nextSectionObject = nextSection.FirstChild;
                if (nextSectionObject != null)
                {
                    hkRootLevelContainer rootLevelContainer = new(nextSectionObject);

                    for (int i = 0; i < rootLevelContainer.NamedVariants.Count; i++)
                    {
                        if (rootLevelContainer.NamedVariants[i].Variant != null)
                        {
                            nextSectionObject = nextSectionObject.NextSibling;
                            nextSectionObject = rootLevelContainer.NamedVariants[i].Variant.ReadXml(nextSectionObject);
                        }
                    }
                    
                    packFile.RootLevelContainer = rootLevelContainer;
                }
            }
            
            nextSection = nextSection.NextSibling;
        }
       
        return true;
    }
}