using TMPro;
using UnityEngine;

namespace TurboRush.UI
{
    /// <summary>Sprites, font and colours for the generated UI (asset built by the editor setup).</summary>
    [CreateAssetMenu(menuName = "Turbo Rush/UI Skin", fileName = "UISkin")]
    public sealed class UISkin : ScriptableObject
    {
        public TMP_FontAsset font;
        public Material fontOutline;
        public Sprite rounded;
        public Sprite circle;
        public Sprite softCircle;
        public Sprite gradient;
        public Sprite logo;
        public Sprite[] icons = new Sprite[(int)Icon.Count];

        public Color primary = new Color(1f, 0.42f, 0.1f);
        public Color secondary = new Color(0.12f, 0.55f, 0.98f);
        public Color accent = new Color(1f, 0.8f, 0.1f);
        public Color good = new Color(0.25f, 0.78f, 0.35f);
        public Color panel = new Color(0.07f, 0.12f, 0.28f, 0.82f);
        public Color panelLight = new Color(1f, 1f, 1f, 0.95f);
        public Color text = Color.white;
        public Color textDark = new Color(0.1f, 0.13f, 0.25f);
        public Color locked = new Color(0.45f, 0.5f, 0.6f);

        public Sprite Get(Icon i) => icons != null && (int)i < icons.Length ? icons[(int)i] : null;
    }
}
