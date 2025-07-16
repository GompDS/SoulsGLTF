using SoulsFormats;
using SoulsGLTF.Havok;

namespace SoulsGLTF;

public static class HavokImporting
{
    public static void ExtractHkObjectsFromFiles(string[] hkFilePaths, out hkaSkeleton? skeleton,
        out List<hkaAnimation> animations)
    {
        skeleton = null;
        animations = new List<hkaAnimation>();
        
        foreach (string path in hkFilePaths)
        {
            if (hkPackFile.IsRead(path, out hkPackFile? packFile))
            {
                if (packFile?.RootLevelContainer?.NamedVariants[0].Variant is hkaAnimationContainer
                    animContainer)
                {
                    if (animContainer.Skeletons.Length > 0)
                    {
                        skeleton = animContainer.Skeletons[0];
                    }

                    if (animContainer.Animations.Length > 0)
                    {
                        animations = new List<hkaAnimation>();
                                    
                        foreach (hkaAnimation anim in animContainer.Animations)
                        {
                            animations.Add(anim);
                        }
                    }
                }
            }
        }
    }

    public static void ExtractHkFilesFromBinders(string[] animationBinderPaths, out hkaSkeleton? skeleton,
        out List<hkaAnimation> animations)
    {
        skeleton = null;
        animations = new List<hkaAnimation>();
        
        foreach (string path in animationBinderPaths)
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
                BinderFile? compendiumFile = bnd.Files.FirstOrDefault(x => x.Name.EndsWith(".compendium", StringComparison.OrdinalIgnoreCase));
                
                foreach (BinderFile bf in bnd.Files)
                {
                    if (bf.Name.EndsWith(".hkx", StringComparison.OrdinalIgnoreCase))
                    {
                        ReadBinderPackFile(bf, out hkaSkeleton? skeletonOut, out List<hkaAnimation> animationsOut, compendiumFile);
                        if (skeletonOut != null) skeleton = skeletonOut;
                        if (animationsOut.Count > 0) animations?.AddRange(animationsOut);
                    }
                    else if (bf.Name.EndsWith("bnd", StringComparison.OrdinalIgnoreCase))
                    {
                        ExtractHkFilesFromBinderRecursive(bf, out hkaSkeleton? skeletonOut, out List<hkaAnimation> animationsOut);
                        if (skeletonOut != null) skeleton = skeletonOut;
                        if (animationsOut.Count > 0) animations?.AddRange(animationsOut);
                    }
                }
            }
        }
    }
    
    private static void ExtractHkFilesFromBinderRecursive(BinderFile binderBf, out hkaSkeleton? skeleton,
        out List<hkaAnimation> animations)
    {
        skeleton = null;
        animations = new List<hkaAnimation>();
        
        IBinder? bnd = null;
        if (BND4.Is(binderBf.Bytes))
        {
            bnd = BND4.Read(binderBf.Bytes);
        }
        else
        {
            bnd = BND3.Read(binderBf.Bytes);
        }
            
        if (bnd != null)
        {
            BinderFile? compendiumFile = bnd.Files.FirstOrDefault(x => x.Name.EndsWith(".compendium", StringComparison.OrdinalIgnoreCase));
            
            foreach (BinderFile bf in bnd.Files)
            {
                if (bf.Name.EndsWith(".hkx", StringComparison.OrdinalIgnoreCase))
                {
                    ReadBinderPackFile(bf, out hkaSkeleton? skeletonOut, out List<hkaAnimation> animationsOut, compendiumFile);
                    if (skeletonOut != null) skeleton = skeletonOut;
                    if (animationsOut.Count > 0) animations?.AddRange(animationsOut);
                }
                else if (bf.Name.EndsWith("bnd", StringComparison.OrdinalIgnoreCase))
                {
                    ExtractHkFilesFromBinderRecursive(bf, out hkaSkeleton? skeletonOut, out List<hkaAnimation> animationsOut);
                    if (skeletonOut != null) skeleton = skeletonOut;
                    if (animationsOut.Count > 0) animations?.AddRange(animationsOut);
                }
            }
        }
    }

    private static void ReadBinderPackFile(BinderFile bf, out hkaSkeleton? skeleton, out List<hkaAnimation> animations, BinderFile? compendiumFile = null)
    {
        skeleton = null;
        animations = new List<hkaAnimation>();
        
        if (hkPackFile.IsRead(bf.Bytes, out hkPackFile? packFile, compendiumFile?.Bytes))
        {
            if (packFile?.RootLevelContainer?.NamedVariants[0].Variant is hkaAnimationContainer
                animContainer)
            {
                if (animContainer.Skeletons.Length > 0)
                {
                    skeleton = animContainer.Skeletons[0];
                }

                if (animContainer.Animations.Length > 0)
                {
                    animations = new List<hkaAnimation>();
                                    
                    foreach (hkaAnimation anim in animContainer.Animations)
                    {
                        animations.Add(anim);
                    }
                }
            }
        }
    }
}