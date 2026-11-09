using System;
using TMPro;
using TurboRush.Audio;
using TurboRush.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TurboRush.UI
{
    public enum Icon { Pause, Play, Restart, Home, Star, StarEmpty, Lock, Flag, Car, Settings, Music, Sound, Vibrate, Steer, ArrowLeft, ArrowRight, Check, Trophy, Back, Road, Speed, Quality, Count }

    /// <summary>Factory for the code-built, safe-area aware UI.</summary>
    public static class UIKit
    {
        public static UISkin Skin;

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        /// <summary>Screen-space canvas with adaptive scaler; returns the safe-area child to build into.</summary>
        public static RectTransform Canvas(string name, int order, out Canvas canvas)
        {
            EnsureEventSystem();
            var go = new GameObject(name, typeof(RectTransform));
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<AdaptiveCanvasScaler>();
            go.AddComponent<GraphicRaycaster>();
            var safe = new GameObject("SafeArea", typeof(RectTransform)).GetComponent<RectTransform>();
            safe.SetParent(go.transform, false);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            return safe;
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static Image Image(Transform parent, string name, Sprite sprite, Color color, bool raycast = false)
        {
            RectTransform rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null && sprite.border.sqrMagnitude > 0f) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        public static Image Panel(Transform parent, string name, Color color) => Image(parent, name, Skin != null ? Skin.rounded : null, color, true);

        public static TextMeshProUGUI Label(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center, bool outline = false)
        {
            RectTransform rt = Rect(parent, name);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (Skin != null && Skin.font != null) t.font = Skin.font;
            if (outline && Skin != null && Skin.fontOutline != null) t.fontSharedMaterial = Skin.fontOutline;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        /// <summary>Big rounded button with optional icon and label. Plays the click sound.</summary>
        public static Button Button(Transform parent, string name, string label, Icon? icon, Color color, Vector2 size, Action onClick, float fontSize = 52f)
        {
            RectTransform rt = Rect(parent, name);
            rt.sizeDelta = size;
            Image shadow = Image(rt, "Shadow", Skin != null ? Skin.rounded : null, new Color(0f, 0f, 0f, 0.28f));
            Stretch(shadow.rectTransform);
            shadow.rectTransform.offsetMin = new Vector2(0, -10);
            shadow.rectTransform.offsetMax = new Vector2(0, -10);
            Image bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = Skin != null ? Skin.rounded : null;
            bg.type = UnityEngine.UI.Image.Type.Sliced;
            bg.color = color;
            Image shine = Image(rt, "Shine", Skin != null ? Skin.gradient : null, new Color(1f, 1f, 1f, 0.22f));
            Stretch(shine.rectTransform, 6f);
            shine.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            ColorBlock cb = b.colors;
            cb.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            cb.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.8f);
            b.colors = cb;
            rt.gameObject.AddComponent<PressFeedback>();
            b.onClick.AddListener(() =>
            {
                if (Services.Instance != null) Services.Instance.Audio.Play(Sfx.Click, 1f, true);
                onClick?.Invoke();
            });

            bool hasLabel = label != null; // "" = label filled in later
            if (icon.HasValue && Skin != null)
            {
                float s = Mathf.Min(size.y * 0.62f, size.x * 0.7f);
                Image ic = Image(rt, "Icon", Skin.Get(icon.Value), Color.white);
                Place(ic.rectTransform, hasLabel ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    hasLabel ? new Vector2(size.y * 0.55f, 4f) : new Vector2(0f, 4f), new Vector2(s, s));
                ic.preserveAspect = true;
            }
            if (hasLabel)
            {
                TextMeshProUGUI t = Label(rt, "Label", label, fontSize, Color.white, TextAlignmentOptions.Center, true);
                Stretch(t.rectTransform);
                if (icon.HasValue) t.rectTransform.offsetMin = new Vector2(size.y * 0.9f, 0f);
                t.rectTransform.offsetMax = new Vector2(t.rectTransform.offsetMax.x - 10f, 4f);
            }
            return b;
        }

        public static GameObject Overlay(Transform canvasRoot, string name, float alpha)
        {
            // Full-screen (ignores safe area) dimmer that also swallows touches.
            RectTransform rt = Rect(canvasRoot.root, name);
            Stretch(rt);
            rt.SetAsLastSibling();
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.02f, 0.05f, 0.15f, alpha);
            img.raycastTarget = true;
            return rt.gameObject;
        }

        public static string FormatTime(float seconds)
        {
            if (seconds <= 0f || float.IsNaN(seconds)) return "--:--.--";
            int m = (int)(seconds / 60f);
            float s = seconds - m * 60;
            return $"{m}:{s:00.00}";
        }

        public static string Ordinal(int n)
        {
            if (n % 100 >= 11 && n % 100 <= 13) return n + "th";
            switch (n % 10) { case 1: return n + "st"; case 2: return n + "nd"; case 3: return n + "rd"; default: return n + "th"; }
        }
    }
}
