using System.Collections;
using System.Linq;
using System.Net.Sockets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using VrFsim.Input;

namespace VrFsim.Tests
{
    /// <summary>Controller Connect's controller stream becomes a gamepad inside the game.</summary>
    public class RemoteGamepadTests
    {
        static Gamepad Remote() => InputSystem.devices.OfType<Gamepad>().FirstOrDefault(d => d.name.StartsWith("VrFsim Remote Gamepad"));

        [UnityTest]
        public IEnumerator ControllerConnectPacket_DrivesAVirtualGamepad()
        {
            var go = new GameObject("Remote Gamepad");
            var remote = go.AddComponent<RemoteGamepad>();
            yield return null;

            using (var client = new TcpClient())
            {
                client.NoDelay = true;
                client.Connect("127.0.0.1", RemoteGamepad.Port);
                var stream = client.GetStream();
                stream.Write(System.Text.Encoding.ASCII.GetBytes(RemoteGamepad.Hello), 0, 4);
                var ack = new byte[2];
                stream.ReadTimeout = 3000;
                int got = 0;
                while (got < 2) { int n = stream.Read(ack, got, 2 - got); if (n <= 0) break; got += n; }
                Assert.AreEqual("OK", System.Text.Encoding.ASCII.GetString(ack), "game acknowledges Controller Connect");
                ushort buttons = (ushort)((1 << (int)GamepadButton.South) | (1 << (int)GamepadButton.Start) | (1 << (int)GamepadButton.DpadUp));
                var packet = RemoteGamepad.Encode(buttons, new Vector2(0.5f, -1f), new Vector2(0f, 0.25f), 0f, 1f);
                stream.Write(packet, 0, packet.Length);

                Gamepad pad = null;
                for (int i = 0; i < 120 && (pad == null || !pad.buttonSouth.isPressed); i++)
                {
                    yield return null;
                    pad = Remote();
                }
                Assert.IsTrue(remote.Connected, "Controller Connect connection accepted");
                Assert.IsNotNull(pad, "virtual gamepad added");
                Assert.IsTrue(pad.buttonSouth.isPressed, "A");
                Assert.IsTrue(pad.startButton.isPressed, "Start");
                Assert.IsTrue(pad.dpad.up.isPressed, "D-pad up");
                Assert.IsFalse(pad.buttonEast.isPressed, "B not pressed");
                Assert.AreEqual(1f, pad.rightTrigger.ReadValue(), 0.01f);
                Assert.AreEqual(-1f, pad.leftStick.ReadValue().y, 0.02f);
                Assert.Greater(pad.leftStick.ReadValue().x, 0.3f);
            }

            // Disconnect removes the device again.
            for (int i = 0; i < 120 && Remote() != null; i++) yield return null;
            Assert.IsNull(Remote(), "virtual gamepad removed after Controller Connect disconnects");
            Object.Destroy(go);
            yield return null;
        }

        [Test]
        public void Packet_RoundTrips()
        {
            var b = RemoteGamepad.Encode(0x3FFF, new Vector2(-1f, 1f), new Vector2(0.5f, -0.5f), 0.5f, 0f);
            Assert.AreEqual(RemoteGamepad.PacketSize, b.Length);
            var s = RemoteGamepad.Decode(b);
            Assert.AreEqual(0x3FFFu, s.buttons);
            Assert.AreEqual(-1f, s.leftStick.x, 1e-3f);
            Assert.AreEqual(0.5f, s.rightStick.x, 1e-3f);
            Assert.AreEqual(0.5f, s.leftTrigger, 0.01f);
        }
    }
}
