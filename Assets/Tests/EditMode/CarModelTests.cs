using System.Collections.Generic;
using NUnit.Framework;
using TurboRush.Core;
using TurboRush.Vehicle;
using UnityEditor;
using UnityEngine;

namespace TurboRush.Tests
{
    /// <summary>The imported Blender cars: orientation, ground contact, size, budget, paint/emission channels, wheel motion.</summary>
    public class CarModelTests
    {
        private static IEnumerable<CarDefinition> Cars()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Resources/GameConfig.asset");
            Assert.NotNull(cfg, "run setup first");
            Assert.AreEqual(8, cfg.cars.Length);
            return cfg.cars;
        }

        private static Mesh MeshOf(GameObject prefab, string name)
        {
            foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>(true))
                if (mf.name == name) return mf.sharedMesh;
            Assert.Fail(prefab.name + " has no " + name);
            return null;
        }

        [Test]
        public void EveryCarFacesForwardWithLightsAtBothEnds()
        {
            foreach (CarDefinition c in Cars())
            {
                Mesh body = MeshOf(c.prefab, "Body");
                Vector3[] v = body.vertices;
                var uv2 = new List<Vector2>();
                body.GetUVs(1, uv2);
                Color32[] col = body.colors32;
                float head = 0f, tail = 0f;
                int nHead = 0, nTail = 0, painted = 0;
                for (int i = 0; i < v.Length; i++)
                {
                    if (col[i].a > 0) painted++;
                    if (uv2[i].x <= 0f) continue;
                    Color32 k = col[i];
                    if (k.r > 200 && k.g > 200) { head += v[i].z; nHead++; }        // warm white headlights
                    else if (k.r > 200 && k.g < 60) { tail += v[i].z; nTail++; }    // red tail lights
                }
                Assert.Greater(nHead, 0, c.id + ": has headlights");
                Assert.Greater(nTail, 0, c.id + ": has tail lights");
                Assert.Greater(head / nHead, 1f, c.id + ": headlights at the front (+Z)");
                Assert.Less(tail / nTail, -1f, c.id + ": tail lights at the rear (-Z)");
                Assert.Greater(painted, v.Length / 10, c.id + ": a good share of the body takes the paint colour");
            }
        }

        [Test]
        public void EveryCarSitsOnTheGroundAtTheRightSize()
        {
            foreach (CarDefinition c in Cars())
            {
                var view = c.prefab.GetComponent<RacerView>();
                Assert.AreEqual(4, view.Wheels.Length);
                Bounds b = MeshOf(c.prefab, "Body").bounds;
                foreach (Transform w in view.Wheels)
                {
                    Bounds wb = w.GetComponent<MeshFilter>().sharedMesh.bounds;
                    Assert.AreEqual(0f, w.localPosition.y + wb.min.y, 0.01f, $"{c.id} {w.name} touches the ground");
                    Assert.AreEqual(view.WheelRadius, w.localPosition.y, 0.02f, $"{c.id} {w.name} radius");
                    Assert.Less(new Vector2(wb.center.y, wb.center.z).magnitude, 0.01f, $"{c.id} {w.name} pivot on the axle");
                    b.Encapsulate(new Bounds(w.localPosition + wb.center, wb.size));
                }
                Assert.That(b.size.z, Is.InRange(3.9f, 4.5f), c.id + " length");
                Assert.That(b.size.x, Is.InRange(1.85f, 2.05f), c.id + " width");
                Assert.That(b.size.y, Is.InRange(1.1f, 1.7f), c.id + " height");
                Assert.Less(view.Wheels[0].localPosition.x, 0f, c.id + ": FL is on the left");
                Assert.Greater(view.Wheels[0].localPosition.z, view.Wheels[2].localPosition.z, c.id + ": front wheels ahead of the rear");
                Assert.AreEqual(0f, b.center.x, 0.03f, c.id + " centred");
            }
        }

        [Test]
        public void EveryCarIsWithinTheMobileBudgetAndHasALod()
        {
            foreach (CarDefinition c in Cars())
            {
                int full = MeshOf(c.prefab, "Body").triangles.Length / 3;
                foreach (Transform w in c.prefab.GetComponent<RacerView>().Wheels) full += w.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3;
                Assert.LessOrEqual(full, 3700, c.id + " triangles");
                Assert.LessOrEqual(MeshOf(c.prefab, "Body_LOD1").triangles.Length / 3, 900, c.id + " LOD1 triangles");
                LODGroup g = c.prefab.GetComponent<LODGroup>();
                Assert.NotNull(g);
                LOD[] lods = g.GetLODs();
                Assert.AreEqual(5, lods[0].renderers.Length, c.id + ": LOD0 = body + 4 wheels");
                Assert.AreEqual("Body_LOD1", lods[1].renderers[0].name);
                foreach (Renderer r in c.prefab.GetComponentsInChildren<Renderer>())
                    if (r.name != "Shadow") Assert.AreSame(r.sharedMaterial, c.prefab.GetComponentInChildren<MeshRenderer>().sharedMaterial, c.id + ": one material per car");
            }
        }

        [Test]
        public void WheelsRollWithDistanceAndFrontWheelsSteer()
        {
            foreach (CarDefinition c in Cars())
            {
                GameObject go = Object.Instantiate(c.prefab);
                try
                {
                    var view = go.GetComponent<RacerView>();
                    float r = view.WheelRadius;
                    // A quarter turn: travel a quarter of the circumference.
                    view.SpinWheels(Mathf.PI * r * 0.5f, 0f, 1f);
                    Vector3 up = view.Wheels[2].localRotation * Vector3.up;
                    Assert.AreEqual(1f, up.z, 1e-3f, c.id + ": the top of the wheel rolls forward");
                    view.SpinWheels(0f, 1f, 0f);
                    // The axle (spin axis) of a wheel turned right points right-and-back.
                    Vector3 axle = view.Wheels[0].localRotation * Vector3.right;
                    Assert.Less(axle.z, -0.3f, c.id + ": front wheels turn right when steering right");
                    Assert.AreEqual(Vector3.right, view.Wheels[2].localRotation * Vector3.right, c.id + ": rear wheels do not steer");
                }
                finally { Object.DestroyImmediate(go); }
            }
        }
    }
}
