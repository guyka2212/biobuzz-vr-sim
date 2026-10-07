using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VrFsim.Game;
using VrFsim.Settings;
using VrFsim.UI;

namespace VrFsim.Tests
{
    /// <summary>Drives the in-VR menu with a virtual gamepad, the way a player would.</summary>
    public class GamepadMenuTests
    {
        Gamepad pad;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            var s = new SimSettings();
            s.match.mode = GameMode.FreeDrive;
            SettingsStore.UseTransient(s);
            SimWorld.Field = null;
            SceneManager.LoadScene("Main");
            for (int i = 0; i < 30; i++) yield return null;
            pad = InputSystem.AddDevice<Gamepad>("VirtualPad");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (pad != null) InputSystem.RemoveDevice(pad);
            Time.timeScale = 1f;
            yield return null;
        }

        IEnumerator Press(GamepadButton b)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(b));
            for (int i = 0; i < 3; i++) yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            for (int i = 0; i < 3; i++) yield return null;
        }

        static string Selected() => EventSystem.current && EventSystem.current.currentSelectedGameObject
            ? EventSystem.current.currentSelectedGameObject.name : "(none)";

        [UnityTest]
        public IEnumerator Menu_IsFullyUsableWithAGamepad()
        {
            var menu = Object.FindAnyObjectByType<SettingsMenu>();
            Assert.IsNotNull(menu);
            Assert.IsFalse(menu.IsOpen);

            yield return Press(GamepadButton.Start);
            Assert.IsTrue(menu.IsOpen, "Start opens the menu");
            Assert.AreEqual(0f, Time.timeScale, "simulation pauses");
            Assert.AreEqual("Button_Match", Selected());

            // Down to the Driving tab (Match, Robot, Mechanisms, Driving), A to enter it.
            for (int i = 0; i < 3; i++) yield return Press(GamepadButton.DpadDown);
            Assert.AreEqual("Button_Driving", Selected());
            yield return Press(GamepadButton.South);
            Assert.AreEqual("Row_Field-centric drive", Selected());

            bool before = SettingsStore.Current.assists.fieldCentric;
            yield return Press(GamepadButton.South);
            Assert.AreNotEqual(before, SettingsStore.Current.assists.fieldCentric, "A toggles a setting");

            // Down to "Tank-drive control" and change it with D-pad Right.
            for (int i = 0; i < 4; i++) yield return Press(GamepadButton.DpadDown);
            Assert.AreEqual("Row_Tank-drive control", Selected());
            var mode = SettingsStore.Current.assists.controlMode;
            yield return Press(GamepadButton.DpadRight);
            Assert.AreNotEqual(mode, SettingsStore.Current.assists.controlMode, "D-pad Right changes a value");

            // B back to the tabs, up to Robot, into it, and type on the on-screen keyboard.
            yield return Press(GamepadButton.East);
            Assert.AreEqual("Button_Driving", Selected());
            for (int i = 0; i < 2; i++) yield return Press(GamepadButton.DpadUp);
            Assert.AreEqual("Button_Robot", Selected());
            yield return Press(GamepadButton.South);
            for (int i = 0; i < 2; i++) yield return Press(GamepadButton.DpadDown);
            StringAssert.StartsWith("Button_Robot name", Selected());
            string name = SettingsStore.Current.robot.name;
            yield return Press(GamepadButton.South);                    // open keyboard
            Assert.AreEqual("Button_1", Selected());
            yield return Press(GamepadButton.South);                    // type "1"
            yield return Press(GamepadButton.DpadRight);
            yield return Press(GamepadButton.South);                    // type "2"
            yield return Press(GamepadButton.East);                     // done
            Assert.AreEqual(name + "12", SettingsStore.Current.robot.name);

            yield return Press(GamepadButton.Start);
            Assert.IsFalse(menu.IsOpen, "Start closes the menu");
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator FreeView_SticksFlyTheCameraInsteadOfDriving()
        {
            SettingsStore.Current.camera.view = CameraView.Free;
            SettingsStore.Commit();
            yield return null;
            var origin = VR.ViewManager.Instance.Origin.transform;
            var robot = Match.MatchController.Instance.Player;
            Vector3 camStart = origin.position, robotStart = robot.transform.position;
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(0f, 1f) });
            for (int i = 0; i < 40; i++) yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            Assert.Greater(Vector3.Distance(camStart, origin.position), 0.2f, "left stick flies the camera");
            Assert.Less(Vector3.Distance(robotStart, robot.transform.position), 0.05f, "robot holds still");
        }
    }
}
