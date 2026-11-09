using System;
using UnityEngine;

namespace TurboRush.Track
{
    /// <summary>One point on the track centreline, in world space.</summary>
    public struct TrackSample
    {
        public Vector3 position;
        public Vector3 forward;
        public Vector3 right;
        public Vector3 up;
        /// <summary>Signed horizontal curvature (1/m). Positive = the road turns right.</summary>
        public float curvature;
        /// <summary>Banking angle in degrees (positive = right side lower).</summary>
        public float bank;
    }

    /// <summary>
    /// Closed (looping) centreline built from control points with a centripetal Catmull-Rom spline, then resampled
    /// at uniform arc length so that distance → position lookups are O(1) and speed is consistent around curves.
    /// All lookups are allocation-free.
    /// </summary>
    public sealed class TrackPath
    {
        public const float DefaultSpacing = 1f;

        private Vector3[] m_Pos;
        private Vector3[] m_Fwd;
        private Vector3[] m_Right;
        private Vector3[] m_Up;
        private float[] m_Curv;
        private float[] m_Bank;
        private int m_Count;

        public float Length { get; private set; }
        public float Spacing { get; private set; }
        public int SampleCount => m_Count;

        /// <param name="controlPoints">Loop control points (the loop closes from the last back to the first).</param>
        /// <param name="autoBankFactor">Degrees of bank per unit of curvature×100 (0 = flat).</param>
        /// <param name="maxBank">Bank clamp in degrees.</param>
        public static TrackPath Build(Vector3[] controlPoints, float autoBankFactor = 0f, float maxBank = 0f, float spacing = DefaultSpacing)
        {
            if (controlPoints == null || controlPoints.Length < 4) throw new ArgumentException("A looping track needs at least 4 control points.");
            var p = new TrackPath();
            p.Spacing = Mathf.Max(0.25f, spacing);
            p.Generate(controlPoints, autoBankFactor, maxBank);
            return p;
        }

        private void Generate(Vector3[] cps, float autoBankFactor, float maxBank)
        {
            int n = cps.Length;
            const int sub = 48;
            int dense = n * sub;
            var densePts = new Vector3[dense + 1];
            for (int i = 0; i < n; i++)
            {
                Vector3 p0 = cps[(i - 1 + n) % n], p1 = cps[i], p2 = cps[(i + 1) % n], p3 = cps[(i + 2) % n];
                for (int s = 0; s < sub; s++) densePts[i * sub + s] = CentripetalCatmullRom(p0, p1, p2, p3, s / (float)sub);
            }
            densePts[dense] = densePts[0];

            var cum = new float[dense + 1];
            for (int i = 1; i <= dense; i++) cum[i] = cum[i - 1] + Vector3.Distance(densePts[i - 1], densePts[i]);
            float total = cum[dense];
            m_Count = Mathf.Max(8, Mathf.RoundToInt(total / Spacing));
            Spacing = total / m_Count; // exact, so the loop closes seamlessly
            Length = total;

            m_Pos = new Vector3[m_Count];
            int seg = 0;
            for (int k = 0; k < m_Count; k++)
            {
                float d = k * Spacing;
                while (seg < dense - 1 && cum[seg + 1] < d) seg++;
                float segLen = cum[seg + 1] - cum[seg];
                float t = segLen > 1e-5f ? (d - cum[seg]) / segLen : 0f;
                m_Pos[k] = Vector3.Lerp(densePts[seg], densePts[seg + 1], t);
            }

            m_Fwd = new Vector3[m_Count];
            for (int k = 0; k < m_Count; k++)
            {
                Vector3 f = m_Pos[(k + 1) % m_Count] - m_Pos[(k - 1 + m_Count) % m_Count];
                m_Fwd[k] = f.sqrMagnitude > 1e-8f ? f.normalized : Vector3.forward;
            }

            // Signed horizontal curvature, smoothed over ~±12 m so banking and drift change gently.
            var rawCurv = new float[m_Count];
            for (int k = 0; k < m_Count; k++)
            {
                Vector3 a = m_Fwd[(k - 1 + m_Count) % m_Count], b = m_Fwd[(k + 1) % m_Count];
                a.y = 0; b.y = 0;
                float ang = Vector3.SignedAngle(a, b, Vector3.up) * Mathf.Deg2Rad;
                rawCurv[k] = ang / (2f * Spacing);
            }
            m_Curv = new float[m_Count];
            int win = Mathf.Max(1, Mathf.RoundToInt(12f / Spacing));
            for (int k = 0; k < m_Count; k++)
            {
                float sum = 0f;
                for (int j = -win; j <= win; j++) sum += rawCurv[(k + j + m_Count) % m_Count];
                m_Curv[k] = sum / (2 * win + 1);
            }

            m_Bank = new float[m_Count];
            m_Up = new Vector3[m_Count];
            m_Right = new Vector3[m_Count];
            for (int k = 0; k < m_Count; k++)
            {
                float bank = Mathf.Clamp(m_Curv[k] * 100f * autoBankFactor, -maxBank, maxBank);
                m_Bank[k] = bank;
                Vector3 f = m_Fwd[k];
                Vector3 flatRight = Vector3.Cross(Vector3.up, f);
                if (flatRight.sqrMagnitude < 1e-6f) flatRight = Vector3.right;
                flatRight.Normalize();
                Vector3 flatUp = Vector3.Cross(f, flatRight).normalized;
                float r = bank * Mathf.Deg2Rad;
                Vector3 up = (flatUp * Mathf.Cos(r) + flatRight * Mathf.Sin(r)).normalized; // normal leans to the inside: right side lower for positive bank
                m_Up[k] = up;
                m_Right[k] = Vector3.Cross(up, f).normalized;
            }
        }

        private static Vector3 CentripetalCatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t0 = 0f;
            float t1 = t0 + Mathf.Pow(Mathf.Max(Vector3.Distance(p0, p1), 1e-4f), 0.5f);
            float t2 = t1 + Mathf.Pow(Mathf.Max(Vector3.Distance(p1, p2), 1e-4f), 0.5f);
            float t3 = t2 + Mathf.Pow(Mathf.Max(Vector3.Distance(p2, p3), 1e-4f), 0.5f);
            float tt = Mathf.Lerp(t1, t2, t);
            Vector3 a1 = (t1 - tt) / (t1 - t0) * p0 + (tt - t0) / (t1 - t0) * p1;
            Vector3 a2 = (t2 - tt) / (t2 - t1) * p1 + (tt - t1) / (t2 - t1) * p2;
            Vector3 a3 = (t3 - tt) / (t3 - t2) * p2 + (tt - t2) / (t3 - t2) * p3;
            Vector3 b1 = (t2 - tt) / (t2 - t0) * a1 + (tt - t0) / (t2 - t0) * a2;
            Vector3 b2 = (t3 - tt) / (t3 - t1) * a2 + (tt - t1) / (t3 - t1) * a3;
            return (t2 - tt) / (t2 - t1) * b1 + (tt - t1) / (t2 - t1) * b2;
        }

        /// <summary>Wraps any distance (including negative or multi-lap) into [0, Length).</summary>
        public float Wrap(float distance)
        {
            float d = distance % Length;
            return d < 0f ? d + Length : d;
        }

        /// <summary>Signed shortest along-track difference a−b on the loop, in [−Length/2, Length/2).</summary>
        public float DeltaOnLoop(float a, float b)
        {
            float d = Wrap(a - b);
            return d >= Length * 0.5f ? d - Length : d;
        }

        public TrackSample Sample(float distance)
        {
            Sample(distance, out TrackSample s);
            return s;
        }

        public void Sample(float distance, out TrackSample s)
        {
            float d = Wrap(distance) / Spacing;
            int i0 = (int)d;
            if (i0 >= m_Count) i0 = m_Count - 1;
            int i1 = i0 + 1 == m_Count ? 0 : i0 + 1;
            float t = d - i0;
            s.position = Vector3.LerpUnclamped(m_Pos[i0], m_Pos[i1], t);
            s.forward = Vector3.LerpUnclamped(m_Fwd[i0], m_Fwd[i1], t).normalized;
            s.up = Vector3.LerpUnclamped(m_Up[i0], m_Up[i1], t).normalized;
            s.right = Vector3.LerpUnclamped(m_Right[i0], m_Right[i1], t).normalized;
            s.curvature = Mathf.LerpUnclamped(m_Curv[i0], m_Curv[i1], t);
            s.bank = Mathf.LerpUnclamped(m_Bank[i0], m_Bank[i1], t);
        }

        /// <summary>World position at a distance and lateral offset (metres to the right of the centreline).</summary>
        public Vector3 PositionAt(float distance, float lateral)
        {
            Sample(distance, out TrackSample s);
            return s.position + s.right * lateral;
        }

        /// <summary>Closest along-track distance to a world point (coarse scan + local refine). Not for per-frame use.</summary>
        public float ClosestDistance(Vector3 world)
        {
            int best = 0;
            float bestSq = float.MaxValue;
            for (int k = 0; k < m_Count; k += 4)
            {
                float sq = (m_Pos[k] - world).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = k; }
            }
            for (int k = best - 4; k <= best + 4; k++)
            {
                int i = (k + m_Count) % m_Count;
                float sq = (m_Pos[i] - world).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = i; }
            }
            return best * Spacing;
        }

        public Vector3 PointAtIndex(int i) => m_Pos[((i % m_Count) + m_Count) % m_Count];
    }
}
