using System.IO;
using UnityEditor;
using UnityEngine;

namespace TurboRush.EditorTools
{
    /// <summary>Re-runnable asset helpers: generated assets keep their GUIDs between setup runs.</summary>
    public static class EditorAssets
    {
        public static void EnsureFolder(string path) => ProjectConfigurator.EnsureFolder(path);

        public static T LoadOrCreateSO<T>(string path) where T : ScriptableObject
        {
            EnsureFolder(Path.GetDirectoryName(path));
            T so = AssetDatabase.LoadAssetAtPath<T>(path);
            if (so != null) return so;
            so = ScriptableObject.CreateInstance<T>();
            so.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(so, path);
            return so;
        }

        public static Mesh SaveMesh(Mesh mesh, string path)
        {
            EnsureFolder(Path.GetDirectoryName(path));
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = Path.GetFileNameWithoutExtension(path);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        public static Material Material(string path, Shader shader)
        {
            EnsureFolder(Path.GetDirectoryName(path));
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader;
            m.enableInstancing = false; // SRP Batcher handles it
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Writes a PNG and imports it (as a sprite when border/sprite settings are given).</summary>
        public static Texture2D SavePng(Texture2D tex, string path, bool sprite, Vector4 border = default, bool mipmaps = false,
            TextureWrapMode wrap = TextureWrapMode.Clamp, int maxSize = 1024, float ppu = 100f)
        {
            EnsureFolder(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            imp.spriteImportMode = sprite ? SpriteImportMode.Single : SpriteImportMode.None;
            imp.spriteBorder = border;
            imp.spritePixelsPerUnit = ppu;
            imp.mipmapEnabled = mipmaps;
            imp.wrapMode = wrap;
            imp.alphaIsTransparency = true;
            imp.sRGBTexture = true;
            imp.maxTextureSize = maxSize;
            imp.textureCompression = TextureImporterCompression.Compressed;
            var android = imp.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = maxSize;
            android.format = TextureImporterFormat.ASTC_6x6;
            imp.SetPlatformTextureSettings(android);
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static void Set(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) throw new System.Exception($"Field '{field}' not found on {target.GetType().Name}");
            switch (value)
            {
                case Object o: p.objectReferenceValue = o; break;
                case float f: p.floatValue = f; break;
                case int i: p.intValue = i; break;
                case bool b: p.boolValue = b; break;
                case string s: p.stringValue = s; break;
                default: throw new System.Exception("Unsupported type for " + field);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
