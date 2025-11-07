using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

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
            if (!Directory.Exists("Assets/AssetBundles/HasteModels"))
            {
                Directory.CreateDirectory("Assets/AssetBundles/HasteModels");
            }
            if (BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64))
            {
                BuildPipeline.BuildAssetBundles("Assets/AssetBundles", BuildAssetBundleOptions.None, BuildTarget.StandaloneLinux64);
                CopyPlatformBundle(".linux");
            }
            else
            {
                Debug.LogWarning("Install the Linux Build Support (Mono) module in Unity Hub to build asset bundles for Linux");
            }
            if (BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX))
            {
                BuildPipeline.BuildAssetBundles("Assets/AssetBundles", BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX);
                CopyPlatformBundle(".mac");
            }
            else
            {
                Debug.LogWarning("Install the Mac Build Support (Mono) module in Unity Hub to build asset bundles for Mac");
            }
            if (BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            {
                BuildPipeline.BuildAssetBundles("Assets/AssetBundles", BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows64);
                CopyPlatformBundle(".windows");
            }
        }
        
        static void CopyPlatformBundle(string extension)
        {
            foreach (var bundleName in AssetDatabase.GetAllAssetBundleNames()
                         .Where(x => x.EndsWith(".hastemodel") &&
                                     !x.EndsWith(".windows.hastemodel") &&
                                     !x.EndsWith(".mac.hastemodel") &&
                                     !x.EndsWith(".linux.hastemodel")))
            {
                var newName = bundleName.Remove(bundleName.LastIndexOf('.')) + extension + ".hastemodel";
                if(AssetDatabase.AssetPathExists($"Assets/AssetBundles/{bundleName}"))
                {
                    AssetDatabase.CopyAsset($"Assets/AssetBundles/{bundleName}", $"Assets/AssetBundles/HasteModels/{newName}");
                }
            }
        }
    }
}