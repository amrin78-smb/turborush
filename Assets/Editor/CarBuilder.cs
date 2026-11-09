using System.Collections.Generic;
using TurboRush.Core;
using TurboRush.Track;
using TurboRush.Vehicle;
using UnityEditor;
using UnityEngine;

namespace TurboRush.EditorTools
{
    /// <summary>
    /// Original stylised low-poly cars: a lofted body (paint-masked so one mesh serves every colour), glass cabin,
    /// chunky wheels, emissive tail/head lights, accent spoiler/skirts. Three silhouettes, eight colour schemes.
    /// </summary>
    public static class CarBuilder
    {
        private struct Spec
        {
            public string id, name;
            public CarBodyStyle style;
            public Color paint, accent;
            public int stars;
            public Spec(string id, string name, CarBodyStyle s, Color p, Color a, int stars) { this.id = id; this.name = name; style = s; paint = p; accent = a; this.stars = stars; }
        }

        private static readonly Spec[] Specs =
        {
            new Spec("blaze", "Blaze", CarBodyStyle.Wedge, new Color(0.93f, 0.16f, 0.12f), Color.white, 0),
            new Spec("zoom", "Zoom", CarBodyStyle.Bubble, new Color(0.1f, 0.45f, 0.96f), new Color(1f, 0.85f, 0.1f), 1),
            new Spec("sunny", "Sunny", CarBodyStyle.Muscle, new Color(1f, 0.8f, 0.08f), new Color(0.12f, 0.12f, 0.15f), 2),
            new Spec("mint", "Mint", CarBodyStyle.Wedge, new Color(0.18f, 0.8f, 0.52f), Color.white, 4),
            new Spec("grape", "Grape", CarBodyStyle.Bubble, new Color(0.56f, 0.3f, 0.92f), new Color(1f, 0.6f, 0.9f), 5),
            new Spec("tango", "Tango", CarBodyStyle.Muscle, new Color(1f, 0.48f, 0.08f), Color.white, 7),
            new Spec("frost", "Frost", CarBodyStyle.Wedge, new Color(0.9f, 0.95f, 1f), new Color(0.15f, 0.65f, 1f), 8),
            new Spec("bolt", "Bolt", CarBodyStyle.Bubble, new Color(1f, 0.36f, 0.66f), new Color(0.2f, 0.9f, 1f), 9),
        };

        public static CarDefinition[] BuildAll(RaceMaterials mats)
        {
            var meshes = new Dictionary<string, Mesh>();
            var defs = new List<CarDefinition>();
            Mesh shadow = ShadowQuad();
            foreach (Spec sp in Specs)
            {
                string key = sp.style + "_" + ColorUtility.ToHtmlStringRGB(sp.accent);
                if (!meshes.TryGetValue(key, out Mesh mesh))
                {
                    mesh = EditorAssets.SaveMesh(Body(sp.style, sp.accent), $"Assets/Art/Models/Vehicles/Car_{key}.asset");
                    meshes[key] = mesh;
                }
                Material mat = EditorAssets.Material($"Assets/Art/Materials/Cars/Car_{sp.id}.mat", ArtBuilder.VertexLit);
                mat.CopyPropertiesFromMaterial(mats.carBase);
                mat.SetColor("_Paint", sp.paint);
                mat.SetFloat("_Gloss", 0.55f);
                EditorUtility.SetDirty(mat);

                GameObject prefab = Prefab(sp, mesh, mat, shadow, mats.blobShadow);
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

        private static GameObject Prefab(Spec sp, Mesh mesh, Material mat, Mesh shadowMesh, Material shadowMat)
        {
            var root = new GameObject("Car_" + sp.id);
            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = body.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            Lean(mr);
            var shadow = new GameObject("Shadow");
            shadow.transform.SetParent(root.transform, false);
            shadow.transform.localPosition = new Vector3(0, 0.04f, 0);
            shadow.AddComponent<MeshFilter>().sharedMesh = shadowMesh;
            var smr = shadow.AddComponent<MeshRenderer>();
            smr.sharedMaterial = shadowMat;
            Lean(smr);
            var view = root.AddComponent<RacerView>();
            view.Bind(body.transform, shadow.transform, mr);
            string path = $"Assets/Prefabs/Vehicles/Car_{sp.id}.prefab";
            EditorAssets.EnsureFolder("Assets/Prefabs/Vehicles");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
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

        // ---------------- body construction ----------------

        private struct Station { public float z, w, b, t; public Station(float z, float w, float b, float t) { this.z = z; this.w = w; this.b = b; this.t = t; } }

        public static Mesh Body(CarBodyStyle style, Color accent)
        {
            Station[] st;
            float cab0, cab1, cabH, cabTopW, roofPaint;
            switch (style)
            {
                case CarBodyStyle.Bubble:
                    st = new[] { new Station(-1.95f, 0.82f, 0.3f, 0.86f), new Station(-1.6f, 0.92f, 0.26f, 0.98f), new Station(-0.6f, 0.95f, 0.26f, 1.0f),
                                 new Station(0.5f, 0.95f, 0.26f, 0.96f), new Station(1.45f, 0.9f, 0.27f, 0.8f), new Station(1.95f, 0.76f, 0.3f, 0.6f) };
                    cab0 = -1.35f; cab1 = 0.75f; cabH = 0.62f; cabTopW = 0.66f; roofPaint = 1f;
                    break;
                case CarBodyStyle.Muscle:
                    st = new[] { new Station(-2.15f, 0.9f, 0.26f, 0.9f), new Station(-1.8f, 0.97f, 0.24f, 0.94f), new Station(-0.9f, 0.98f, 0.24f, 0.9f),
                                 new Station(0.3f, 0.98f, 0.24f, 0.84f), new Station(1.6f, 0.96f, 0.25f, 0.8f), new Station(2.2f, 0.88f, 0.28f, 0.66f) };
                    cab0 = -1.35f; cab1 = 0.25f; cabH = 0.5f; cabTopW = 0.7f; roofPaint = 0f;
                    break;
                default: // Wedge
                    st = new[] { new Station(-2.1f, 0.86f, 0.25f, 0.84f), new Station(-1.7f, 0.95f, 0.22f, 0.9f), new Station(-0.7f, 0.97f, 0.22f, 0.84f),
                                 new Station(0.4f, 0.97f, 0.22f, 0.74f), new Station(1.45f, 0.92f, 0.23f, 0.58f), new Station(2.15f, 0.78f, 0.25f, 0.44f) };
                    cab0 = -1.2f; cab1 = 0.55f; cabH = 0.46f; cabTopW = 0.62f; roofPaint = 1f;
                    break;
            }

            var k = new MeshKit();
            Color white = Color.white, glass = new Color(0.12f, 0.17f, 0.28f), dark = new Color(0.1f, 0.1f, 0.12f), chrome = new Color(0.5f, 0.53f, 0.6f);

            // Lofted shell (paint).
            k.PaintMask = true;
            Vector2[] Section(Station s) => new[]
            {
                new Vector2(-s.w * 0.9f, s.b), new Vector2(-s.w, s.b + 0.16f), new Vector2(-s.w, s.t - 0.16f), new Vector2(-s.w * 0.84f, s.t),
                new Vector2(s.w * 0.84f, s.t), new Vector2(s.w, s.t - 0.16f), new Vector2(s.w, s.b + 0.16f), new Vector2(s.w * 0.9f, s.b)
            };
            for (int i = 0; i < st.Length - 1; i++)
            {
                Vector2[] a = Section(st[i]), b = Section(st[i + 1]);
                for (int j = 0; j < a.Length - 1; j++)
                {
                    Vector3 a0 = new Vector3(a[j].x, a[j].y, st[i].z), a1 = new Vector3(a[j + 1].x, a[j + 1].y, st[i].z);
                    Vector3 b0 = new Vector3(b[j].x, b[j].y, st[i + 1].z), b1 = new Vector3(b[j + 1].x, b[j + 1].y, st[i + 1].z);
                    k.Quad(a0, b0, b1, a1, white);
                }
            }
            // caps
            Vector2[] rear = Section(st[0]), front = Section(st[st.Length - 1]);
            for (int j = 1; j < rear.Length - 1; j++)
            {
                k.Tri(new Vector3(rear[0].x, rear[0].y, st[0].z), new Vector3(rear[j].x, rear[j].y, st[0].z), new Vector3(rear[j + 1].x, rear[j + 1].y, st[0].z), white);
                float zf = st[st.Length - 1].z;
                k.Tri(new Vector3(front[0].x, front[0].y, zf), new Vector3(front[j + 1].x, front[j + 1].y, zf), new Vector3(front[j].x, front[j].y, zf), white);
            }
            k.PaintMask = false;
            // underside
            float zr = st[0].z, zfr = st[st.Length - 1].z;
            k.Quad(new Vector3(0.8f, 0.23f, zr), new Vector3(0.8f, 0.23f, zfr), new Vector3(-0.8f, 0.23f, zfr), new Vector3(-0.8f, 0.23f, zr), dark);

            // Cabin: glass sides, painted or accent roof.
            float baseY = Mathf.Lerp(TopAt(st, cab0), TopAt(st, cab1), 0.5f) - 0.03f;
            float wb = 0.86f, wt = cabTopW;
            float r0 = cab0 + 0.35f, r1 = cab1 - 0.55f;
            Vector3 bl0 = new Vector3(-wb, TopAt(st, cab0) - 0.02f, cab0), br0 = new Vector3(wb, TopAt(st, cab0) - 0.02f, cab0);
            Vector3 bl1 = new Vector3(-wb, TopAt(st, cab1) - 0.02f, cab1), br1 = new Vector3(wb, TopAt(st, cab1) - 0.02f, cab1);
            float roofY = baseY + cabH;
            Vector3 tl0 = new Vector3(-wt, roofY, r0), tr0 = new Vector3(wt, roofY, r0), tl1 = new Vector3(-wt, roofY, r1), tr1 = new Vector3(wt, roofY, r1);
            k.Quad(bl1, tl1, tl0, bl0, glass);   // left side faces −x
            k.Quad(br0, tr0, tr1, br1, glass);   // right side
            k.Quad(br1, tr1, tl1, bl1, glass);   // windscreen faces +z
            k.Quad(bl0, tl0, tr0, br0, glass);   // rear window
            k.PaintMask = roofPaint > 0.5f;
            k.Quad(tl0, tl1, tr1, tr0, k.PaintMask ? white : accent); // roof
            k.PaintMask = false;
            // window pillars (thin painted strips) for a friendlier look
            k.PaintMask = true;
            Vector3 nOff = new Vector3(0f, 0.55f, 0.83f) * 0.012f;
            k.Quad(bl1 + new Vector3(0.16f, 0, 0) + nOff, tl1 + new Vector3(0.14f, 0, 0) + nOff, tl1 + nOff, bl1 + nOff, white);
            k.Quad(br1 + nOff, tr1 + nOff, tr1 - new Vector3(0.14f, 0, 0) + nOff, br1 - new Vector3(0.16f, 0, 0) + nOff, white);
            k.PaintMask = false;

            // Wheels.
            float wheelR = style == CarBodyStyle.Bubble ? 0.4f : 0.37f, wheelW = 0.32f;
            float zf2 = zfr - 0.75f, zr2 = zr + 0.7f;
            foreach (float z in new[] { zr2, zf2 })
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * (st[2].w - wheelW * 0.35f);
                Vector3 c = new Vector3(x - side * wheelW * 0.5f, wheelR, z);
                k.Prism(c, Vector3.right * side, wheelR, wheelR, wheelW, 10, dark, 0f, chrome);
            }

            // Lights (emissive).
            k.Emission = 1f;
            float ry = Mathf.Lerp(st[0].b, st[0].t, 0.62f);
            foreach (float side in new[] { -1f, 1f })
            {
                k.Box(new Vector3(side * st[0].w * 0.6f, ry, zr - 0.01f), new Vector3(0.42f, 0.14f, 0.06f), Quaternion.identity, new Color(1f, 0.12f, 0.1f));
                Station f = st[st.Length - 1];
                k.Box(new Vector3(side * f.w * 0.58f, Mathf.Lerp(f.b, f.t, 0.55f), zfr + 0.01f), new Vector3(0.34f, 0.12f, 0.06f), Quaternion.identity, new Color(1f, 0.97f, 0.8f));
            }
            k.Emission = 0f;

            // Accent: side skirts and (Wedge) rear wing, (Muscle) bonnet stripe.
            foreach (float side in new[] { -1f, 1f })
                k.Box(new Vector3(side * (st[2].w + 0.01f), 0.34f, (zr2 + zf2) * 0.5f), new Vector3(0.06f, 0.12f, (zf2 - zr2) - wheelR * 2.2f), Quaternion.identity, accent);
            if (style == CarBodyStyle.Wedge)
            {
                float wy = st[0].t + 0.3f;
                k.Box(new Vector3(-0.55f, wy - 0.15f, zr + 0.25f), new Vector3(0.08f, 0.3f, 0.18f), Quaternion.identity, dark);
                k.Box(new Vector3(0.55f, wy - 0.15f, zr + 0.25f), new Vector3(0.08f, 0.3f, 0.18f), Quaternion.identity, dark);
                k.Box(new Vector3(0f, wy, zr + 0.2f), new Vector3(1.75f, 0.07f, 0.42f), Quaternion.Euler(-6f, 0, 0), accent);
            }
            else if (style == CarBodyStyle.Muscle)
            {
                for (int i = 2; i < st.Length - 1; i++)
                {
                    float za = i == 2 ? cab1 : st[i].z, zb = st[i + 1].z;
                    if (zb <= za) continue;
                    k.Quad(new Vector3(-0.2f, TopAt(st, za) + 0.012f, za), new Vector3(-0.2f, TopAt(st, zb) + 0.012f, zb),
                           new Vector3(0.2f, TopAt(st, zb) + 0.012f, zb), new Vector3(0.2f, TopAt(st, za) + 0.012f, za), accent);
                }
            }
            else
            {
                // Bubble: little roof light bar
                k.Box(new Vector3(0f, roofY + 0.05f, (r0 + r1) * 0.5f), new Vector3(0.7f, 0.08f, 0.2f), Quaternion.identity, accent);
            }

            Mesh m = k.ToMesh("Car_" + style);
            return m;
        }

        private static float TopAt(Station[] st, float z)
        {
            for (int i = 0; i < st.Length - 1; i++)
                if (z >= st[i].z && z <= st[i + 1].z) return Mathf.Lerp(st[i].t, st[i + 1].t, (z - st[i].z) / (st[i + 1].z - st[i].z));
            return z < st[0].z ? st[0].t : st[st.Length - 1].t;
        }
    }
}
