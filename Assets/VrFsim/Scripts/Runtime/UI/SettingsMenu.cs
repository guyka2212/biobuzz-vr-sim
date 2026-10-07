using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VrFsim.Game;
using VrFsim.Input;
using VrFsim.Match;
using VrFsim.Settings;
using VrFsim.VR;

namespace VrFsim.UI
{
    /// <summary>
    /// The in-VR settings menu. Opens in front of the player with the Menu button (Start / ☰ /
    /// Esc), pauses the simulation, and is fully usable with a gamepad (D-pad or stick to move,
    /// left/right to change a value, A to select, B to go back) or by pointing a VR controller.
    /// Every change is saved to disk automatically.
    /// </summary>
    public class SettingsMenu : MonoBehaviour
    {
        Canvas canvas;
        CanvasGroup menuGroup;
        OnScreenKeyboard keyboard;
        RectTransform tabColumn, content;
        ScrollRect scroll;
        TextMeshProUGUI footer, title;
        readonly List<IMenuRow> rows = new List<IMenuRow>();
        readonly List<Button> tabButtons = new List<Button>();
        int currentTab, savedSlot, savedStart, presetIndex;
        bool open, dirty, needsRestart;
        float dirtyAt;

        static SimSettings S => SettingsStore.Current;
        static RobotConfig R => SettingsStore.Current.robot;

        (string name, Action build)[] tabs;

        static (string name, string hex)[] Palette => RobotConfig.Palette;

        void Start()
        {
            tabs = new (string, Action)[]
            {
                ("Match", BuildMatchTab), ("Robot", BuildRobotTab), ("Mechanisms", BuildMechanismTab),
                ("Driving", BuildDrivingTab), ("Controls", BuildControlsTab), ("Rules", BuildRulesTab),
                ("Audio", BuildAudioTab), ("Graphics", BuildGraphicsTab), ("Camera", BuildCameraTab),
            };
            Build();
            SetOpen(false);
            if (InputHub.Instance) InputHub.Instance.MenuPressed += Toggle;
        }

        void OnDestroy()
        {
            if (InputHub.Instance) InputHub.Instance.MenuPressed -= Toggle;
        }

        void Update()
        {
            if (dirty && Time.unscaledTime - dirtyAt > 0.35f) CommitNow();
        }

        // ── Open / close ────────────────────────────────────────────────────────────────────

        public bool IsOpen => open;

        void Toggle()
        {
            if (InputHub.Instance && InputHub.Instance.IsRebinding) return;
            if (keyboard && keyboard.IsOpen) { keyboard.Close(true); return; }
            SetOpen(!open);
        }

        public void SetOpen(bool v)
        {
            open = v;
            canvas.gameObject.SetActive(v);
            if (InputHub.Instance) InputHub.Instance.DrivingSuppressed = v;
            ControllerPointer.Visible = v;
            Time.timeScale = v ? 0f : 1f;
            if (v)
            {
                PlaceInFrontOfHead();
                ShowTab(currentTab);
                Select(tabButtons[currentTab]);
            }
            else
            {
                CommitNow();
                if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
                if (needsRestart && MatchController.Instance) { needsRestart = false; MatchController.Instance.ResetMatch(); }
            }
        }

        void PlaceInFrontOfHead()
        {
            var vm = ViewManager.Instance;
            if (!vm) return;
            var head = vm.Head;
            Vector3 fwd = head.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-3f) fwd = vm.Origin.transform.forward;
            fwd.Normalize();
            canvas.transform.position = head.position + fwd * 1.15f + Vector3.down * 0.12f;
            canvas.transform.rotation = Quaternion.LookRotation(fwd);
        }

        static void Select(Selectable s)
        {
            if (s && EventSystem.current) EventSystem.current.SetSelectedGameObject(s.gameObject);
        }

        void Changed(bool restart = false)
        {
            dirty = true;
            dirtyAt = Time.unscaledTime;
            if (restart) needsRestart = true;
            UpdateFooter();
        }

        void CommitNow()
        {
            if (!dirty) return;
            dirty = false;
            SettingsStore.Commit();
            foreach (var r in rows) r.Refresh();
            foreach (var l in liveTexts) if (l) l.Refresh();
            UpdateFooter();
        }

        void UpdateFooter()
        {
            footer.text = needsRestart
                ? "<color=#F2B705>Robot or match changes apply when you close the menu (the match restarts).</color>"
                : "Saved automatically.  D-pad: move   Left/Right: change   A: select   B: back   Start: close";
        }

        // ── Layout ──────────────────────────────────────────────────────────────────────────

        void Build()
        {
            canvas = UiKit.WorldCanvas("SettingsMenu", transform, new Vector2(1200, 820), 1.1f, true);
            canvas.sortingOrder = 10;
            menuGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            var bg = UiKit.Box(canvas.transform, "Bg", UiKit.Bg);
            UiKit.Fill(bg.rectTransform);

            title = UiKit.Text(canvas.transform, "Title", "VrFsim  -  BIOBUZZ settings", 34, UiKit.TextMain);
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1); trt.pivot = new Vector2(0.5f, 1);
            trt.sizeDelta = new Vector2(-40, 54); trt.anchoredPosition = new Vector2(0, -14);

            tabColumn = UiKit.Rect(canvas.transform, "Tabs");
            tabColumn.anchorMin = new Vector2(0, 0); tabColumn.anchorMax = new Vector2(0, 1); tabColumn.pivot = new Vector2(0, 1);
            tabColumn.sizeDelta = new Vector2(230, -130); tabColumn.anchoredPosition = new Vector2(20, -76);
            UiKit.VStack(tabColumn.gameObject, 6);

            for (int i = 0; i < tabs.Length; i++)
            {
                int idx = i;
                var b = UiKit.Button(tabColumn, tabs[i].name, () => { ShowTab(idx); FocusFirstRow(); }, 46, 24);
                tabButtons.Add(b);
                b.gameObject.AddComponent<TabCancel>().menu = this;
            }
            UiKit.Size(UiKit.Rect(tabColumn, "Gap"), 14);
            tabButtons.Add(UiKit.Button(tabColumn, "Resume", () => SetOpen(false), 46, 24));
            tabButtons.Add(UiKit.Button(tabColumn, "Restart match", () => { CommitNow(); needsRestart = false; MatchController.Instance?.ResetMatch(); SetOpen(false); }, 46, 24));
            tabButtons.Add(UiKit.Button(tabColumn, "Quit", Application.Quit, 46, 24));
            for (int i = 0; i < tabButtons.Count; i++)
            {
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                nav.selectOnUp = tabButtons[(i - 1 + tabButtons.Count) % tabButtons.Count];
                nav.selectOnDown = tabButtons[(i + 1) % tabButtons.Count];
                tabButtons[i].navigation = nav;
            }

            // Scrollable content.
            var view = UiKit.Box(canvas.transform, "Viewport", UiKit.Panel);
            var vrt = view.rectTransform;
            vrt.anchorMin = new Vector2(0, 0); vrt.anchorMax = new Vector2(1, 1);
            vrt.offsetMin = new Vector2(270, 60); vrt.offsetMax = new Vector2(-20, -76);
            view.gameObject.AddComponent<RectMask2D>();
            content = UiKit.Rect(view.transform, "Content");
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            UiKit.VStack(content.gameObject, 6, new RectOffset(14, 14, 12, 12));
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = content; scroll.viewport = vrt; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30f;
            view.gameObject.AddComponent<KeepSelectionVisible>().scroll = scroll;

            footer = UiKit.Text(canvas.transform, "Footer", "", 19, UiKit.TextDim);
            var frt = footer.rectTransform;
            frt.anchorMin = new Vector2(0, 0); frt.anchorMax = new Vector2(1, 0); frt.pivot = new Vector2(0.5f, 0);
            frt.sizeDelta = new Vector2(-300, 44); frt.anchoredPosition = new Vector2(130, 10);
            UpdateFooter();

            keyboard = canvas.gameObject.AddComponent<OnScreenKeyboard>();
            keyboard.Build(canvas.transform, menuGroup);
            var kbGroup = canvas.transform.Find("Keyboard").gameObject.AddComponent<CanvasGroup>();
            kbGroup.ignoreParentGroups = true;
        }

        internal void ShowTab(int i)
        {
            currentTab = i;
            for (int c = content.childCount - 1; c >= 0; c--) Destroy(content.GetChild(c).gameObject);
            content.DetachChildren();
            rows.Clear();
            liveTexts.Clear();
            tabs[i].build();
            for (int t = 0; t < tabs.Length; t++) tabButtons[t].targetGraphic.color = t == i ? new Color(0.3f, 0.26f, 0.1f) : UiKit.Row;
            foreach (var r in rows) r.Refresh();
            LinkRows();
            content.anchoredPosition = Vector2.zero;
        }

        void LinkRows()
        {
            var tab = tabButtons[currentTab];
            for (int i = 0; i < rows.Count; i++)
            {
                var s = rows[i].Focus;
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                nav.selectOnUp = i > 0 ? rows[i - 1].Focus : null;
                nav.selectOnDown = i < rows.Count - 1 ? rows[i + 1].Focus : null;
                if (s is Button || s is Toggle || s is TMP_InputField) nav.selectOnLeft = tab;
                s.navigation = nav;
                if (!s.GetComponent<RowCancel>()) s.gameObject.AddComponent<RowCancel>().menu = this;
            }
            var tnav = tab.navigation;
            tnav.selectOnRight = rows.Count > 0 ? rows[0].Focus : null;
            tab.navigation = tnav;
        }

        void FocusFirstRow()
        {
            if (rows.Count > 0) Select(rows[0].Focus);
        }

        internal void BackToTabs() => Select(tabButtons[currentTab]);

        // ── Row builders ────────────────────────────────────────────────────────────────────

        RectTransform RowBase(string label, float height = 46f)
        {
            var row = UiKit.Box(content, "Row_" + label, UiKit.Row);
            UiKit.HStack(row.gameObject, 10, new RectOffset(14, 10, 4, 4));
            UiKit.Size(row, height);
            var l = UiKit.Text(row.transform, "Label", label, 22, UiKit.TextMain);
            UiKit.Size(l, -1, 360);
            return row.rectTransform;
        }

        void Header(string text)
        {
            var t = UiKit.Text(content, "Header", text, 26, UiKit.Highlight);
            UiKit.Size(t, 40);
        }

        void Info(string text)
        {
            var t = UiKit.Text(content, "Info", text, 18, UiKit.TextDim);
            t.textWrappingMode = TextWrappingModes.Normal;
            UiKit.Size(t, 22 * Mathf.Max(1, Mathf.CeilToInt(text.Length / 85f)) + 6);
        }

        void Stepper(string label, Func<string> text, Action<int> step, bool restart = false, Action submit = null)
        {
            var row = RowBase(label);
            var go = row.gameObject;
            var st = go.AddComponent<StepperRow>();
            st.targetGraphic = go.GetComponent<Image>();
            st.colors = UiKit.Colors();
            st.getText = text;
            st.step = d => { step(d); Changed(restart); foreach (var l in liveTexts) if (l) l.Refresh(); };
            if (submit != null) st.submit = () => { submit(); Changed(restart); };
            var left = UiKit.Button(row, "<", () => { st.step(-1); st.Refresh(); }, 38, 24); UiKit.Size(left, -1, 44);
            st.valueText = UiKit.Text(row, "Value", "", 22, UiKit.Highlight, TextAlignmentOptions.Center);
            UiKit.Size(st.valueText, -1, -1, 1);
            var right = UiKit.Button(row, ">", () => { st.step(1); st.Refresh(); }, 38, 24); UiKit.Size(right, -1, 44);
            left.navigation = right.navigation = new Navigation { mode = Navigation.Mode.None };
            rows.Add(st);
        }

        void Choice<T>(string label, Func<T> get, Action<T> set, bool restart = false) where T : struct, Enum
        {
            var values = (T[])Enum.GetValues(typeof(T));
            Stepper(label, () => Pretty(get().ToString()), d =>
            {
                int i = Array.IndexOf(values, get());
                set(values[(i + d + values.Length) % values.Length]);
            }, restart);
        }

        void Slider(string label, Func<float> get, Action<float> set, float min, float max, float step, Func<float, string> fmt = null, bool restart = false)
        {
            var row = RowBase(label);
            var sr = row.gameObject.AddComponent<SliderRow>();
            var slider = BuildSlider(row);
            UiKit.Size(slider, 30, -1, 1);
            slider.minValue = 0; slider.maxValue = Mathf.Round((max - min) / step); slider.wholeNumbers = true;
            sr.slider = slider; sr.get = get; sr.min = min; sr.step = step; sr.format = fmt;
            sr.set = v => { set(Mathf.Clamp(v, min, max)); Changed(restart); };
            sr.valueText = UiKit.Text(row, "Value", "", 22, UiKit.Highlight, TextAlignmentOptions.Right);
            UiKit.Size(sr.valueText, -1, 110);
            sr.Init();
            rows.Add(sr);
        }

        static Slider BuildSlider(Transform parent)
        {
            var bg = UiKit.Box(parent, "Slider", new Color(0.1f, 0.1f, 0.12f));
            var slider = bg.gameObject.AddComponent<Slider>();
            var fillArea = UiKit.Rect(bg.transform, "FillArea"); UiKit.Fill(fillArea, 4, 10, 4, 10);
            var fill = UiKit.Box(fillArea, "Fill", new Color(0.85f, 0.65f, 0.1f));
            fill.rectTransform.sizeDelta = Vector2.zero;
            var handleArea = UiKit.Rect(bg.transform, "HandleArea"); UiKit.Fill(handleArea, 10, 0, 10, 0);
            var handle = UiKit.Box(handleArea, "Handle", Color.white);
            handle.rectTransform.sizeDelta = new Vector2(16, 0);
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            var sc = UiKit.Colors();
            sc.normalColor = new Color(0.85f, 0.86f, 0.9f);
            sc.highlightedColor = Color.white;
            sc.selectedColor = UiKit.Highlight;
            slider.colors = sc;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            return slider;
        }

        void Toggle(string label, Func<bool> get, Action<bool> set, bool restart = false)
        {
            var row = RowBase(label);
            var tr = row.gameObject.AddComponent<ToggleRow>();
            var toggle = row.gameObject.AddComponent<UnityEngine.UI.Toggle>();
            toggle.targetGraphic = row.GetComponent<Image>();
            toggle.colors = UiKit.Colors();
            var box = UiKit.Box(row, "Box", new Color(0.08f, 0.08f, 0.1f)); UiKit.Size(box, 30, 30);
            var check = UiKit.Box(box.transform, "Check", UiKit.Highlight); UiKit.Fill(check.rectTransform, 6, 6, 6, 6);
            toggle.graphic = check;
            var state = UiKit.Text(row, "State", "", 22, UiKit.TextDim); UiKit.Size(state, -1, -1, 1);
            tr.toggle = toggle; tr.get = get;
            tr.set = v => { set(v); state.text = v ? "On" : "Off"; Changed(restart); };
            toggle.onValueChanged.AddListener(v => state.text = v ? "On" : "Off");
            tr.Init();
            rows.Add(tr);
        }

        void Action(string label, Action act, Func<string> dynamicLabel = null)
        {
            var b = UiKit.Button(content, label, act, 46, 22);
            var br = b.gameObject.AddComponent<ButtonRow>();
            br.button = b; br.label = b.GetComponentInChildren<TextMeshProUGUI>(); br.getLabel = dynamicLabel;
            rows.Add(br);
        }

        void TextField(string label, Func<string> get, Action<string> set, bool restart = false)
        {
            Action($"{label}: {get()}", () => keyboard.Open(label, get(), 24, v =>
            {
                set(v);
                Changed(restart);
                foreach (var r in rows) r.Refresh();
            }), () => $"{label}: {get()}   [A: edit]");
        }

        // ── Tabs ────────────────────────────────────────────────────────────────────────────

        void BuildMatchTab()
        {
            Header("Match");
            Choice("Mode", () => S.match.mode, v => S.match.mode = v, true);
            Choice("Alliance", () => S.match.alliance, v => S.match.alliance = v, true);
            Choice("Start position", () => S.match.start, v => { S.match.start = v; ShowTabDeferred(); }, true);
            if (S.match.start == StartAnchor.Custom)
            {
                Slider("Custom start X (in, red frame)", () => S.match.customStart.xIn, v => S.match.customStart.xIn = v, -62f, -9f, 0.5f, F1, true);
                Slider("Custom start Y (in)", () => S.match.customStart.yIn, v => S.match.customStart.yIn = v, -62f, 62f, 0.5f, F1, true);
                Slider("Custom heading (deg)", () => S.match.customStart.headingDeg, v => S.match.customStart.headingDeg = v, -180f, 180f, 5f, F0, true);
                Info("A custom start must still follow G304: on your half, touching the wall, out of the LOADING ZONE, clear of FLOWERS.");
                Action("Save this custom start", () =>
                {
                    var c = S.match.customStart;
                    S.match.savedStarts.Add(new CustomPose { xIn = c.xIn, yIn = c.yIn, headingDeg = c.headingDeg });
                    if (S.match.savedStarts.Count > 8) S.match.savedStarts.RemoveAt(0);
                    Changed(); ShowTabDeferred();
                });
                if (S.match.savedStarts.Count > 0)
                {
                    savedStart = Mathf.Clamp(savedStart, 0, S.match.savedStarts.Count - 1);
                    Stepper("Saved start", () =>
                    {
                        var p = S.match.savedStarts[savedStart];
                        return $"{savedStart + 1}/{S.match.savedStarts.Count}: ({p.xIn:0}, {p.yIn:0}) {p.headingDeg:0} deg";
                    }, d => savedStart = (savedStart + d + S.match.savedStarts.Count) % S.match.savedStarts.Count);
                    Action("Use saved start", () =>
                    {
                        var p = S.match.savedStarts[savedStart];
                        S.match.customStart = new CustomPose { xIn = p.xIn, yIn = p.yIn, headingDeg = p.headingDeg };
                        Changed(true); ShowTabDeferred();
                    });
                }
            }
            Stepper("AUTO", () => AutoName(S.match.auto), d =>
            {
                var all = (AutoRoutine[])Enum.GetValues(typeof(AutoRoutine));
                S.match.auto = all[(Array.IndexOf(all, S.match.auto) + d + all.Length) % all.Length];
            }, true);
            Live(() => S.match.auto == AutoRoutine.Off
                ? "You drive the robot during AUTO."
                : "The robot runs this pre-programmed routine during AUTO (no driver input, G401).");
            Header("Practice robots");
            Choice("Partner", () => S.match.partner, v => S.match.partner = v, true);
            Choice("Opponent 1", () => S.match.opponent1, v => S.match.opponent1 = v, true);
            Choice("Opponent 2", () => S.match.opponent2, v => S.match.opponent2 = v, true);
            Header("Display and rules");
            Toggle("Show event log", () => S.match.showEventLog, v => S.match.showEventLog = v);
            Toggle("Enforce penalties", () => S.match.enforcePenalties, v => S.match.enforcePenalties = v);
            Action("Restart match now", () => { CommitNow(); needsRestart = false; MatchController.Instance?.ResetMatch(); SetOpen(false); });
        }

        // Labels and one-line descriptions, as dsim's builder shows them.
        static string IntakeName(IntakeKind k) => k == IntakeKind.Sweeper ? "Sweeper" : k == IntakeKind.SideRollers ? "Side rollers" : "Deployable ramp";
        static string IntakeBlurb(IntakeKind k) =>
            k == IntakeKind.Sweeper ? "Ground POLLEN only - can't reach into a FLOWER's opening"
            : k == IntakeKind.SideRollers ? "Reaches into a FLOWER's opening and pulls the bottom POLLEN out"
            : "Deploy to wedge under a FLOWER's bottom POLLEN - folded, it takes nothing";
        static string LauncherName(LauncherKind k) => k == LauncherKind.Turret ? "Single turret" : k == LauncherKind.DoubleTurret ? "Double turret" : "Dumper";
        static string LauncherBlurb(LauncherKind k) =>
            k == LauncherKind.Turret ? "POLLEN only - aims itself"
            : k == LauncherKind.DoubleTurret ? "One POLLEN turret, one NECTAR turret"
            : "POLLEN and NECTAR - turn to aim";
        static string MountName(IntakeMount m)
        {
            switch (m)
            {
                case IntakeMount.Front: return "Front";
                case IntakeMount.Back: return "Back";
                case IntakeMount.Left: return "Left side";
                case IntakeMount.Right: return "Right side";
                case IntakeMount.Side: return "Both sides (double)";
                default: return "Front + Back (double)";
            }
        }

        // Menu order: single edges first, then the two double intakes.
        static readonly IntakeMount[] MountOrder =
        {
            IntakeMount.Front, IntakeMount.Back, IntakeMount.Left, IntakeMount.Right, IntakeMount.Side, IntakeMount.FrontAndBack,
        };
        static string AutoName(AutoRoutine a) =>
            a == AutoRoutine.Off ? "Off - you drive"
            : a == AutoRoutine.Leave ? "Routine: leave the wall"
            : a == AutoRoutine.LaunchPreloadsAndLeave ? "Routine: launch preloads"
            : "Routine: launch preloads, then park";
        static string DrivetrainName(DrivetrainType d) => d == DrivetrainType.XDrive ? "X-drive" : d.ToString();

        void BuildRobotTab()
        {
            Header("Start from");
            var presets = RobotPresets.All();
            Stepper("Preset", () => presets[presetIndex].name + " - " + presets[presetIndex].teamName,
                d => presetIndex = (presetIndex + d + presets.Length) % presets.Length, false,
                () => LoadRobot(RobotPresets.All()[presetIndex]));
            Action("Load selected preset", () => LoadRobot(RobotPresets.All()[presetIndex]));
            if (S.savedRobots.Count > 0)
            {
                savedSlot = Mathf.Clamp(savedSlot, 0, S.savedRobots.Count - 1);
                Stepper("Saved robot", () => $"{savedSlot + 1}/{S.savedRobots.Count}: {S.savedRobots[savedSlot].name}",
                    d => savedSlot = (savedSlot + d + S.savedRobots.Count) % S.savedRobots.Count);
                Action("Load saved robot", () => LoadRobot(S.savedRobots[savedSlot].Clone()));
                Action("Delete saved robot", () => { S.savedRobots.RemoveAt(savedSlot); savedSlot = 0; Changed(); ShowTab(currentTab); });
            }

            Header("Build");
            TextField("Robot name", () => R.name, v => R.name = v, true);
            TextField("Team name", () => R.teamName, v => R.teamName = v, true);
            TextField("Team #", () => R.teamNumber == 0 ? "" : R.teamNumber.ToString(), v =>
            {
                int.TryParse(new string(System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(v ?? "", char.IsDigit))), out int n);
                R.teamNumber = Mathf.Clamp(n, 0, 99999);
            }, true);
            Action("Save this robot", () =>
            {
                S.savedRobots.Add(R.Clone());
                if (S.savedRobots.Count > SimSettings.MaxSavedRobots) S.savedRobots.RemoveAt(0);
                Changed(); ShowTabDeferred();
            });

            Header("Drivetrain");
            Stepper("Drivetrain", () => DrivetrainName(R.drivetrain), d =>
            {
                var all = (DrivetrainType[])Enum.GetValues(typeof(DrivetrainType));
                R.drivetrain = all[(Array.IndexOf(all, R.drivetrain) + d + all.Length) % all.Length];
                R.Validate();
                ShowTabDeferred();
            }, true);
            var rr = RobotConfig.RpmRange(R.drivetrain);
            Slider(R.drivetrain == DrivetrainType.Butterfly ? "Mecanum RPM" : "Drive RPM", () => R.driveRpm, v => R.driveRpm = v, rr.x, rr.y, 5f, F0, true);
            if (R.drivetrain == DrivetrainType.Butterfly)
                Slider("Traction RPM", () => R.butterflyTractionRpm, v => R.butterflyTractionRpm = v,
                    RobotConfig.ButterflyTractionRpmRange.x, RobotConfig.ButterflyTractionRpmRange.y, 5f, F0, true);
            Live(DriveSummary);

            Header("Frame");
            Slider("Length", () => R.lengthIn, v => R.lengthIn = v, RobotConfig.MinSize, RobotConfig.MaxSize, RobotConfig.SizeStep, v => v.ToString("0.0") + "\"", true);
            Slider("Width", () => R.widthIn, v => R.widthIn = v, RobotConfig.MinSize, RobotConfig.MaxSize, RobotConfig.SizeStep, v => v.ToString("0.0") + "\"", true);
            var mr = RobotConfig.MassRange(R.drivetrain);
            Slider("Mass", () => R.massLb, v => R.massLb = v, mr.x, mr.y, 0.5f, v => v.ToString("0.0") + " lb", true);
            Slider("Hopper", () => R.storage, v => R.storage = Mathf.RoundToInt(v), RobotConfig.StorageMin, RobotConfig.StorageMax, 1f,
                v => $"{v:0} / {RobotConfig.StorageMax} pollen", true);
            Slider("Height", () => R.heightIn, v => R.heightIn = v, RobotConfig.MinHeight, RobotConfig.MaxHeight, 1f, v => v.ToString("0") + "\"", true);
            Slider("Height, starting configuration", () => R.stowHeightIn, v => R.stowHeightIn = v, RobotConfig.MinHeight, RobotConfig.MaxHeight, 1f, v => v.ToString("0") + "\"", true);

            Header("Look");
            Stepper("Chassis colour", () => PaletteName(R.chassisColor), d => R.chassisColor = PaletteStep(R.chassisColor, d), true);
            Toggle("Accent matches chassis", () => R.accentMatchesChassis, v => { R.accentMatchesChassis = v; ShowTabDeferred(); }, true);
            if (!R.accentMatchesChassis)
                Stepper("Accent colour", () => PaletteName(R.accentColor), d => R.accentColor = PaletteStep(R.accentColor, d), true);
            Choice("Decal", () => R.decal, v => R.decal = v, true);
            Choice("Sign plate", () => R.plate, v => R.plate = v, true);
        }

        void BuildMechanismTab()
        {
            Header("Intake");
            Stepper("Intake", () => IntakeName(R.intakeKind), d =>
            {
                var all = (IntakeKind[])Enum.GetValues(typeof(IntakeKind));
                R.intakeKind = all[(Array.IndexOf(all, R.intakeKind) + d + all.Length) % all.Length];
            }, true);
            Live(() => IntakeBlurb(R.intakeKind));
            Stepper("Intake mount", () => MountName(R.intakeMount), d =>
            {
                int i = Array.IndexOf(MountOrder, R.intakeMount);
                R.intakeMount = MountOrder[(i + d + MountOrder.Length) % MountOrder.Length];
            }, true);
            Live(() => R.IsDoubleIntake
                ? "Double intake: rollers on two opposite edges, so you can collect driving either way (+1.5 lb)."
                : "Single intake on one edge.");
            Choice("Intake reach", () => R.intakeReach, v => R.intakeReach = v, true);

            Header("Launcher");
            Stepper("Launcher", () => LauncherName(R.launcher), d =>
            {
                var all = (LauncherKind[])Enum.GetValues(typeof(LauncherKind));
                R.launcher = all[(Array.IndexOf(all, R.launcher) + d + all.Length) % all.Length];
                R.Validate();
                ShowTabDeferred();
            }, true);
            Live(() => LauncherBlurb(R.launcher));
            string other(MountPos p, bool forFirst)
            {
                if (R.hasBoxTube && R.boxTubeMount == p) return "Box Tube";
                if (forFirst && R.launcher == LauncherKind.DoubleTurret && R.launcherMount2 == p) return "NECTAR";
                if (!forFirst && R.launcherMount == p) return "POLLEN";
                return null;
            }
            switch (R.launcher)
            {
                case LauncherKind.Turret:
                    MountGrid("Turret", () => R.launcherMount, v => R.launcherMount = v,
                        p => !(R.hasBoxTube && R.boxTubeMount == p), p => other(p, true));
                    break;
                case LauncherKind.DoubleTurret:
                    MountGrid("POLLEN turret", () => R.launcherMount, v => R.launcherMount = v,
                        p => p != MountPos.Center && !(R.hasBoxTube && R.boxTubeMount == p), p => other(p, true));
                    MountGrid("NECTAR turret", () => R.launcherMount2, v => R.launcherMount2 = v,
                        p => p != MountPos.Center && !RobotConfig.Adjacent(R.launcherMount, p) && !(R.hasBoxTube && R.boxTubeMount == p),
                        p => other(p, false));
                    Live(() => "Turret rings can't sit in neighbouring cells, so the NECTAR turret goes at least two cells away.");
                    break;
                case LauncherKind.Dumper:
                    MountGrid("Dumper edge", () => R.launcherMount, v => R.launcherMount = v,
                        p => RobotConfig.IsEdge(p) && !(R.hasBoxTube && R.boxTubeMount == p), p => other(p, true));
                    Slider("Hood angle", () => R.hoodDeg, v => R.hoodDeg = v, RobotConfig.HoodMin, RobotConfig.HoodMax, 1f, v => v.ToString("0") + " deg", true);
                    break;
            }

            Header("Flower scoring");
            Stepper("Flower scoring", () => R.hasBoxTube ? "Box Tube" : "None", d => { R.hasBoxTube = !R.hasBoxTube; R.Validate(); ShowTabDeferred(); }, true);
            if (R.hasBoxTube)
                MountGrid("Box Tube", () => R.boxTubeMount, v => R.boxTubeMount = v,
                    p => p != MountPos.Center && R.BoxTubeCellFree(p),
                    p => R.launcherMount == p ? (R.launcher == LauncherKind.DoubleTurret ? "POLLEN" : "Launcher")
                        : R.launcher == LauncherKind.DoubleTurret && R.launcherMount2 == p ? "NECTAR" : null);
            Live(() => R.CarriesNectar ? "This build can carry its own alliance's NECTAR." : "A single turret without a Box Tube carries POLLEN only.");

            Header("Passing");
            Slider("Pass target X (in, red frame)", () => R.passTargetIn.x, v => R.passTargetIn.x = v, -70f, 70f, 1f, F0);
            Slider("Pass target Y (in)", () => R.passTargetIn.y, v => R.passTargetIn.y = v, -70f, 70f, 1f, F0);
        }

        /// <summary>A 3x3 chassis-map row (dsim's mount picker).</summary>
        void MountGrid(string label, Func<MountPos> get, Action<MountPos> set, Func<MountPos, bool> allowed, Func<MountPos, string> marker)
        {
            var row = RowBase(label, 150f);
            var grid = row.gameObject.AddComponent<MountGridRow>();
            grid.targetGraphic = row.GetComponent<Image>();
            grid.colors = UiKit.Colors();
            grid.get = get;
            grid.allowed = allowed;
            grid.marker = marker;
            grid.set = v =>
            {
                set(v);
                R.Validate();
                Changed(true);
                foreach (var r in rows) r.Refresh();
            };
            var holder = UiKit.Rect(row, "Grid");
            UiKit.Size(holder, 138, 420);
            var col = holder.gameObject.AddComponent<VerticalLayoutGroup>();
            col.spacing = 2; col.childControlHeight = true; col.childControlWidth = true; col.childForceExpandWidth = true;
            var front = UiKit.Text(holder, "Front", "^ FRONT", 16, UiKit.TextDim, TextAlignmentOptions.Center);
            UiKit.Size(front, 18);
            var cellsRoot = UiKit.Rect(holder, "Cells");
            UiKit.Size(cellsRoot, 114);
            var gl = cellsRoot.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(136, 36); gl.spacing = new Vector2(4, 3);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 3;
            foreach (MountPos p in Enum.GetValues(typeof(MountPos)))
            {
                var pos = p;
                var img = UiKit.Box(cellsRoot, "Cell_" + p, UiKit.Row);
                var b = img.gameObject.AddComponent<Button>();
                b.targetGraphic = img;
                b.navigation = new Navigation { mode = Navigation.Mode.None };
                b.onClick.AddListener(() => grid.Pick(pos));
                var t = UiKit.Text(img.transform, "T", MountGridRow.Name(p), 15, UiKit.TextMain, TextAlignmentOptions.Center);
                UiKit.Fill(t.rectTransform);
                grid.cells.Add((pos, img, t, b));
            }
            rows.Add(grid);
        }

        /// <summary>A description line that updates when the choice above it changes.</summary>
        void Live(Func<string> text)
        {
            var t = UiKit.Text(content, "Live", text(), 18, UiKit.TextDim);
            UiKit.Size(t, 28);
            var lt = t.gameObject.AddComponent<LiveText>();
            lt.get = text; lt.text = t;
            liveTexts.Add(lt);
        }

        readonly List<LiveText> liveTexts = new List<LiveText>();

        void BuildDrivingTab()
        {
            Header("Driver assists");
            Toggle("Field-centric drive", () => S.assists.fieldCentric, v => S.assists.fieldCentric = v);
            Toggle("Aim assist (turret tracks HIVE)", () => S.assists.aimAssist, v => S.assists.aimAssist = v);
            Toggle("Auto intake", () => S.assists.autoIntake, v => S.assists.autoIntake = v);
            Toggle("Auto fire", () => S.assists.autoFire, v => S.assists.autoFire = v);
            Choice("Tank-drive control", () => S.assists.controlMode, v => S.assists.controlMode = v);
            Slider("Slow mode speed (%)", () => S.assists.slowModePercent, v => S.assists.slowModePercent = v, 10f, 100f, 5f, F0);
            Header("Sticks and triggers");
            Choice("Drive stick", () => S.controls.driveStick, v => S.controls.driveStick = v);
            Slider("Stick deadzone", () => S.controls.deadzone, v => S.controls.deadzone = v, 0f, 0.4f, 0.01f, F2);
            Slider("Response curve (1 = linear)", () => S.controls.curve, v => S.controls.curve = v, 1f, 3f, 0.1f, F1);
            Slider("Trigger threshold", () => S.controls.triggerThreshold, v => S.controls.triggerThreshold = v, 0.05f, 0.95f, 0.05f, F2);
        }

        void BuildControlsTab()
        {
            Header("Rebind controls");
            Info("Select a row and press A, then press the new button. Esc on a keyboard cancels.");
            var hub = InputHub.Instance;
            if (!hub) return;
            foreach (var action in InputHub.RebindableActions)
            {
                string a = action;
                Action($"{Pretty(a)} (gamepad): {hub.Action(a)?.GetBindingDisplayString(InputBinding.MaskByGroup("Gamepad"))}",
                    () => StartCoroutine(Rebind(a, false)),
                    () => $"{Pretty(a)} (gamepad): {hub.Action(a)?.GetBindingDisplayString(InputBinding.MaskByGroup("Gamepad"))}");
                Action($"{Pretty(a)} (keyboard): {hub.Action(a)?.GetBindingDisplayString(InputBinding.MaskByGroup("Keyboard"))}",
                    () => StartCoroutine(Rebind(a, true)),
                    () => $"{Pretty(a)} (keyboard): {hub.Action(a)?.GetBindingDisplayString(InputBinding.MaskByGroup("Keyboard"))}");
            }
            Action("Reset all bindings to defaults", () => { hub.ResetBindings(); foreach (var r in rows) r.Refresh(); });
        }

        IEnumerator Rebind(string action, bool keyboard)
        {
            var es = EventSystem.current;
            var selected = es ? es.currentSelectedGameObject : null;
            var label = selected ? selected.GetComponentInChildren<TextMeshProUGUI>() : null;
            if (label) label.text = $"Press a {(keyboard ? "key" : "gamepad button")} for {Pretty(action)}...";
            if (es) es.sendNavigationEvents = false;
            // Wait for the A press that opened this to be released, so it is not captured itself.
            yield return new WaitForSecondsRealtime(0.35f);
            bool finished = false;
            InputHub.Instance.StartRebind(action, keyboard, ok => finished = true);
            while (!finished) yield return null;
            yield return new WaitForSecondsRealtime(0.2f);
            if (es) es.sendNavigationEvents = true;
            foreach (var r in rows) r.Refresh();
        }

        void BuildRulesTab()
        {
            Header("Rule assumptions");
            Info("FIRST does not publish these values. Defaults come from the two official HIVE calibration points (8 POLLEN; 3 POLLEN + 3 NECTAR). See Docs/biobuzz-facts.md.");
            Slider("HIVE tip load (POLLEN)", () => S.rules.tipLoadPollen, v => S.rules.tipLoadPollen = v, 2f, 20f, 0.5f, F1);
            Slider("NECTAR weight (in POLLEN)", () => S.rules.nectarWeight, v => S.rules.nectarWeight = v, 0.5f, 5f, 0.05f, F2);
            Info(TipTable());
            Slider("HIVE swing time (s)", () => S.rules.hiveSwingSeconds, v => S.rules.hiveSwingSeconds = v, 0.5f, 8f, 0.25f, F2);
            Slider("POLLEN mass (g)", () => S.rules.pollenMassGrams, v => S.rules.pollenMassGrams = v, 10f, 120f, 1f, F0, true);
            Slider("NECTAR mass (g)", () => S.rules.nectarMassGrams, v => S.rules.nectarMassGrams = v, 10f, 200f, 1f, F0, true);
            Toggle("PARK counts in own LOADING ZONE only", () => S.rules.parkOwnLoadingZoneOnly, v => S.rules.parkOwnLoadingZoneOnly = v);
        }

        void BuildAudioTab()
        {
            Header("Audio");
            Slider("Master", () => S.audio.master, v => S.audio.master = v, 0f, 1f, 0.05f, Pct);
            Slider("Field cues", () => S.audio.fieldCues, v => S.audio.fieldCues = v, 0f, 1f, 0.05f, Pct);
            Slider("Robot drive", () => S.audio.robot, v => S.audio.robot = v, 0f, 1f, 0.05f, Pct);
            Slider("Launcher", () => S.audio.launch, v => S.audio.launch = v, 0f, 1f, 0.05f, Pct);
            Slider("Intake", () => S.audio.intake, v => S.audio.intake = v, 0f, 1f, 0.05f, Pct);
            Slider("HIVE", () => S.audio.hive, v => S.audio.hive = v, 0f, 1f, 0.05f, Pct);
            Slider("Alerts and fouls", () => S.audio.alerts, v => S.audio.alerts = v, 0f, 1f, 0.05f, Pct);
            Slider("Countdown beeps", () => S.audio.voice, v => S.audio.voice = v, 0f, 1f, 0.05f, Pct);
            Toggle("Transition countdown", () => S.audio.voiceCues, v => S.audio.voiceCues = v);
        }

        void BuildGraphicsTab()
        {
            Header("Graphics");
            Choice("Quality preset", () => S.graphics.preset, v => { S.graphics.ApplyPreset(v); ShowTabDeferred(); });
            Slider("Render scale", () => S.graphics.renderScale, v => { S.graphics.renderScale = v; S.graphics.preset = QualityPreset.Custom; }, 0.6f, 1.4f, 0.05f, F2);
            Choice("Anti-aliasing (MSAA)", () => S.graphics.msaa, v => { S.graphics.msaa = v; S.graphics.preset = QualityPreset.Custom; });
            Choice("Shadows", () => S.graphics.shadows, v => { S.graphics.shadows = v; S.graphics.preset = QualityPreset.Custom; });
            Choice("Moving-object shadows", () => S.graphics.dynamicShadows, v => { S.graphics.dynamicShadows = v; S.graphics.preset = QualityPreset.Custom; });
            Choice("Venue", () => S.graphics.venue, v => S.graphics.venue = v);
            Slider("Desktop frame cap (no headset)", () => S.graphics.targetRefreshRate, v => S.graphics.targetRefreshRate = v, 60f, 144f, 1f, F0);
            Info("In a headset the refresh rate is set by your VR runtime (SteamVR / Meta / WMR settings).");
            Toggle("Reduced motion (comfort)", () => S.graphics.reducedMotion, v => S.graphics.reducedMotion = v);
            Toggle("Minimap on driver display", () => S.graphics.minimap, v => S.graphics.minimap = v);
            Choice("Performance overlay", () => S.graphics.perfOverlay, v => S.graphics.perfOverlay = v);
        }

        void BuildCameraTab()
        {
            Header("Camera");
            Choice("View", () => S.camera.view, v => S.camera.view = v);
            Choice("Driver station position", () => S.camera.station, v => S.camera.station = v);
            Slider("Eye height override (in, 0 = tracked)", () => S.camera.eyeHeightOverrideIn, v => S.camera.eyeHeightOverrideIn = v < 36f ? 0f : v, 0f, 84f, 1f, v => v <= 0f ? "tracked" : v.ToString("0"));
            Slider("Chase distance (in)", () => S.camera.chaseDistanceIn, v => S.camera.chaseDistanceIn = v, 24f, 160f, 2f, F0);
            Slider("Chase height (in)", () => S.camera.chaseHeightIn, v => S.camera.chaseHeightIn = v, 12f, 120f, 2f, F0);
            Slider("Overhead height (in)", () => S.camera.overheadHeightIn, v => S.camera.overheadHeightIn = v, 80f, 300f, 5f, F0);
            Toggle("Comfort vignette", () => S.camera.comfortVignette, v => S.camera.comfortVignette = v);
            Info("Chase and robot-POV views move you without your body moving, which can cause motion sickness. Driver station is the most comfortable.");
            Action("Recenter view", () => ViewManager.Instance?.Recenter());
            Action("Reset ALL settings to defaults", () => { SettingsStore.ResetToDefaults(); InputHub.Instance?.ResetBindings(); needsRestart = true; ShowTab(currentTab); });
        }

        /// <summary>Rebuild the current tab next frame (rows depend on the new value), keeping the cursor on the same row.</summary>
        void ShowTabDeferred()
        {
            int keep = 0;
            var sel = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            for (int i = 0; i < rows.Count; i++) if (sel && rows[i].Focus && rows[i].Focus.gameObject == sel) keep = i;
            StartCoroutine(NextFrame(() =>
            {
                CommitNow();
                ShowTab(currentTab);
                if (rows.Count > 0) Select(rows[Mathf.Min(keep, rows.Count - 1)].Focus);
            }));
        }

        static IEnumerator NextFrame(Action a) { yield return null; a(); }

        // ── Helpers ─────────────────────────────────────────────────────────────────────────

        void LoadRobot(RobotConfig c)
        {
            S.robot = c.Clone();
            S.robot.Validate();
            Changed(true);
            CommitNow();
            ShowTab(currentTab);
        }

        string DriveSummary()
        {
            var cfg = R.Clone(); cfg.Validate();
            var p = Robot.DriveParams.For(cfg, false);
            return $"Top speed {Units.ToIn(p.vFwd):0} in/s forward, {Units.ToIn(p.vStrafe):0} in/s strafe; turn {p.omegaMax * Mathf.Rad2Deg:0} deg/s; push {p.PushForceN:0} N.";
        }

        static string TipTable()
        {
            var sb = new System.Text.StringBuilder("POLLEN needed to tip by NECTAR in the cell: ");
            for (int n = 0; n <= 5; n++) sb.Append($"{n}N->{TipModel.PollenNeeded(n, SettingsStore.Current.rules)}P  ");
            return sb.ToString();
        }

        static string PaletteName(string hex)
        {
            foreach (var p in Palette) if (string.Equals(p.hex, hex, StringComparison.OrdinalIgnoreCase)) return p.name;
            return hex;
        }

        static string PaletteStep(string hex, int d)
        {
            int i = Array.FindIndex(Palette, p => string.Equals(p.hex, hex, StringComparison.OrdinalIgnoreCase));
            return Palette[((i < 0 ? 0 : i) + d + Palette.Length) % Palette.Length].hex;
        }

        static string Pretty(string s)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && char.IsUpper(s[i]) && !char.IsUpper(s[i - 1])) sb.Append(' ');
                sb.Append(s[i]);
            }
            return sb.ToString();
        }

        static string F0(float v) => v.ToString("0");
        static string F1(float v) => v.ToString("0.0");
        static string F2(float v) => v.ToString("0.00");
        static string Pct(float v) => Mathf.RoundToInt(v * 100f) + "%";
    }

    /// <summary>B on a content row returns to the tab list.</summary>
    public class RowCancel : MonoBehaviour, ICancelHandler
    {
        public SettingsMenu menu;
        public void OnCancel(BaseEventData e) => menu.BackToTabs();
    }

    /// <summary>B on the tab list closes the menu.</summary>
    public class TabCancel : MonoBehaviour, ICancelHandler
    {
        public SettingsMenu menu;
        public void OnCancel(BaseEventData e) => menu.SetOpen(false);
    }
}
