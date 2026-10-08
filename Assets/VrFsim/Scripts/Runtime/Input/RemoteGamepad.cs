using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace VrFsim.Input
{
    /// <summary>
    /// Receives a gamepad from VrFsim Controller Connect (a Windows app for laptops too weak for
    /// Quest Link): the controller is plugged into the laptop and its state arrives over the
    /// headset's USB cable (adb port forwarding to this loopback port). Each packet becomes the
    /// state of a virtual <see cref="Gamepad"/>, so every gamepad binding and the menu just work.
    ///
    /// Protocol (little-endian TCP): the client sends the 4-byte hello "VFG1", the game answers
    /// "OK" (so Controller Connect knows the game itself is listening, not just adb), then 16-byte
    /// packets: 'S', reserved, buttons (uint16, bit i = <see cref="GamepadButton"/> i, 0..13),
    /// left x/y, right x/y (int16, -32767..32767, y up), left/right trigger (uint8), 2 reserved.
    /// Shared with github.com/guyka2212/controller-connect (PROTOCOL.md).
    /// </summary>
    public class RemoteGamepad : MonoBehaviour
    {
        public const int Port = 47812;
        public const string Hello = "VFG1";
        public const int PacketSize = 16;
        static readonly byte[] Ack = { (byte)'O', (byte)'K' };

        public static RemoteGamepad Instance { get; private set; }
        /// <summary>True while Controller Connect is connected and sending.</summary>
        public bool Connected => connected;

        TcpListener listener;
        Thread thread;
        volatile bool running, connected, dirty;
        readonly object gate = new object();
        GamepadState latest;
        Gamepad device;

        /// <summary>Started on the standalone Quest build only (on a PC the controller is local).</summary>
        public static bool ShouldRun => Application.platform == RuntimePlatform.Android;

        void Awake()
        {
            Instance = this;
            running = true;
            thread = new Thread(Serve) { IsBackground = true, Name = "VrFsim remote gamepad" };
            thread.Start();
        }

        void OnDestroy()
        {
            running = false;
            try { listener?.Stop(); } catch { }
            if (device != null) InputSystem.RemoveDevice(device);
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (connected && device == null)
                device = InputSystem.AddDevice<Gamepad>("VrFsim Remote Gamepad");
            if (!connected && device != null)
            {
                InputSystem.RemoveDevice(device);
                device = null;
            }
            if (device != null && dirty)
            {
                GamepadState s;
                lock (gate) { s = latest; dirty = false; }
                InputSystem.QueueStateEvent(device, s);
            }
        }

        void Serve()
        {
            try
            {
                // Loopback only: adb forwards the USB connection here; nothing on the network can connect.
                listener = new TcpListener(IPAddress.Loopback, Port);
                listener.Start();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VrFsim] Remote gamepad port {Port} unavailable: {e.Message}");
                return;
            }
            var buffer = new byte[PacketSize];
            while (running)
            {
                TcpClient client = null;
                try
                {
                    client = listener.AcceptTcpClient();
                    client.NoDelay = true;
                    client.ReceiveTimeout = 3000;   // Controller Connect sends at least every 100 ms
                    var stream = client.GetStream();
                    if (!ReadExactly(stream, buffer, 4) || System.Text.Encoding.ASCII.GetString(buffer, 0, 4) != Hello)
                        continue;
                    stream.Write(Ack, 0, Ack.Length);
                    connected = true;
                    while (running && ReadExactly(stream, buffer, PacketSize))
                    {
                        if (buffer[0] != (byte)'S') break;
                        var s = Decode(buffer);
                        lock (gate) { latest = s; dirty = true; }
                    }
                }
                catch (Exception) { /* disconnected or timed out: wait for the next connection */ }
                finally
                {
                    connected = false;
                    lock (gate) { latest = default; dirty = true; }
                    client?.Close();
                }
            }
        }

        static bool ReadExactly(Stream s, byte[] buf, int count)
        {
            int read = 0;
            while (read < count)
            {
                int n = s.Read(buf, read, count - read);
                if (n <= 0) return false;
                read += n;
            }
            return true;
        }

        /// <summary>Packet bytes to Input System gamepad state.</summary>
        public static GamepadState Decode(byte[] b)
        {
            float Axis(int i) => Mathf.Clamp(BitConverter.ToInt16(b, i) / 32767f, -1f, 1f);
            return new GamepadState
            {
                buttons = (uint)(BitConverter.ToUInt16(b, 2) & 0x3FFF),
                leftStick = new Vector2(Axis(4), Axis(6)),
                rightStick = new Vector2(Axis(8), Axis(10)),
                leftTrigger = b[12] / 255f,
                rightTrigger = b[13] / 255f,
            };
        }

        /// <summary>Gamepad state to packet bytes (used by tests; Controller Connect has its own copy).</summary>
        public static byte[] Encode(ushort buttons, Vector2 left, Vector2 right, float lt, float rt)
        {
            var b = new byte[PacketSize];
            b[0] = (byte)'S';
            BitConverter.GetBytes(buttons).CopyTo(b, 2);
            void Put(int i, float v) => BitConverter.GetBytes((short)Mathf.RoundToInt(Mathf.Clamp(v, -1f, 1f) * 32767f)).CopyTo(b, i);
            Put(4, left.x); Put(6, left.y); Put(8, right.x); Put(10, right.y);
            b[12] = (byte)Mathf.RoundToInt(Mathf.Clamp01(lt) * 255f);
            b[13] = (byte)Mathf.RoundToInt(Mathf.Clamp01(rt) * 255f);
            return b;
        }
    }
}
