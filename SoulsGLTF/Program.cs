using SoulsFormats;
using SoulsGLTF.Havok;

namespace SoulsGLTF;

public static class Program
{
    public static void Main(string[] args)
    {
        // Check for starting arg options

        bool binaryOutput = args.Length > 0 && args[0] == "--b";
        bool consolidateBuffers = args.Length > 0 && args[0] == "--c";
        
        // Determine Make-up of Args
        
        string[] validArgs = args.Where(File.Exists).ToArray();
        string[] binderPaths = validArgs.Where(x => 
            x.EndsWith("bnd", StringComparison.OrdinalIgnoreCase) ||
            x.EndsWith("bnd.dcx", StringComparison.OrdinalIgnoreCase)).ToArray();
        string[] flverPaths = validArgs.Where(x => x.EndsWith("flver", StringComparison.OrdinalIgnoreCase)).ToArray();
        string[] hkxPaths = validArgs.Where(x => x.EndsWith(".hkx", StringComparison.OrdinalIgnoreCase)).ToArray();

        // Define Input Variables
        
        hkaSkeleton? skeleton = null;
        List<hkaAnimation> animations = new List<hkaAnimation>();
        Dictionary<string, FLVER2> flvers = new Dictionary<string, FLVER2>();
        
        // Import from binders and loose files
        
        if (binderPaths.Length > 0)
        {
            HavokImporting.ExtractHkFilesFromBinders(binderPaths, out hkaSkeleton? skeletonOut, out List<hkaAnimation> animationsOut);
            if (skeletonOut != null) skeleton = skeletonOut;
            if (animationsOut.Count > 0) animations.AddRange(animationsOut);
            FLVERImporting.ExtractFLVERsFromBinders(binderPaths, out Dictionary<string, FLVER2> flversOut);
            if (flversOut.Count > 0)
            {
                foreach (KeyValuePair<string, FLVER2> kvp in flversOut)
                {
                    flvers.TryAdd(kvp.Key, kvp.Value);
                }
            }
        }

        if (hkxPaths.Length > 0)
        {
            HavokImporting.ExtractHkObjectsFromFiles(hkxPaths, out hkaSkeleton? skeletonOut,
                out List<hkaAnimation> animationsOut);
            if (skeletonOut != null) skeleton = skeletonOut;
            if (animationsOut.Count > 0) animations.AddRange(animationsOut);
        }
        
        if (flverPaths.Length > 0)
        {
            FLVERImporting.ExtractFLVERsFromFiles(flverPaths, out Dictionary<string, FLVER2> flversOut);
            if (flversOut.Count > 0)
            {
                foreach (KeyValuePair<string, FLVER2> kvp in flversOut)
                {
                    flvers.TryAdd(kvp.Key, kvp.Value);
                }
            }
        }
        
        // Export each flver to gltf
        
        foreach (KeyValuePair<string, FLVER2> kvp in flvers)
        {
            FLVERExporting.ExportFLVERToGLTF(kvp.Value, skeleton, animations, kvp.Key, binaryOutput, consolidateBuffers);
        }
    }
}