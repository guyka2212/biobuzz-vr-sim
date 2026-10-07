using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace VrFsim.UI
{
    /// <summary>
    /// A keyboard drawn in the menu, so text (the robot name) can be typed with a gamepad or a VR
    /// pointer. D-pad / stick moves between keys, A types, B finishes. A physical keyboard also
    /// types into it while it is open (Backspace deletes, Enter finishes, Esc cancels).
    /// </summary>
    public class OnScreenKeyboard : MonoBehaviour
    {
        static readonly string[] Rows = { "1234567890", "QWERTYUIOP", "ASDFGHJKL-", "ZXCVBNM_.#" };

        CanvasGroup behind;
        RectTransform panel;
        TextMeshProUGUI preview, title;
        Button firstKey;
        string text, original;
        int maxLength;
        Action<string> done;
        GameObject returnTo;
        readonly List<Button> keys = new List<Button>();

        public bool IsOpen => panel && panel.gameObject.activeSelf;

        public void Build(Transform canvas, CanvasGroup content)
        {
            behind = content;
            panel = UiKit.Box(canvas, "Keyboard", new Color(0.05f, 0.06f, 0.08f, 0.98f)).rectTransform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(820, 470);
            UiKit.VStack(panel.gameObject, 8, new RectOffset(20, 20, 16, 16));
            title = UiKit.Text(panel, "Title", "", 24, UiKit.Highlight); UiKit.Size(title, 32);
            var box = UiKit.Box(panel, "Preview", new Color(0.12f, 0.13f, 0.16f)); UiKit.Size(box, 52);
            preview = UiKit.Text(box.transform, "Text", "", 30, UiKit.TextMain, TextAlignmentOptions.Left);
            UiKit.Fill(preview.rectTransform, 14, 4, 14, 4);

            foreach (var row in Rows)
            {
                var r = UiKit.Rect(panel, "Row"); UiKit.HStack(r.gameObject, 6).childForceExpandWidth = true; UiKit.Size(r, 56);
                foreach (char ch in row)
                {
                    char c = ch;
                    var b = UiKit.Button(r, c.ToString(), () => Type(c.ToString()), 56, 26);
                    keys.Add(b);
                    if (!firstKey) firstKey = b;
                }
            }
            var bottom = UiKit.Rect(panel, "Bottom"); UiKit.HStack(bottom.gameObject, 6).childForceExpandWidth = true; UiKit.Size(bottom, 56);
            keys.Add(UiKit.Button(bottom, "Space", () => Type(" "), 56, 24));
            keys.Add(UiKit.Button(bottom, "Delete", Backspace, 56, 24));
            keys.Add(UiKit.Button(bottom, "Clear", () => { text = ""; Refresh(); }, 56, 24));
            keys.Add(UiKit.Button(bottom, "Done", () => Close(true), 56, 24));
            foreach (var k in keys) k.gameObject.AddComponent<KeyCancel>().keyboard = this;
            panel.gameObject.SetActive(false);
        }

        public void Open(string label, string current, int limit, Action<string> onDone)
        {
            title.text = label + "   (A: type   B: done)";
            text = original = current ?? "";
            maxLength = limit;
            done = onDone;
            returnTo = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            behind.interactable = false;
            panel.gameObject.SetActive(true);
            panel.SetAsLastSibling();
            Refresh();
            if (Keyboard.current != null) Keyboard.current.onTextInput += OnPhysicalText;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(firstKey.gameObject);
        }

        public void Close(bool accept)
        {
            if (!IsOpen) return;
            if (Keyboard.current != null) Keyboard.current.onTextInput -= OnPhysicalText;
            panel.gameObject.SetActive(false);
            behind.interactable = true;
            if (!accept) text = original;
            done?.Invoke(text);
            if (EventSystem.current && returnTo) EventSystem.current.SetSelectedGameObject(returnTo);
        }

        void Update()
        {
            if (!IsOpen || Keyboard.current == null) return;
            var kb = Keyboard.current;
            if (kb.backspaceKey.wasPressedThisFrame) Backspace();
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Close(true);
            if (kb.escapeKey.wasPressedThisFrame) Close(false);
        }

        void OnPhysicalText(char c)
        {
            if (char.IsControl(c)) return;
            Type(c.ToString());
        }

        void Type(string s)
        {
            if (text.Length + s.Length > maxLength) return;
            text += s;
            Refresh();
        }

        void Backspace()
        {
            if (text.Length > 0) text = text.Substring(0, text.Length - 1);
            Refresh();
        }

        void Refresh() => preview.text = text + "<color=#F2B705>|</color>";

        /// <summary>B on any key finishes typing.</summary>
        class KeyCancel : MonoBehaviour, ICancelHandler
        {
            public OnScreenKeyboard keyboard;
            public void OnCancel(BaseEventData e) => keyboard.Close(true);
        }
    }
}
