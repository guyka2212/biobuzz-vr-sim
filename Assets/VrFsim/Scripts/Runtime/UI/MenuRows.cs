using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VrFsim.UI
{
    /// <summary>A settings row that can re-read its value after validation clamps it.</summary>
    public interface IMenuRow
    {
        void Refresh();
        Selectable Focus { get; }
    }

    /// <summary>
    /// Left/right cycles through a list of values. Up/down navigates. Works with a gamepad (D-pad or
    /// stick) and with a pointer (the arrow buttons).
    /// </summary>
    public class StepperRow : Selectable, IMoveHandler, ISubmitHandler, IMenuRow
    {
        public Func<string> getText;
        public Action<int> step;
        public Action submit;
        public TextMeshProUGUI valueText;
        public Action<BaseEventData> onCancel;

        public Selectable Focus => this;

        public override void OnMove(AxisEventData e)
        {
            if (e.moveDir == MoveDirection.Left) { step?.Invoke(-1); Refresh(); e.Use(); return; }
            if (e.moveDir == MoveDirection.Right) { step?.Invoke(1); Refresh(); e.Use(); return; }
            base.OnMove(e);
        }

        public void OnSubmit(BaseEventData e)
        {
            if (submit != null) submit(); else step?.Invoke(1);
            Refresh();
        }

        public void Refresh()
        {
            if (valueText && getText != null) valueText.text = getText();
        }
    }

    /// <summary>A slider row with a live value label. Values snap to a step.</summary>
    public class SliderRow : MonoBehaviour, IMenuRow
    {
        public Slider slider;
        public TextMeshProUGUI valueText;
        public Func<float> get;
        public Action<float> set;
        public float min, step;
        public Func<float, string> format;
        bool refreshing;

        public Selectable Focus => slider;

        public void Init()
        {
            slider.onValueChanged.AddListener(v =>
            {
                if (refreshing) return;
                set(min + v * step);
                UpdateLabel();
            });
        }

        public void Refresh()
        {
            refreshing = true;
            slider.value = Mathf.Round((get() - min) / step);
            refreshing = false;
            UpdateLabel();
        }

        void UpdateLabel() => valueText.text = format != null ? format(get()) : get().ToString("0.##");
    }

    public class ToggleRow : MonoBehaviour, IMenuRow
    {
        public Toggle toggle;
        public Func<bool> get;
        bool refreshing;
        public Action<bool> set;
        public Selectable Focus => toggle;

        public void Init() => toggle.onValueChanged.AddListener(v => { if (!refreshing) set(v); });

        public void Refresh()
        {
            refreshing = true;
            toggle.isOn = get();
            refreshing = false;
        }
    }

    public class ButtonRow : MonoBehaviour, IMenuRow
    {
        public Button button;
        public TextMeshProUGUI label;
        public Func<string> getLabel;
        public Selectable Focus => button;
        public void Refresh() { if (getLabel != null) label.text = getLabel(); }
    }

    /// <summary>Scrolls the content so the selected row stays visible during gamepad navigation.</summary>
    public class KeepSelectionVisible : MonoBehaviour
    {
        public ScrollRect scroll;

        void LateUpdate()
        {
            var sel = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            if (!sel || !scroll || !sel.transform.IsChildOf(scroll.content)) return;
            var content = scroll.content;
            var viewport = scroll.viewport;
            var rt = (RectTransform)sel.transform;
            Vector3[] c = new Vector3[4];
            rt.GetWorldCorners(c);
            Vector3 top = viewport.InverseTransformPoint(c[1]), bottom = viewport.InverseTransformPoint(c[0]);
            float vpTop = viewport.rect.yMax, vpBottom = viewport.rect.yMin;
            Vector2 pos = content.anchoredPosition;
            if (top.y > vpTop) pos.y -= top.y - vpTop + 8f;
            else if (bottom.y < vpBottom) pos.y += vpBottom - bottom.y + 8f;
            content.anchoredPosition = pos;
        }
    }
}

namespace VrFsim.UI
{
    using System.Collections.Generic;
    using VrFsim.Settings;

    /// <summary>
    /// A 3x3 chassis map for picking where a mechanism is mounted (front row at the top), like
    /// dsim's builder. Gamepad: Left/Right steps through the legal cells. Pointer: click a cell.
    /// Illegal cells are dimmed; cells taken by another mechanism show its name.
    /// </summary>
    public class MountGridRow : Selectable, IMoveHandler, ISubmitHandler, IMenuRow
    {
        public Func<MountPos> get;
        public Action<MountPos> set;
        public Func<MountPos, bool> allowed;
        public Func<MountPos, string> marker;
        public readonly List<(MountPos pos, Image bg, TextMeshProUGUI text, Button button)> cells =
            new List<(MountPos, Image, TextMeshProUGUI, Button)>();

        public Selectable Focus => this;

        static readonly string[] Names = { "F-LEFT", "FRONT", "F-RIGHT", "LEFT", "CENTER", "RIGHT", "B-LEFT", "BACK", "B-RIGHT" };

        public static string Name(MountPos p) => Names[(int)p];

        public override void OnMove(AxisEventData e)
        {
            if (e.moveDir == MoveDirection.Left || e.moveDir == MoveDirection.Right)
            {
                Step(e.moveDir == MoveDirection.Right ? 1 : -1);
                e.Use();
                return;
            }
            base.OnMove(e);
        }

        public void OnSubmit(BaseEventData e) => Step(1);

        void Step(int d)
        {
            int cur = (int)get();
            for (int i = 1; i <= 9; i++)
            {
                var p = (MountPos)(((cur + d * i) % 9 + 9) % 9);
                if (allowed(p)) { set(p); break; }
            }
            Refresh();
        }

        public void Pick(MountPos p)
        {
            if (!allowed(p)) return;
            set(p);
            Refresh();
        }

        public void Refresh()
        {
            var cur = get();
            foreach (var c in cells)
            {
                bool ok = allowed(c.pos), sel = c.pos == cur;
                string mark = marker?.Invoke(c.pos);
                c.bg.color = sel ? new Color(0.36f, 0.62f, 0.5f) : ok ? UiKit.Row : new Color(0.1f, 0.1f, 0.12f);
                c.text.color = sel ? Color.white : ok ? UiKit.TextMain : UiKit.TextDim;
                c.text.text = string.IsNullOrEmpty(mark) ? Name(c.pos) : $"{Name(c.pos)}\n<size=70%>{mark}</size>";
            }
        }
    }

    /// <summary>A text line that re-reads its content (e.g. a description of the current choice).</summary>
    public class LiveText : MonoBehaviour
    {
        public Func<string> get;
        public TextMeshProUGUI text;
        public void Refresh() { if (get != null) text.text = get(); }
    }
}
