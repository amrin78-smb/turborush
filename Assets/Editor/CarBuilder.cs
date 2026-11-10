using System.Collections.Generic;
using TurboRush.Core;
using TurboRush.Track;
using TurboRush.Vehicle;
using UnityEditor;
using UnityEngine;

namespace TurboRush.EditorTools
{
    /// <summary>
    /// Builds the eight car prefabs from the Blender models (Art/Cars/&lt;id&gt;/Car_&lt;id&gt;.fbx, made by Tools/Blender or Kimi).
    /// Each FBX part is converted into the game's one-material vertex-colour format: flat-shaded, colour per material
    /// slot (Paint faces carry the paint mask so the car material recolours them, lights glow via UV2.x).
    /// Prefab: root (RacerView, LODGroup) ▸ Body (leans) ▸ Wheel_FL/FR/RL/RR (spin, front ones steer) + Body_LOD1, and Shadow.
    /// </summary>
    public static class CarBuilder
    {
        public const string SourceFolder = "Assets/Art/Models/Vehicles/Source";
        public static readonly string[] WheelNames = { "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR" };

        private struct Spec
        {
            public string id, name;
            public CarBodyStyle style;
            public Color paint, accent;
            public int stars;
            public Spec(string id, string name, CarBodyStyle s, string p, string a, int stars)
            {
                this.id = id; this.name = name; style = s; this.stars = stars;
                ColorUtility.TryParseHtmlString(p, out paint);
                ColorUtility.TryParseHtmlString(a, out accent);
            }
        }

        // Colours are the art-brief colours (Docs/ArtBriefs/KimiCarPrompts), so the models and the game agree.
        private static readonly Spec[] Specs =
        {
            new Spec("blaze", "Blaze", CarBodyStyle.Wedge, "#ED291F", "#FFFFFF", 0),
            new Spec("zoom", "Zoom", CarBodyStyle.Bubble, "#1A73F5", "#FFD91A", 1),
            new Spec("sunny", "Sunny", CarBodyStyle.Muscle, "#FFCC14", "#1F1F26", 2),
            new Spec("mint", "Mint", CarBodyStyle.Wedge, "#2ECC85", "#FFFFFF", 4),
            new Spec("grape", "Grape", CarBodyStyle.Bubble, "#8F4CEB", "#FF99E6", 5),
            new Spec("tango", "Tango", CarBodyStyle.Muscle, "#FF7A14", "#FFFFFF", 7),
            new Spec("frost", "Frost", CarBodyStyle.Wedge, "#E6F2FF", "#26A6FF", 8),
            new Spec("bolt", "Bolt", CarBodyStyle.Bubble, "#FF5CA8", "#33E6FF", 9),
        };

        /// <summary>Fixed colours of the shared material slots (sRGB hex from the art brief).</summary>
        private static readonly Dictionary<string, string> SlotColors = new Dictionary<string, string>
        {
            { "Trim", "#26262E" }, { "Glass", "#1F2B47" }, { "Tyre", "#1A1A1F" }, { "Rim", "#8C93A3" },
            { "HeadLight", "#FFF7D6" }, { "TailLight", "#FF2A1F" }, { "Glow", "#33E6FF" },
        };

        public static CarDefinition[] BuildAll(RaceMaterials mats)
        {
            var defs = new List<CarDefinition>();
            Mesh shadow = ShadowQuad();
            foreach (Spec sp in Specs)
            {
                string fbx = $"{SourceFolder}/Car_{sp.id}.fbx";
                AssetDatabase.ImportAsset(fbx, ImportAssetOptions.ForceUpdate); // applies CarModelImport settings
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                if (model == null) throw new System.Exception("Car model missing: " + fbx + " (run Tools/setup.sh, which copies Art/Cars/*/Car_*.fbx)");

                Material mat = EditorAssets.Material($"Assets/Art/Materials/Cars/Car_{sp.id}.mat", ArtBuilder.VertexLit);
                mat.CopyPropertiesFromMaterial(mats.carBase);
                mat.SetColor("_Paint", sp.paint);
                mat.SetFloat("_Gloss", 0.55f);
                EditorUtility.SetDirty(mat);

                GameObject prefab = Prefab(sp, model, mat, shadow, mats.blobShadow);
                CarDefinition def = EditorAssets.LoadOrCreateSO<CarDefinition>($"Assets/ScriptableObjects/Cars/Car_{sp.id}.asset");
                def.id = sp.id;
                def.displayName = sp.name;
                def.bodyStyle = sp.style;
                def.paint = sp.paint;
                def.accent = sp.accent;
                def.unlockStars = sp.stars;
                def.prefab = prefab;
                def.tuning = CarTuning.Default; // equal performance: cars are cosmetic (fair, no pay-to-win)
                EditorUtility.SetDirty(def);
                defs.Add(def);
            }
            AssetDatabase.SaveAssets();
            return defs.ToArray();
        }

        private static GameObject Prefab(Spec sp, GameObject model, Material mat, Mesh shadowMesh, Material shadowMat)
        {
            string dir = "Assets/Art/Models/Vehicles/" + sp.id;
            Transform src = model.transform;
            var root = new GameObject("Car_" + sp.id);
            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);

            // The game's cars face +Z with their left at -X. The FBX importer's axis conversion can turn or mirror the model,
            // so derive the correction from where the named wheels landed (a mirror also flips the triangle winding).
            Vector3 fl = src.InverseTransformPoint(Find(src, "Wheel_FL").position), rl = src.InverseTransformPoint(Find(src, "Wheel_RL").position);
            Matrix4x4 face = Matrix4x4.Scale(new Vector3(fl.x < 0f ? 1f : -1f, 1f, fl.z > rl.z ? 1f : -1f));

            var lod0 = new List<Renderer>();
            Mesh bodyMesh = EditorAssets.SaveMesh(Convert(Find(src, "Body"), src, face, Vector3.zero, sp, "Body"), $"{dir}/Car_{sp.id}_Body.asset");
            lod0.Add(AddRenderer(body.transform, bodyMesh, mat));

            var wheels = new Transform[4];
            float radius = 0f;
            for (int i = 0; i < 4; i++)
            {
                Transform w = Find(src, WheelNames[i]);
                Vector3 centre = face.MultiplyPoint3x4(src.InverseTransformPoint(w.position));
                Mesh wm = EditorAssets.SaveMesh(Convert(w, src, face, centre, sp, WheelNames[i]), $"{dir}/Car_{sp.id}_{WheelNames[i]}.asset");
                var go = new GameObject(WheelNames[i]);
                go.transform.SetParent(body.transform, false);
                go.transform.localPosition = centre;
                lod0.Add(AddRenderer(go.transform, wm, mat));
                wheels[i] = go.transform;
                radius = Mathf.Max(radius, centre.y);
            }

            var lodGo = new GameObject("Body_LOD1");
            lodGo.transform.SetParent(body.transform, false);
            Mesh lodMesh = EditorAssets.SaveMesh(Convert(Find(src, "Body_LOD1"), src, face, Vector3.zero, sp, "LOD1"), $"{dir}/Car_{sp.id}_LOD1.asset");
            Renderer lod1 = AddRenderer(lodGo.transform, lodMesh, mat);

            var group = root.AddComponent<LODGroup>();
            group.SetLODs(new[]
            {
                new LOD(0.10f, lod0.ToArray()),   // full model while the car is more than 10 % of the screen height
                new LOD(0.006f, new[] { lod1 }),  // simplified model further away, culled when tiny
            });
            group.RecalculateBounds();

            var shadow = new GameObject("Shadow");
            shadow.transform.SetParent(root.transform, false);
            shadow.transform.localPosition = new Vector3(0, 0.04f, 0);
            shadow.AddComponent<MeshFilter>().sharedMesh = shadowMesh;
            var smr = shadow.AddComponent<MeshRenderer>();
            smr.sharedMaterial = shadowMat;
            Lean(smr);

            var renderers = new List<Renderer>(lod0) { lod1 };
            var view = root.AddComponent<RacerView>();
            view.Bind(body.transform, shadow.transform, renderers.ToArray(), wheels, radius);
            string path = $"Assets/Prefabs/Vehicles/Car_{sp.id}.prefab";
            EditorAssets.EnsureFolder("Assets/Prefabs/Vehicles");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                Transform f = Find(c, name);
                if (f != null) return f;
            }
            if (t.parent == null) throw new System.Exception($"{t.name}: part '{name}' not found in the car model");
            return null;
        }

        private static MeshRenderer AddRenderer(Transform t, Mesh mesh, Material mat)
        {
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = t.gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            Lean(mr);
            return mr;
        }

        /// <summary>
        /// Converts an FBX part into a flat-shaded vertex-colour mesh in car space (relative to <paramref name="origin"/>):
        /// one vertex per triangle corner, colour from the material slot name, paint mask on Paint, emission on lights.
        /// </summary>
        public static Mesh Convert(Transform part, Transform car, Matrix4x4 face, Vector3 origin, Color accent, string name)
        {
            var mf = part.GetComponent<MeshFilter>();
            var mr = part.GetComponent<MeshRenderer>();
            if (mf == null || mr == null || mf.sharedMesh == null) throw new System.Exception(part.name + " has no mesh");
            Mesh src = mf.sharedMesh;
            Matrix4x4 m = face * car.worldToLocalMatrix * part.localToWorldMatrix;
            Vector3[] v = src.vertices;
            var kit = new MeshKit();
            Material[] slots = mr.sharedMaterials;
            for (int s = 0; s < src.subMeshCount; s++)
            {
                string slot = s < slots.Length && slots[s] != null ? slots[s].name.Split('.')[0].Split(' ')[0] : "Trim";
                Color col = SlotColor(slot, accent, out bool paint, out bool glow);
                kit.PaintMask = paint;
                kit.Emission = glow ? 1f : 0f;
                int[] tris = src.GetTriangles(s);
                bool flip = face.determinant < 0f;
                for (int i = 0; i < tris.Length; i += 3)
                {
                    Vector3 a = m.MultiplyPoint3x4(v[tris[i]]) - origin, b = m.MultiplyPoint3x4(v[tris[i + 1]]) - origin, c = m.MultiplyPoint3x4(v[tris[i + 2]]) - origin;
                    if (flip) kit.Tri(a, c, b, col); else kit.Tri(a, b, c, col);
                }
            }
            return kit.ToMesh(name);
        }

        private static Mesh Convert(Transform part, Transform car, Matrix4x4 face, Vector3 origin, Spec sp, string name) =>
            Convert(part, car, face, origin, sp.accent, $"Car_{sp.id}_{name}");

        public static Color SlotColor(string slot, Color accent, out bool paint, out bool glow)
        {
            paint = slot == "Paint";
            glow = slot == "HeadLight" || slot == "TailLight" || slot == "Glow";
            Color c = Color.white;
            if (slot == "Accent") c = accent;
            else if (SlotColors.TryGetValue(slot, out string hex)) ColorUtility.TryParseHtmlString(hex, out c);
            // Vertex colours are used as-is by the shader (linear project), so convert the sRGB brief colours.
            return paint ? Color.white : c.linear;
        }

        private static void Lean(MeshRenderer r)
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        private static Mesh ShadowQuad()
        {
            var m = new Mesh { name = "CarShadow" };
            m.vertices = new[] { new Vector3(-1.35f, 0, -2.6f), new Vector3(-1.35f, 0, 2.6f), new Vector3(1.35f, 0, 2.6f), new Vector3(1.35f, 0, -2.6f) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.RecalculateBounds();
            return EditorAssets.SaveMesh(m, "Assets/Art/Models/Vehicles/CarShadow.asset");
        }
    }
}
