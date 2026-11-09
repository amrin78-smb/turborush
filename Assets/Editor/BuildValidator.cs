using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace TurboRush.EditorTools
{
    /// <summary>
    /// Post-build APK checks (release-blocking for the Redmi A3):
    ///  1. aapt dump badging → native-code must list arm64-v8a AND armeabi-v7a;
    ///  2. every file under assets/bin/Data/ is scanned for the PowerVR deny-filter pattern;
    ///  3. landscape orientation; 4. no INTERNET permission in release builds.
    /// Writes a report next to the APK (&lt;apk&gt;.validation.txt). Menu: Tools ▸ Turbo Rush ▸ Validate Build.
    /// </summary>
    public static class BuildValidator
    {
        [MenuItem("Tools/Turbo Rush/Validate Build", priority = 23)]
        public static void ValidateMenu()
        {
            bool any = false;
            foreach (string apk in new[] { BuildScript.AndroidReleasePath, BuildScript.AndroidDevPath })
            {
                if (!File.Exists(apk)) continue;
                any = true;
                ValidateApk(apk, apk == BuildScript.AndroidReleasePath);
            }
            if (!any) Debug.LogError("[TR Validate] No APK found. Build one first.");
        }

        public static void ValidateBatch()
        {
            string apk = Environment.GetEnvironmentVariable("TR_APK") ?? BuildScript.AndroidDevPath;
            EditorApplication.Exit(ValidateApk(apk, !apk.EndsWith("-dev.apk")) ? 0 : 1);
        }

        public static string FindAapt()
        {
            var roots = new List<string>
            {
                Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines/AndroidPlayer/SDK/build-tools"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Android/Sdk/build-tools")
            };
            string env = Environment.GetEnvironmentVariable("ANDROID_HOME");
            if (!string.IsNullOrEmpty(env)) roots.Add(Path.Combine(env, "build-tools"));
            string best = null;
            foreach (string root in roots)
            {
                if (!Directory.Exists(root)) continue;
                var dirs = Directory.GetDirectories(root);
                Array.Sort(dirs, StringComparer.Ordinal);
                for (int i = dirs.Length - 1; i >= 0; i--)
                {
                    string c = Path.Combine(dirs[i], "aapt");
                    if (File.Exists(c)) { best = c; break; }
                }
                if (best != null) break;
            }
            return best;
        }

        public static bool ValidateApk(string apk, bool release)
        {
            var report = new StringBuilder();
            bool ok = true;
            void Line(string s) { report.AppendLine(s); Debug.Log("[TR Validate] " + s); }
            void Fail(string s) { ok = false; report.AppendLine("FAIL " + s); Debug.LogError("[TR Validate] FAIL " + s); }

            Line($"APK: {apk}  ({DateTime.Now:yyyy-MM-dd HH:mm:ss})");
            if (!File.Exists(apk)) { Fail("APK not found"); return false; }
            Line($"Size: {new FileInfo(apk).Length / (1024f * 1024f):0.0} MB");

            // 1. ABIs via aapt.
            string aapt = FindAapt();
            if (aapt == null) Fail("aapt not found (install Android SDK Build Tools); ABI check NOT performed");
            else
            {
                string badging = Run(aapt, $"dump badging \"{apk}\"");
                string native = "";
                foreach (string l in badging.Split('\n'))
                {
                    if (l.StartsWith("native-code:") || l.StartsWith("package:") || l.StartsWith("sdkVersion") ||
                        l.StartsWith("targetSdkVersion") || l.StartsWith("uses-permission")) Line("  aapt> " + l.Trim());
                    if (l.StartsWith("native-code:")) native = l;
                }
                Line($"Command: {aapt} dump badging <apk> | grep native-code");
                if (native.Contains("'arm64-v8a'") && native.Contains("'armeabi-v7a'")) Line("OK   native-code lists arm64-v8a AND armeabi-v7a");
                else Fail("native-code must list both arm64-v8a and armeabi-v7a (got: " + native.Trim() + ")");

                if (badging.Contains("android.permission.INTERNET"))
                {
                    if (release) Fail("release APK requests INTERNET (game must be fully offline)");
                    else Line("INFO INTERNET permission present (development build: Unity profiler)");
                }
                else Line("OK   no INTERNET permission");

                string tree = Run(aapt, $"dump xmltree \"{apk}\" AndroidManifest.xml");
                string orient = "";
                foreach (string l in tree.Split('\n')) if (l.Contains("screenOrientation")) { orient = l.Trim(); break; }
                // 0x6 sensorLandscape, 0xb userLandscape, 0x0 landscape
                if (orient.Contains("0x6") || orient.Contains("0xb") || orient.EndsWith("0x0")) Line("OK   landscape orientation (" + orient + ")");
                else Fail("orientation is not landscape: " + orient);
            }

            // 2. PowerVR filter in packaged player data.
            byte[] needle = Encoding.UTF8.GetBytes(ProjectConfigurator.PowerVrPattern);
            var hits = new List<string>();
            int scanned = 0;
            using (ZipArchive zip = ZipFile.OpenRead(apk))
            {
                foreach (ZipArchiveEntry e in zip.Entries)
                {
                    if (!e.FullName.StartsWith("assets/bin/Data/") || e.Length == 0) continue;
                    scanned++;
                    using (Stream s = e.Open())
                    using (var ms = new MemoryStream())
                    {
                        s.CopyTo(ms);
                        if (IndexOf(ms.GetBuffer(), (int)ms.Length, needle) >= 0) hits.Add(e.FullName);
                    }
                }
            }
            Line($"Scanned {scanned} files under assets/bin/Data/ for \"{ProjectConfigurator.PowerVrPattern}\"");
            if (hits.Count > 0) Line("OK   PowerVR Vulkan deny filter found in: " + string.Join(", ", hits));
            else Fail("PowerVR Vulkan deny filter pattern NOT found in packaged player data");

            Line(ok ? "RESULT: PASS" : "RESULT: FAIL");
            File.WriteAllText(apk + ".validation.txt", report.ToString());
            return ok;
        }

        private static int IndexOf(byte[] hay, int len, byte[] needle)
        {
            for (int i = 0; i <= len - needle.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && hay[i + j] == needle[j]) j++;
                if (j == needle.Length) return i;
            }
            return -1;
        }

        private static string Run(string exe, string args)
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true
            };
            using (Process p = Process.Start(psi))
            {
                string o = p.StandardOutput.ReadToEnd();
                string err = p.StandardError.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode != 0) Debug.LogWarning($"[TR Validate] {Path.GetFileName(exe)} exit {p.ExitCode}: {err}");
                return o;
            }
        }
    }
}
