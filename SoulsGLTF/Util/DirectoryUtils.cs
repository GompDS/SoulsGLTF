namespace SoulsGLTF.Util;

public static class DirectoryUtils
{
    /// <summary>
    /// Given the full path to a file, create any missing directories in the chain.
    /// </summary>
    public static void CreateAllDirectories(string fullPath)
    {
        string[] directories = fullPath.Split("/");
        for (int i = 0; i < directories.Length; i++)
        {
            string dirChain = "";
            for (int j = 0; j < i + 1; j++)
            {
                dirChain += directories[j] + "/";
                if (!Directory.Exists(dirChain))
                {
                    Directory.CreateDirectory(dirChain);
                }
            }
        }
    }

    /// <summary>
    /// Delete a directory and everything in it.
    /// </summary>
    public static void DeleteRecursive(string dirPath)
    {
        foreach (string subDirPath in Directory.EnumerateDirectories(dirPath))
        {
            DeleteRecursive(subDirPath);
        }

        foreach (string subFilePath in Directory.EnumerateFiles(dirPath))
        {
            File.Delete(subFilePath);
        }
        
        Directory.Delete(dirPath);
    }
}