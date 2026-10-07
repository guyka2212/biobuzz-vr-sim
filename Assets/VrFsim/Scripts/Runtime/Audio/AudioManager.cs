using System.Collections;
using UnityEngine;
using VrFsim.Game;
using VrFsim.Match;
using VrFsim.Robot;
using VrFsim.Settings;

namespace VrFsim.Audio
{
    /// <summary>
    /// Field cues (Table 9-1) and robot sounds, all synthesised at start-up so the build carries no
    /// audio files. Field cues play flat (they come from the arena PA); robot sounds are 3D.
    /// The manual lists the 1:00 FLOWER cue as "[TBD]"; a two-note chime stands in for it.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        const int Rate = 44100;
        AudioClip buzzer, longBuzzer, bell, whistle, charge, beep, thud, thunk, click, alert, chime, motor, pop;
        AudioSource fieldSource, motorSource;
        RobotController player;

        void Awake()
        {
            buzzer = Make("Buzzer", 0.45f, t => Square(t, 150f) * 0.5f + Square(t, 225f) * 0.3f, 0.01f, 0.05f);
            longBuzzer = Make("Buzzer3s", 3f, t => Square(t, 150f) * 0.5f + Square(t, 225f) * 0.3f, 0.01f, 0.2f);
            bell = Make("Bell", 1.4f, t => (Sin(t, 880f) + 0.6f * Sin(t, 1320f) + 0.3f * Sin(t, 2210f)) * Mathf.Exp(-t * 3f) * 0.5f, 0.002f, 0.1f);
            whistle = Make("Whistle", 1.6f, t => (Sin(t, 440f + Mathf.Sin(t * 30f) * 4f) + Sin(t, 554f) + Sin(t, 659f)) * 0.25f, 0.08f, 0.3f);
            charge = Make("Charge", 1.6f, Charge, 0.005f, 0.1f);
            beep = Make("Beep", 0.15f, t => Sin(t, 1000f) * 0.5f, 0.005f, 0.03f);
            thud = Make("Thud", 0.5f, t => (Sin(t, 70f - t * 40f) + Noise() * 0.3f) * Mathf.Exp(-t * 9f), 0.002f, 0.05f);
            thunk = Make("Thunk", 0.18f, t => (Sin(t, 220f - t * 600f) * 0.7f + Noise() * 0.4f) * Mathf.Exp(-t * 25f), 0.001f, 0.02f);
            click = Make("Click", 0.06f, t => Noise() * Mathf.Exp(-t * 80f) * 0.6f, 0.001f, 0.01f);
            alert = Make("Alert", 0.5f, t => Square(t, t < 0.25f ? 660f : 440f) * 0.3f, 0.005f, 0.05f);
            chime = Make("Chime", 1.0f, t => Sin(t, t < 0.35f ? 784f : 1046f) * Mathf.Exp(-(t % 0.35f) * 4f) * 0.5f, 0.003f, 0.1f);
            pop = Make("Pop", 0.12f, t => Sin(t, 500f - t * 2000f) * Mathf.Exp(-t * 30f) * 0.6f, 0.001f, 0.02f);
            motor = Make("Motor", 1f, t => (Saw(t, 110f) * 0.5f + Saw(t, 220f) * 0.25f + Noise() * 0.1f) * 0.5f, 0f, 0f);

            fieldSource = gameObject.AddComponent<AudioSource>();
            fieldSource.spatialBlend = 0f;
            fieldSource.playOnAwake = false;
        }

        void Start()
        {
            var m = MatchController.Instance;
            if (!m) return;
            m.Cue += OnCue;
            m.PlayerSpawned += AttachRobot;
            if (m.Player) AttachRobot(m.Player);
        }

        static SoundSettings S => SettingsStore.Current.audio;

        void OnCue(MatchCue cue)
        {
            float field = S.master * S.fieldCues;
            switch (cue)
            {
                case MatchCue.AutoStart: StartCoroutine(Countdown(field)); break;
                case MatchCue.AutoEnd: StartCoroutine(Repeat(buzzer, 3, 0.55f, field)); break;
                case MatchCue.TransitionCountdown: if (S.voiceCues) StartCoroutine(Repeat(beep, 3, 1f, S.master * S.voice)); break;
                case MatchCue.TeleopStart: StartCoroutine(Repeat(bell, 3, 0.45f, field)); break;
                case MatchCue.FlowersOpen: fieldSource.PlayOneShot(chime, field); break;
                case MatchCue.Final20: fieldSource.PlayOneShot(whistle, field); break;
                case MatchCue.MatchEnd: fieldSource.PlayOneShot(longBuzzer, field); break;
                case MatchCue.Tip: fieldSource.PlayOneShot(thud, S.master * S.hive); break;
                case MatchCue.Foul: fieldSource.PlayOneShot(alert, S.master * S.alerts); break;
                case MatchCue.HumanNectar: fieldSource.PlayOneShot(pop, S.master * S.alerts * 0.6f); break;
            }
        }

        IEnumerator Countdown(float vol)
        {
            fieldSource.PlayOneShot(charge, vol);
            yield break;
        }

        IEnumerator Repeat(AudioClip c, int n, float gap, float vol)
        {
            for (int i = 0; i < n; i++) { fieldSource.PlayOneShot(c, vol); yield return new WaitForSeconds(gap); }
        }

        void AttachRobot(RobotController r)
        {
            player = r;
            if (!r) return;
            motorSource = r.gameObject.AddComponent<AudioSource>();
            motorSource.clip = motor;
            motorSource.loop = true;
            motorSource.spatialBlend = 1f;
            motorSource.minDistance = 0.5f;
            motorSource.maxDistance = 12f;
            motorSource.volume = 0f;
            motorSource.Play();
            r.Launched += (_, e) => PlayAt(thunk, e.Position, S.launch);
            r.Intaken += (_, e) => PlayAt(click, e.transform.position, S.intake);
            r.Placed += (_, e) => PlayAt(click, e.Position, S.intake);
        }

        void PlayAt(AudioClip c, Vector3 pos, float cat) => AudioSource.PlayClipAtPoint(c, pos, S.master * cat);

        void Update()
        {
            if (!player || !motorSource) return;
            float speed = player.Velocity.magnitude;
            float spin = Mathf.Abs(player.Rig.body.angularVelocity.y) * 0.15f;
            float load = Mathf.Clamp01((speed + spin) / 2f);
            motorSource.pitch = 0.6f + load * 1.2f;
            motorSource.volume = S.master * S.robot * (0.05f + load * 0.35f);
        }

        // ── Synthesis ───────────────────────────────────────────────────────────────────────

        delegate float Wave(float t);

        static AudioClip Make(string name, float seconds, Wave f, float attack, float release)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float env = 1f;
                if (attack > 0f && t < attack) env = t / attack;
                if (release > 0f && t > seconds - release) env *= Mathf.Clamp01((seconds - t) / release);
                data[i] = Mathf.Clamp(f(t) * env, -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Sin(float t, float hz) => Mathf.Sin(2f * Mathf.PI * hz * t);
        static float Square(float t, float hz) => Mathf.Sign(Mathf.Sin(2f * Mathf.PI * hz * t));
        static float Saw(float t, float hz) => 2f * (t * hz - Mathf.Floor(t * hz + 0.5f));
        static readonly System.Random rng = new System.Random(7);
        static float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);

        /// <summary>A short bugle call in the spirit of the "Cavalry Charge" start cue.</summary>
        static float Charge(float t)
        {
            float[] notes = { 392f, 523f, 659f, 784f, 659f, 784f };
            float[] lens = { 0.2f, 0.2f, 0.2f, 0.35f, 0.15f, 0.5f };
            float acc = 0f;
            for (int i = 0; i < notes.Length; i++)
            {
                if (t < acc + lens[i])
                {
                    float lt = t - acc;
                    float env = Mathf.Min(1f, lt / 0.02f) * Mathf.Min(1f, (lens[i] - lt) / 0.03f);
                    return (Sin(t, notes[i]) * 0.5f + Sin(t, notes[i] * 2f) * 0.2f + Sin(t, notes[i] * 3f) * 0.1f) * env * 0.6f;
                }
                acc += lens[i];
            }
            return 0f;
        }
    }
}
