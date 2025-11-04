using System.IO;
using UnityEditor;
namespace AethaModelSwapMod
{
    public static class CreateAssetBundles
    {
        [MenuItem("Assets/Build Asset Bundles")]
        static void BuildAllAssetBundles()
        {
            if (!Directory.Exists("Assets/AssetBundles"))
            {
                Directory.CreateDirectory("Assets/AssetBundles");
            }
            BuildPipeline.BuildAssetBundles("Assets/AssetBundles", BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows64);
        }
    }
}