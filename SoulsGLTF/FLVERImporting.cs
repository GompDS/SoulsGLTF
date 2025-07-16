using SoulsFormats;

namespace SoulsGLTF;

public static class FLVERImporting
{
    public static void ExtractFLVERsFromFiles(string[] modelPaths, out Dictionary<string, FLVER2> flvers)
    {
        flvers = new Dictionary<string, FLVER2>();
        
        foreach (string path in modelPaths)
        {
            if (FLVER2.IsRead(path, out FLVER2 flver))
            {
                flvers.Add(Path.GetFileNameWithoutExtension(path), flver);
            }
        }
    }
    
    public static void ExtractFLVERsFromBinders(string[] modelBinderPaths, out Dictionary<string, FLVER2> flvers)
    {
        flvers = new Dictionary<string, FLVER2>();
        
        foreach (string path in modelBinderPaths)
        {
            IBinder? bnd = null;
            if (BND4.Is(path))
            {
                bnd = BND4.Read(path);
            }
            else if (BND3.Is(path))
            {
                bnd = BND3.Read(path);
            }
            
            if (bnd != null)
            {
                foreach (BinderFile bf in bnd.Files)
                {
                    if (bf.Name.EndsWith(".flver", StringComparison.OrdinalIgnoreCase))
                    {
                        if (FLVER2.IsRead(bf.Bytes, out FLVER2 flver))
                        {
                            flvers.Add(Path.GetFileNameWithoutExtension(bf.Name), flver);
                        }
                    }
                }
            }
        }
    }
}