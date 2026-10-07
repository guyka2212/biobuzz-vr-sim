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

        static readonly (string name, string hex)[] Palette =
        {
            ("Graphite", "#2B2F36"), ("Black", "#141414"), ("Silver", "#B8BCC2"), ("White", "#EDEDED"),
            ("Gold", "#F2B705"), ("Orange", "#FF8A3D"), ("Green", "#3DBB5C"), ("Lime", "#9BE15D"),
            ("Teal", "#1FB5A6"), ("Purple", "#8E5CE6"), ("Pink", "#F0609E"), ("Navy", "#22305A"),
        };

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
        }

        void ShowTab(int i)
        {
            currentTab = i;
            for (int c = content.childCount - 1; c >= 0; c--) Destroy(content.GetChild(c).gameObject);
            content.DetachChildren();
            rows.Clear();
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
            st.step = d => { step(d); Changed(restart); };
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
            slider.colors = UiKit.Colors();
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
            var row = RowBase(label);
            var box = UiKit.Box(row, "Input", new Color(0.08f, 0.08f, 0.1f));
            UiKit.Size(box, 36, -1, 1);
            var area = UiKit.Rect(box.transform, "TextArea"); UiKit.Fill(area, 10, 2, 10, 2);
            area.gameObject.AddComponent<RectMask2D>();
            var text = UiKit.Text(area, "Text", "", 22, UiKit.TextMain);
            UiKit.Fill(text.rectTransform);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            var input = box.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = area; input.textComponent = text; input.targetGraphic = box;
            input.characterLimit = 24;
            input.colors = UiKit.Colors();
            input.onEndEdit.AddListener(v => { set(v); Changed(restart); });
            var row2 = row.gameObject.AddComponent<InputRow>();
            row2.input = input; row2.get = get;
            rows.Add(row2);
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
            Choice("AUTO routine", () => S.match.auto, v => S.match.auto = v, true);
            Header("Practice robots");
            Choice("Partner", () => S.match.partner, v => S.match.partner = v, true);
            Choice("Opponent 1", () => S.match.opponent1, v => S.match.opponent1 = v, true);
            Choice("Opponent 2", () => S.match.opponent2, v => S.match.opponent2 = v, true);
            Header("Display and rules");
            Toggle("Show event log", () => S.match.showEventLog, v => S.match.showEventLog = v);
            Toggle("Enforce penalties", () => S.match.enforcePenalties, v => S.match.enforcePenalties = v);
            Action("Restart match now", () => { CommitNow(); needsRestart = false; MatchController.Instance?.ResetMatch(); SetOpen(false); });
        }

        void BuildRobotTab()
        {
            Header("Robot");
            var presets = RobotPresets.All();
            Stepper("Preset", () => presets[presetIndex].name + " - " + presets[presetIndex].teamName,
                d => presetIndex = (presetIndex + d + presets.Length) % presets.Length, false,
                () => LoadRobot(RobotPresets.All()[presetIndex]));
            Action("Load selected preset", () => LoadRobot(RobotPresets.All()[presetIndex]));
            TextField("Robot name", () => R.name, v => R.name = v, true);
            Stepper("Team number", () => R.teamNumber == 0 ? "-" : R.teamNumber.ToString(), d => R.teamNumber = Mathf.Clamp(R.teamNumber + d * teamStep, 0, 99999), true,
                () => teamStep = teamStep >= 10000 ? 1 : teamStep * 10);
            Info("Team number: left/right adds the step, A changes the step (1, 10, 100, 1000, 10000).");
            Choice("Drivetrain", () => R.drivetrain, v => { R.drivetrain = v; ShowTabDeferred(); }, true);
            Slider("Length (in)", () => R.lengthIn, v => R.lengthIn = v, RobotConfig.MinSize, RobotConfig.MaxSize, RobotConfig.SizeStep, F1, true);
            Slider("Width (in)", () => R.widthIn, v => R.widthIn = v, RobotConfig.MinSize, RobotConfig.MaxSize, RobotConfig.SizeStep, F1, true);
            Slider("Height, deployed (in)", () => R.heightIn, v => R.heightIn = v, RobotConfig.MinHeight, RobotConfig.MaxHeight, 1f, F0, true);
            Slider("Height, stowed (in)", () => R.stowHeightIn, v => R.stowHeightIn = v, RobotConfig.MinHeight, RobotConfig.MaxHeight, 1f, F0, true);
            var mr = RobotConfig.MassRange(R.drivetrain);
            Slider("Mass (lb)", () => R.massLb, v => R.massLb = v, mr.x, mr.y, 0.5f, F1, true);
            var rr = RobotConfig.RpmRange(R.drivetrain);
            Slider("Drive wheel RPM", () => R.driveRpm, v => R.driveRpm = v, rr.x, rr.y, 5f, F0, true);
            if (R.drivetrain == DrivetrainType.Butterfly)
                Slider("Traction-mode RPM", () => R.butterflyTractionRpm, v => R.butterflyTractionRpm = v,
                    RobotConfig.ButterflyTractionRpmRange.x, RobotConfig.ButterflyTractionRpmRange.y, 5f, F0, true);
            Info(DriveSummary());
            Header("Colours");
            Stepper("Chassis colour", () => PaletteName(R.chassisColor), d => R.chassisColor = PaletteStep(R.chassisColor, d), true);
            Stepper("Accent colour", () => PaletteName(R.accentColor), d => R.accentColor = PaletteStep(R.accentColor, d), true);
            Header("Saved robots");
            Action("Save current robot", () =>
            {
                S.savedRobots.Add(R.Clone());
                if (S.savedRobots.Count > SimSettings.MaxSavedRobots) S.savedRobots.RemoveAt(0);
                Changed(); ShowTab(currentTab);
            });
            if (S.savedRobots.Count > 0)
            {
                savedSlot = Mathf.Clamp(savedSlot, 0, S.savedRobots.Count - 1);
                Stepper("Saved robot", () => $"{savedSlot + 1}/{S.savedRobots.Count}: {S.savedRobots[savedSlot].name}",
                    d => savedSlot = (savedSlot + d + S.savedRobots.Count) % S.savedRobots.Count);
                Action("Load saved robot", () => LoadRobot(S.savedRobots[savedSlot].Clone()));
                Action("Delete saved robot", () => { S.savedRobots.RemoveAt(savedSlot); savedSlot = 0; Changed(); ShowTab(currentTab); });
            }
        }

        int teamStep = 1;

        void BuildMechanismTab()
        {
            Header("Intake");
            Choice("Intake type", () => R.intakeKind, v => R.intakeKind = v, true);
            Choice("Intake reach", () => R.intakeReach, v => R.intakeReach = v, true);
            Choice("Intake mount", () => R.intakeMount, v => R.intakeMount = v, true);
            Info("Side rollers, or a deployed ramp, can pull POLLEN out of a FLOWER's retrieval opening; a sweeper cannot.");
            Header("Launcher");
            Choice("Launcher", () => R.launcher, v => { R.launcher = v; ShowTabDeferred(); }, true);
            Choice(R.launcher == LauncherKind.DoubleTurret ? "POLLEN turret mount" : "Launcher mount", () => R.launcherMount, v => R.launcherMount = v, true);
            if (R.launcher == LauncherKind.DoubleTurret) Choice("NECTAR turret mount", () => R.launcherMount2, v => R.launcherMount2 = v, true);
            if (R.launcher == LauncherKind.Dumper) Slider("Hood angle (deg)", () => R.hoodDeg, v => R.hoodDeg = v, RobotConfig.HoodMin, RobotConfig.HoodMax, 1f, F0, true);
            Header("Box Tube (places into FLOWERS)");
            Toggle("Box Tube fitted", () => R.hasBoxTube, v => { R.hasBoxTube = v; ShowTabDeferred(); }, true);
            if (R.hasBoxTube) Choice("Box Tube mount", () => R.boxTubeMount, v => R.boxTubeMount = v, true);
            Header("Storage and passing");
            Slider("Storage capacity", () => R.storage, v => R.storage = Mathf.RoundToInt(v), RobotConfig.StorageMin, RobotConfig.StorageMax, 1f, F0, true);
            Slider("Pass target X (in, red frame)", () => R.passTargetIn.x, v => R.passTargetIn.x = v, -70f, 70f, 1f, F0);
            Slider("Pass target Y (in)", () => R.passTargetIn.y, v => R.passTargetIn.y = v, -70f, 70f, 1f, F0);
            Info(R.CarriesNectar ? "This build can carry its own alliance's NECTAR." : "A single turret without a Box Tube carries POLLEN only.");
        }

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

    public class InputRow : MonoBehaviour, IMenuRow
    {
        public TMP_InputField input;
        public Func<string> get;
        public Selectable Focus => input;
        public void Refresh() { if (!input.isFocused) input.SetTextWithoutNotify(get()); }
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
