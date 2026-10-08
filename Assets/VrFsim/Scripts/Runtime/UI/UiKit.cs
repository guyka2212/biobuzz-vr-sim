using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace VrFsim.UI
{
    /// <summary>Builds world-space uGUI in code: flat panels and TextMeshPro text, no textures.</summary>
    public static class UiKit
    {
        public static readonly Color Bg = new Color(0.07f, 0.08f, 0.1f, 0.94f);
        public static readonly Color Panel = new Color(0.13f, 0.14f, 0.17f, 1f);
        public static readonly Color Row = new Color(0.17f, 0.18f, 0.22f, 1f);
        public static readonly Color Highlight = new Color(0.98f, 0.76f, 0.1f, 1f);
        public static readonly Color TextMain = new Color(0.94f, 0.95f, 0.97f, 1f);
        public static readonly Color TextDim = new Color(0.62f, 0.66f, 0.72f, 1f);
        public static readonly Color Red = new Color(0.9f, 0.2f, 0.2f, 1f);
        public static readonly Color Blue = new Color(0.25f, 0.5f, 1f, 1f);

        /// <summary>A world-space canvas <paramref name="widthM"/> metres wide.</summary>
        public static Canvas WorldCanvas(string name, Transform parent, Vector2 sizePx, float widthM, bool interactive)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = sizePx;
            float scale = widthM / sizePx.x;
            rt.localScale = Vector3.one * scale;
            go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
            if (interactive)
            {
                go.AddComponent<GraphicRaycaster>();
            }
            return canvas;
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Fill(RectTransform rt, float l = 0, float t = 0, float r = 0, float b = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        public static Image Box(Transform parent, string name, Color c)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            return img;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            var rt = Rect(parent, name);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            return t;
        }

        public static LayoutElement Size(Component c, float height = -1, float width = -1, float flex = -1)
        {
            if (!c.TryGetComponent<LayoutElement>(out var le)) le = c.gameObject.AddComponent<LayoutElement>();
            if (height >= 0) { le.preferredHeight = height; le.minHeight = height; }
            if (width >= 0) { le.preferredWidth = width; le.minWidth = width; }
            if (flex >= 0) le.flexibleWidth = flex;
            return le;
        }

        public static VerticalLayoutGroup VStack(GameObject go, float spacing, RectOffset pad = null)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = pad ?? new RectOffset();
            v.childControlHeight = true; v.childControlWidth = true;
            v.childForceExpandHeight = false; v.childForceExpandWidth = true;
            return v;
        }

        public static HorizontalLayoutGroup HStack(GameObject go, float spacing, RectOffset pad = null)
        {
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = pad ?? new RectOffset();
            h.childControlHeight = true; h.childControlWidth = true;
            h.childForceExpandHeight = true; h.childForceExpandWidth = false;
            h.childAlignment = TextAnchor.MiddleLeft;
            return h;
        }

        public static Button Button(Transform parent, string label, Action onClick, float height = 44f, float fontSize = 22f)
        {
            var img = Box(parent, "Button_" + label, Row);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.colors = Colors();
            var t = Text(img.transform, "Label", label, fontSize, TextMain, TextAlignmentOptions.Center);
            Fill(t.rectTransform, 8, 0, 8, 0);
            b.onClick.AddListener(() => onClick?.Invoke());
            Size(img, height);
            return b;
        }

        public static ColorBlock Colors()
        {
            var cb = ColorBlock.defaultColorBlock;
            cb.normalColor = Row;
            cb.highlightedColor = new Color(0.27f, 0.3f, 0.36f, 1f);
            cb.selectedColor = new Color(0.36f, 0.3f, 0.12f, 1f);
            cb.pressedColor = Highlight;
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.05f;
            return cb;
        }

        /// <summary>One EventSystem driven by the Input System: gamepad/keyboard navigation and XR controller pointing.</summary>
        public static EventSystem EnsureEventSystem(Transform xrTrackingOrigin)
        {
            var es = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            if (!es)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem));
                es = go.GetComponent<EventSystem>();
            }
            if (!es.TryGetComponent<InputSystemUIInputModule>(out var module)) module = es.gameObject.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
            module.xrTrackingOrigin = xrTrackingOrigin;
            return es;
        }
    }
}
