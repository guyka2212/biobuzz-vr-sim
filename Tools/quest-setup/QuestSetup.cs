// VrFsim Quest Setup: installs (or updates) VrFsim on a Meta Quest connected to this PC by USB, so
// the game runs standalone on the headset. The Quest build (VrFsim-Quest.apk) is embedded.
// Built by Tools/make-quest-setup.sh with the C# compiler that ships with Windows (.NET Framework 4),
// C# 5 only. Talking to the headset uses Google's adb (see Adb.cs).
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace VrFsimQuestSetup
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;   // TLS 1.2 for the adb download
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm());
        }

        public static Image LoadImage(string resource)
        {
            var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource);
            return s == null ? null : Image.FromStream(s);
        }
    }

    class SetupForm : Form
    {
        static readonly Color Dark = Color.FromArgb(0x14, 0x16, 0x1A);
        static readonly Color Accent = Color.FromArgb(0xFF, 0xB4, 0x00);
        static readonly Color Good = Color.FromArgb(0x2E, 0x8B, 0x3E);
        static readonly Color Warn = Color.FromArgb(0xB3, 0x6B, 0x00);
        const string Title = "VrFsim Quest Setup";
        const string Activity = "/com.unity3d.player.UnityPlayerActivity";

        readonly Label headsetStatus, gameStatus;
        readonly Button getAdb, install, start, close;
        readonly ProgressBar bar;
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        volatile bool busy, polling;
        volatile string serial, installedVersion, notReady = "Looking for the headset...";
        string headsetName;
        bool versionChecked;

        public SetupForm()
        {
            Text = Title + " " + BuildInfo.Version;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96f, 96f);
            ClientSize = new Size(580, 470);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.5f);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            var header = new Panel { BackColor = Dark, Dock = DockStyle.Top, Height = 130 };
            header.Controls.Add(new PictureBox { Image = Program.LoadImage("logo.png"), SizeMode = PictureBoxSizeMode.Zoom, Bounds = new Rectangle(90, 14, 400, 102) });
            Controls.Add(header);

            Controls.Add(new Label
            {
                Text = "Installs VrFsim " + BuildInfo.Version + " on your Meta Quest (3, 3S, Pro, 2) so it runs on the headset "
                     + "by itself, without a PC. You need developer mode on and a USB-C cable to this PC.",
                Bounds = new Rectangle(18, 140, 544, 40),
            });

            Controls.Add(Heading("1.  Connect the headset", 190));
            headsetStatus = Status(214);
            getAdb = new Button { Text = "Download Android tools from Google (one time, ~7 MB)", Bounds = new Rectangle(18, 240, 360, 30), Visible = false };
            getAdb.Click += delegate { DownloadAdb(); };
            Controls.Add(getAdb);

            Controls.Add(Heading("2.  Install the game", 282));
            gameStatus = Status(306);
            bar = new ProgressBar { Bounds = new Rectangle(18, 334, 544, 16), Style = ProgressBarStyle.Continuous };
            Controls.Add(bar);

            install = new Button { Text = "Install VrFsim on the headset", Bounds = new Rectangle(18, 362, 260, 34), BackColor = Accent, FlatStyle = FlatStyle.Flat };
            install.FlatAppearance.BorderSize = 0;
            install.Click += delegate { InstallGame(); };
            start = new Button { Text = "Start VrFsim on the headset", Bounds = new Rectangle(288, 362, 220, 34) };
            start.Click += delegate { StartGame(); };
            Controls.Add(install);
            Controls.Add(start);

            Controls.Add(new Label
            {
                Text = "Then in the headset: Library → Unknown Sources → VrFsim. Use the Quest controllers "
                     + "or a gamepad paired by Bluetooth.",
                Bounds = new Rectangle(18, 406, 450, 44), ForeColor = Color.DimGray,
            });
            close = new Button { Text = "Close", Bounds = new Rectangle(476, 418, 86, 30) };
            close.Click += delegate { Close(); };
            Controls.Add(close);
            ActiveControl = install;

            timer.Interval = 1000;
            timer.Tick += delegate { if (!polling && !busy) { polling = true; ThreadPool.QueueUserWorkItem(delegate { Poll(); }); } };
            timer.Start();
        }

        Label Heading(string text, int y)
        {
            return new Label { Text = text, Bounds = new Rectangle(18, y, 544, 22), Font = new Font("Segoe UI Semibold", 10f) };
        }

        Label Status(int y)
        {
            var l = new Label { Bounds = new Rectangle(18, y, 544, 22) };
            Controls.Add(l);
            return l;
        }

        static void Set(Label l, string text, Color c) { l.Text = text; l.ForeColor = c; }

        void Ui(MethodInvoker a)
        {
            try { if (!IsDisposed) BeginInvoke(a); } catch (InvalidOperationException) { }
        }

        void Poll()
        {
            try
            {
                if (Adb.Path == null && !Adb.Locate())
                {
                    notReady = "First click \"Download Android tools from Google\" (step 1). They are needed to talk to the headset.";
                    Ui(delegate { Set(headsetStatus, "Android tools (adb) are needed to talk to the headset.", Warn); getAdb.Visible = true; });
                    return;
                }
                var devices = Adb.Headsets();
                string ready = null, state = null;
                foreach (var d in devices) { state = d.Value; if (d.Value == "device") { ready = d.Key; break; } }
                if (ready == null)
                {
                    serial = null; versionChecked = false;
                    string text = state == "unauthorized"
                        ? "Put the headset on and press Allow on \"Allow USB debugging\" (tick Always allow)."
                        : devices.Count > 0 ? "The headset is connecting..." : "No Quest found. Connect it with a USB-C cable and turn on developer mode.";
                    notReady = text;
                    Ui(delegate { Set(headsetStatus, text, Warn); getAdb.Visible = false; Set(gameStatus, "", Color.Black); });
                    return;
                }
                if (ready != serial)
                {
                    serial = ready;
                    headsetName = Adb.Shell(ready, "getprop ro.product.model");
                    versionChecked = false;
                }
                if (!versionChecked)
                {
                    installedVersion = Adb.InstalledVersion(ready);
                    versionChecked = true;
                }
                notReady = null;
                string name = string.IsNullOrEmpty(headsetName) ? "Quest" : headsetName;
                string v = installedVersion;
                Ui(delegate
                {
                    Set(headsetStatus, "Connected: " + name + ".", Good);
                    getAdb.Visible = false;
                    if (v == null) { Set(gameStatus, "VrFsim is not on the headset yet.", Warn); install.Text = "Install VrFsim on the headset"; }
                    else if (v != BuildInfo.Version) { Set(gameStatus, "The headset has VrFsim " + v + "; this setup installs " + BuildInfo.Version + ".", Warn); install.Text = "Update VrFsim on the headset"; }
                    else { Set(gameStatus, "VrFsim " + v + " is installed on the headset.", Good); install.Text = "Reinstall VrFsim"; }
                });
            }
            catch (Exception e) { Ui(delegate { Set(headsetStatus, "Headset check failed: " + e.Message, Warn); }); }
            finally { polling = false; }
        }

        bool HeadsetReady()
        {
            string why = notReady;
            if (serial != null && why == null) return true;
            MessageBox.Show(this, why ?? "Looking for the headset... connect it with a USB-C cable.", Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        void DownloadAdb()
        {
            var answer = MessageBox.Show(this,
                "Download Android SDK Platform-Tools (adb) from Google?\n\nThey are saved for VrFsim's tools only, in "
                + "%LOCALAPPDATA%\\VrFsim. By downloading you accept Google's Android SDK terms:\n"
                + "https://developer.android.com/studio/terms",
                Title, MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (answer != DialogResult.OK) return;
            RunBusy(delegate { Adb.Download(delegate (string t) { Ui(delegate { Set(headsetStatus, t, Warn); }); }); }, null);
        }

        void InstallGame()
        {
            if (!HeadsetReady()) return;
            string s = serial;
            RunBusy(delegate
            {
                Ui(delegate { Set(gameStatus, "Copying VrFsim to the headset (about a minute)...", Warn); bar.Style = ProgressBarStyle.Marquee; });
                string apk = Path.Combine(Path.GetTempPath(), "VrFsim-Quest.apk");
                using (var res = Assembly.GetExecutingAssembly().GetManifestResourceStream("VrFsim-Quest.apk"))
                {
                    if (res == null) throw new InvalidOperationException("This setup is damaged (no game inside).");
                    using (var file = File.Create(apk)) res.CopyTo(file);
                }
                string result = Adb.Run("-s " + s + " install -r -g \"" + apk + "\"", 300000);
                try { File.Delete(apk); } catch { }
                if (!result.Contains("Success"))
                {
                    // A different signing key (e.g. a build from another PC) blocks an update: remove and retry once.
                    if (result.Contains("INSTALL_FAILED_UPDATE_INCOMPATIBLE"))
                        throw new InvalidOperationException("The VrFsim already on the headset was installed from a different build. "
                            + "Uninstall it in the headset (Library → Unknown Sources → VrFsim → Uninstall), then install again.");
                    throw new InvalidOperationException("Install failed:\n" + result.Trim());
                }
                versionChecked = false;
                Adb.Shell(s, "am start -n " + Adb.Package + Activity);
            }, "VrFsim is installed and started. Put the headset on!");
        }

        void StartGame()
        {
            if (!HeadsetReady()) return;
            if (installedVersion == null)
            {
                MessageBox.Show(this, "VrFsim is not on the headset yet. Click \"Install VrFsim on the headset\" first.", Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string s = serial;
            RunBusy(delegate { Adb.Shell(s, "am start -n " + Adb.Package + Activity); }, "VrFsim started. Put the headset on.");
        }

        void RunBusy(MethodInvoker work, string done)
        {
            busy = true;
            install.Enabled = start.Enabled = getAdb.Enabled = close.Enabled = false;
            ThreadPool.QueueUserWorkItem(delegate
            {
                string error = null;
                try { work(); } catch (Exception e) { error = e.Message; }
                busy = false;
                Ui(delegate
                {
                    bar.Style = ProgressBarStyle.Continuous;
                    bar.Value = error == null && done != null ? 100 : 0;
                    install.Enabled = start.Enabled = getAdb.Enabled = close.Enabled = true;
                    if (error != null) MessageBox.Show(this, error, Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    else if (done != null) Set(gameStatus, done, Good);
                });
            });
        }
    }
}
