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
