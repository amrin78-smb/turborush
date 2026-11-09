using TurboRush.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace TurboRush.Track
{
    /// <summary>Sky gradient, linear fog, flat ambient and the (shadowless) sun from the track palette.</summary>
    public static class RaceEnvironment
    {
        private static readonly int Top = Shader.PropertyToID("_Top");
        private static readonly int Horizon = Shader.PropertyToID("_Horizon");
        private static readonly int Bottom = Shader.PropertyToID("_Bottom");
        private static readonly int SunDir = Shader.PropertyToID("_SunDir");
        private static readonly int SunColor = Shader.PropertyToID("_SunColor");

        private static Material s_Sky;

        public static void Apply(TrackDefinition def, RaceMaterials mats, Light sun) => Apply(def.palette, mats, sun);

        public static void Apply(TrackPalette p, RaceMaterials mats, Light sun)
        {
            if (mats != null && mats.sky != null)
            {
                if (s_Sky == null) s_Sky = new Material(mats.sky) { name = "Sky (runtime)" };
                s_Sky.SetColor(Top, p.skyTop);
                s_Sky.SetColor(Horizon, p.skyHorizon);
                s_Sky.SetColor(Bottom, p.skyBottom);
                Vector3 dir = Quaternion.Euler(p.sunEuler) * Vector3.back;
                s_Sky.SetVector(SunDir, dir.normalized);
                s_Sky.SetColor(SunColor, p.sun);
                RenderSettings.skybox = s_Sky;
            }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = p.fog;
            RenderSettings.fogStartDistance = p.fogStart;
            RenderSettings.fogEndDistance = p.fogEnd;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = p.ambient;
            if (sun != null)
            {
                sun.type = LightType.Directional;
                sun.color = p.sun;
                sun.intensity = 1.05f;
                sun.shadows = LightShadows.None;
                sun.transform.rotation = Quaternion.Euler(p.sunEuler);
                RenderSettings.sun = sun;
            }
        }
    }
}
