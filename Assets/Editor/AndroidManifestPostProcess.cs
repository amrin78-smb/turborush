using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

namespace TurboRush.EditorTools
{
    /// <summary>
    /// Release builds must not request network access (offline children's game). Unity modules can add
    /// android.permission.INTERNET; this strips it from the generated Gradle project and tells the manifest merger
    /// to drop it. Development builds keep it for the profiler.
    /// </summary>
    public sealed class AndroidManifestPostProcess : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
        {
            if (EditorUserBuildSettings.development) return;
            string library = Path.Combine(unityLibraryPath, "src", "main", "AndroidManifest.xml");
            string launcher = Path.GetFullPath(Path.Combine(unityLibraryPath, "..", "launcher", "src", "main", "AndroidManifest.xml"));
            foreach (string manifest in new[] { library, launcher })
            {
                if (!File.Exists(manifest)) continue;
                string xml = File.ReadAllText(manifest);
                xml = Regex.Replace(xml, @"\s*<uses-permission[^>]*android\.permission\.INTERNET[^>]*/>", "");
                if (!xml.Contains("xmlns:tools=")) xml = xml.Replace("<manifest ", "<manifest xmlns:tools=\"http://schemas.android.com/tools\" ");
                xml = Regex.Replace(xml, @"(<manifest[^>]*>)", "$1\n  <uses-permission android:name=\"android.permission.INTERNET\" tools:node=\"remove\" />");
                File.WriteAllText(manifest, xml);
            }
            Debug.Log("[TR Build] Release build: INTERNET permission removed (offline game).");
        }
    }
}
