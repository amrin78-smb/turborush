using System;
using UnityEngine;

namespace TurboRush.EditorTools
{
    /// <summary>
    /// Signed-distance icon painter: shapes are distance functions (negative inside) combined with min/max and
    /// rasterised with 1-pixel anti-aliasing. All UI icons are original and generated here.
    /// </summary>
    public static class IconPainter
    {
        public delegate float Sdf(Vector2 p); // p in 0..1 icon space (y up)

        public static Texture2D Render(int size, Sdf shape, Color color)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            float aa = 1.2f / size;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                float d = shape(p);
                float a = Mathf.Clamp01(0.5f - d / aa);
                px[y * size + x] = new Color(color.r, color.g, color.b, color.a * a);
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        // ---- primitives ----
        public static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;
        public static float Ring(Vector2 p, Vector2 c, float r, float t) => Mathf.Abs((p - c).magnitude - r) - t * 0.5f;

        public static float Box(Vector2 p, Vector2 c, Vector2 half, float radius = 0f)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + new Vector2(radius, radius);
            return new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        }

        public static float Segment(Vector2 p, Vector2 a, Vector2 b, float thickness)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude - thickness * 0.5f;
        }

        public static float Polygon(Vector2 p, params Vector2[] v)
        {
            float d = Vector2.Dot(p - v[0], p - v[0]);
            float s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                Vector2 e = v[j] - v[i], w = p - v[i];
                Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }

        /// <summary>Arc ring between angles (degrees, 0 = +x, counter-clockwise).</summary>
        public static float Arc(Vector2 p, Vector2 c, float r, float t, float a0, float a1)
        {
            Vector2 d = p - c;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            if (ang < 0) ang += 360f;
            float lo = a0, hi = a1;
            bool inside = lo <= hi ? ang >= lo && ang <= hi : ang >= lo || ang <= hi;
            if (inside) return Mathf.Abs(d.magnitude - r) - t * 0.5f;
            Vector2 e0 = c + new Vector2(Mathf.Cos(a0 * Mathf.Deg2Rad), Mathf.Sin(a0 * Mathf.Deg2Rad)) * r;
            Vector2 e1 = c + new Vector2(Mathf.Cos(a1 * Mathf.Deg2Rad), Mathf.Sin(a1 * Mathf.Deg2Rad)) * r;
            return Mathf.Min((p - e0).magnitude, (p - e1).magnitude) - t * 0.5f;
        }

        public static float Star(Vector2 p, Vector2 c, float r, float inner)
        {
            var v = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = (90f + i * 36f) * Mathf.Deg2Rad;
                float rr = i % 2 == 0 ? r : r * inner;
                v[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
            }
            return Polygon(p, v);
        }

        public static float Union(params float[] d) { float m = float.MaxValue; foreach (float x in d) m = Mathf.Min(m, x); return m; }
        public static float Sub(float a, float b) => Mathf.Max(a, -b);

        private static Vector2 V(float x, float y) => new Vector2(x, y);

        public static Sdf For(UI.Icon icon)
        {
            switch (icon)
            {
                case UI.Icon.Pause: return p => Union(Box(p, V(0.36f, 0.5f), V(0.09f, 0.3f), 0.05f), Box(p, V(0.64f, 0.5f), V(0.09f, 0.3f), 0.05f));
                case UI.Icon.Play: return p => Polygon(p, V(0.3f, 0.2f), V(0.3f, 0.8f), V(0.8f, 0.5f)) - 0.03f;
                case UI.Icon.Restart: return p => Union(Arc(p, V(0.5f, 0.5f), 0.28f, 0.11f, 50f, 320f), Polygon(p, V(0.79f, 0.62f), V(0.76f, 0.81f), V(0.6f, 0.62f)) - 0.02f);
                case UI.Icon.Home: return p => Union(Polygon(p, V(0.5f, 0.86f), V(0.14f, 0.52f), V(0.86f, 0.52f)) - 0.02f, Sub(Box(p, V(0.5f, 0.36f), V(0.25f, 0.2f), 0.03f), Box(p, V(0.5f, 0.26f), V(0.07f, 0.11f))));
                case UI.Icon.Star: return p => Star(p, V(0.5f, 0.47f), 0.42f, 0.48f) - 0.02f;
                case UI.Icon.StarEmpty: return p => Mathf.Abs(Star(p, V(0.5f, 0.47f), 0.39f, 0.48f)) - 0.045f;
                case UI.Icon.Lock: return p => Union(Box(p, V(0.5f, 0.36f), V(0.27f, 0.21f), 0.05f), Sub(Arc(p, V(0.5f, 0.57f), 0.17f, 0.08f, 0f, 180f), Box(p, V(0.5f, 0.3f), V(0.5f, 0.27f))), Box(p, V(0.33f, 0.6f), V(0.04f, 0.06f)), Box(p, V(0.67f, 0.6f), V(0.04f, 0.06f)));
                case UI.Icon.Flag: return p =>
                {
                    float pole = Box(p, V(0.22f, 0.5f), V(0.035f, 0.38f), 0.02f);
                    float box = Box(p, V(0.53f, 0.66f), V(0.27f, 0.2f), 0.02f);
                    int cx = Mathf.FloorToInt((p.x - 0.26f) / 0.135f), cy = Mathf.FloorToInt((p.y - 0.46f) / 0.1334f);
                    bool hole = ((cx + cy) & 1) == 0;
                    float fill = hole ? Mathf.Max(box, 0.01f) : box;
                    return Union(pole, fill, Mathf.Abs(box) - 0.015f);
                };
                case UI.Icon.Car: return p =>
                {
                    float body = Union(Box(p, V(0.5f, 0.42f), V(0.4f, 0.12f), 0.07f), Polygon(p, V(0.3f, 0.52f), V(0.38f, 0.7f), V(0.64f, 0.7f), V(0.74f, 0.52f)) - 0.03f);
                    body = Sub(body, Union(Circle(p, V(0.28f, 0.3f), 0.12f), Circle(p, V(0.72f, 0.3f), 0.12f)));
                    return Union(body, Circle(p, V(0.28f, 0.3f), 0.085f), Circle(p, V(0.72f, 0.3f), 0.085f));
                };
                case UI.Icon.Settings: return p =>
                {
                    Vector2 d = p - V(0.5f, 0.5f);
                    float a = Mathf.Atan2(d.y, d.x);
                    float teeth = 0.31f + 0.07f * Mathf.Clamp(Mathf.Cos(a * 8f) * 3f, -1f, 1f);
                    return Sub(d.magnitude - teeth, Circle(p, V(0.5f, 0.5f), 0.12f));
                };
                case UI.Icon.Music: return p => Union(Circle(p, V(0.35f, 0.28f), 0.12f), Box(p, V(0.45f, 0.53f), V(0.035f, 0.27f)), Polygon(p, V(0.45f, 0.8f), V(0.72f, 0.7f), V(0.72f, 0.58f), V(0.45f, 0.66f)));
                case UI.Icon.Sound: return p => Union(Box(p, V(0.24f, 0.5f), V(0.08f, 0.11f), 0.02f), Polygon(p, V(0.26f, 0.5f), V(0.5f, 0.78f), V(0.5f, 0.22f)) - 0.01f,
                    Arc(p, V(0.5f, 0.5f), 0.16f, 0.06f, 300f, 60f), Arc(p, V(0.5f, 0.5f), 0.3f, 0.06f, 305f, 55f));
                case UI.Icon.Vibrate: return p => Union(Sub(Box(p, V(0.5f, 0.5f), V(0.15f, 0.3f), 0.05f), Box(p, V(0.5f, 0.52f), V(0.1f, 0.21f), 0.02f)),
                    Segment(p, V(0.2f, 0.62f), V(0.2f, 0.38f), 0.05f), Segment(p, V(0.8f, 0.62f), V(0.8f, 0.38f), 0.05f), Segment(p, V(0.1f, 0.56f), V(0.1f, 0.44f), 0.05f), Segment(p, V(0.9f, 0.56f), V(0.9f, 0.44f), 0.05f));
                case UI.Icon.Steer: return p => Union(Ring(p, V(0.5f, 0.5f), 0.33f, 0.09f), Circle(p, V(0.5f, 0.5f), 0.09f), Segment(p, V(0.2f, 0.52f), V(0.8f, 0.52f), 0.08f), Segment(p, V(0.5f, 0.5f), V(0.5f, 0.18f), 0.08f));
                case UI.Icon.ArrowLeft: return p => Polygon(p, V(0.2f, 0.5f), V(0.62f, 0.85f), V(0.62f, 0.64f), V(0.84f, 0.64f), V(0.84f, 0.36f), V(0.62f, 0.36f), V(0.62f, 0.15f)) - 0.03f;
                case UI.Icon.ArrowRight: return p => Polygon(p, V(0.8f, 0.5f), V(0.38f, 0.15f), V(0.38f, 0.36f), V(0.16f, 0.36f), V(0.16f, 0.64f), V(0.38f, 0.64f), V(0.38f, 0.85f)) - 0.03f;
                case UI.Icon.Check: return p => Union(Segment(p, V(0.2f, 0.5f), V(0.42f, 0.27f), 0.13f), Segment(p, V(0.42f, 0.27f), V(0.82f, 0.72f), 0.13f));
                case UI.Icon.Trophy: return p => Union(Sub(Box(p, V(0.5f, 0.62f), V(0.22f, 0.2f), 0.12f), Box(p, V(0.5f, 0.85f), V(0.3f, 0.04f))),
                    Ring(p, V(0.26f, 0.64f), 0.1f, 0.05f), Ring(p, V(0.74f, 0.64f), 0.1f, 0.05f), Box(p, V(0.5f, 0.33f), V(0.05f, 0.1f)), Box(p, V(0.5f, 0.17f), V(0.2f, 0.06f), 0.02f));
                case UI.Icon.Back: return p => Union(Polygon(p, V(0.16f, 0.5f), V(0.48f, 0.82f), V(0.48f, 0.18f)) - 0.02f, Box(p, V(0.6f, 0.5f), V(0.24f, 0.09f), 0.04f));
                case UI.Icon.Road: return p => Sub(Polygon(p, V(0.38f, 0.88f), V(0.62f, 0.88f), V(0.9f, 0.12f), V(0.1f, 0.12f)) - 0.01f,
                    Union(Box(p, V(0.5f, 0.24f), V(0.035f, 0.08f)), Box(p, V(0.5f, 0.49f), V(0.028f, 0.07f)), Box(p, V(0.5f, 0.72f), V(0.022f, 0.055f))));
                case UI.Icon.Speed: return p => Union(Arc(p, V(0.5f, 0.4f), 0.33f, 0.09f, 0f, 180f), Segment(p, V(0.5f, 0.4f), V(0.68f, 0.62f), 0.08f), Circle(p, V(0.5f, 0.4f), 0.08f));
                case UI.Icon.Bolt: return p => Polygon(p, V(0.58f, 0.95f), V(0.24f, 0.47f), V(0.47f, 0.47f), V(0.38f, 0.05f), V(0.78f, 0.57f), V(0.55f, 0.57f), V(0.72f, 0.95f)) - 0.02f;
                case UI.Icon.Shield: return p =>
                {
                    float Crest(float k) => Polygon(p, V(0.5f, 0.5f + 0.43f * k), V(0.5f + 0.33f * k, 0.5f + 0.31f * k), V(0.5f + 0.29f * k, 0.5f - 0.06f * k),
                        V(0.5f, 0.5f - 0.43f * k), V(0.5f - 0.29f * k, 0.5f - 0.06f * k), V(0.5f - 0.33f * k, 0.5f + 0.31f * k));
                    return Union(Sub(Crest(1f), Crest(0.8f)), Crest(0.62f));
                };
                case UI.Icon.Quality: return p => Union(Sub(Box(p, V(0.5f, 0.5f), V(0.36f, 0.3f), 0.06f), Box(p, V(0.5f, 0.5f), V(0.29f, 0.23f), 0.02f)),
                    Polygon(p, V(0.24f, 0.29f), V(0.42f, 0.56f), V(0.56f, 0.38f), V(0.64f, 0.48f), V(0.76f, 0.29f)), Circle(p, V(0.66f, 0.63f), 0.06f));
                default: return p => Circle(p, V(0.5f, 0.5f), 0.4f);
            }
        }
    }
}
