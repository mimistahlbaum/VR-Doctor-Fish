using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace DoctorFish
{
    /// <summary>
    /// One line of a VibraForge command file:
    /// {"time": 2.0, "addr": 1, "mode": 1, "duty": 5, "freq": 6}
    /// </summary>
    [Serializable]
    public class HapticCommand
    {
        public float time;
        public int addr;
        public int mode;
        public int duty;
        public int freq;
    }

    /// <summary>A parsed pattern file: an ascending list of timed commands.</summary>
    public class HapticPattern
    {
        public string Name;
        public List<HapticCommand> Commands = new List<HapticCommand>();
        public float Duration;

        public static HapticPattern Parse(string name, string text)
        {
            var pattern = new HapticPattern { Name = name };
            foreach (var rawLine in text.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0)
                    continue;
                var command = JsonUtility.FromJson<HapticCommand>(line);
                if (command != null)
                    pattern.Commands.Add(command);
            }
            if (pattern.Commands.Count > 0)
                pattern.Duration = pattern.Commands[pattern.Commands.Count - 1].time;
            return pattern;
        }
    }

    /// <summary>
    /// Plays the VibraForge pattern files from StreamingAssets/haptics through
    /// the VibraForge sender (Unity -> TCP -> Python server -> BLE -> ESP32 ->
    /// UART -> vibration units).
    ///
    /// One-shot patterns are authored for the left leg (even addresses); pass
    /// HapticLeg.Right or HapticLeg.Both to mirror them at address + 1.
    /// </summary>
    public class HapticController : MonoBehaviour
    {
        static readonly string[] PatternNames =
        {
            "welcome_experience",
            "idle_water",
            "small_fish_nibble",
            "big_fish_bite",
            "jellyfish_sting"
        };

        public VibraForge sender;

        /// <summary>
        /// Raised for every command (addr, mode, duty), including when no
        /// hardware is connected, so visuals can mirror at the actuator's
        /// position exactly what each vibration unit is doing.
        /// </summary>
        public event Action<int, int, int> CommandSent;

        readonly Dictionary<string, HapticPattern> patterns =
            new Dictionary<string, HapticPattern>();
        readonly HashSet<int> activeUnits = new HashSet<int>();
        readonly List<Coroutine> running = new List<Coroutine>();

        bool hardwareWarningShown;

        public bool IsReady { get; private set; }

        void Start()
        {
            if (sender == null)
                sender = GetComponent<VibraForge>();
            StartCoroutine(LoadPatterns());
        }

        IEnumerator LoadPatterns()
        {
            foreach (var name in PatternNames)
            {
                var path = Path.Combine(Application.streamingAssetsPath,
                    "haptics", name + ".json");
                string text = null;
                if (path.Contains("://"))
                {
                    // Android: StreamingAssets live inside the APK.
                    using (var request = UnityWebRequest.Get(path))
                    {
                        yield return request.SendWebRequest();
                        if (request.result == UnityWebRequest.Result.Success)
                            text = request.downloadHandler.text;
                    }
                }
                else if (File.Exists(path))
                {
                    text = File.ReadAllText(path);
                }

                if (string.IsNullOrEmpty(text))
                {
                    Debug.LogWarning($"HapticController: pattern file missing: {path}");
                    continue;
                }
                patterns[name] = HapticPattern.Parse(name, text);
            }
            IsReady = true;
            Debug.Log($"HapticController: loaded {patterns.Count} patterns");
        }

        /// <summary>Play a pattern once.</summary>
        public void PlayOneShot(string name, HapticLeg leg = HapticLeg.Left)
        {
            Play(name, false, leg);
        }

        /// <summary>Replay a pattern until StopAll is called.</summary>
        public void PlayLoop(string name, HapticLeg leg = HapticLeg.Left)
        {
            Play(name, true, leg);
        }

        void Play(string name, bool loop, HapticLeg leg)
        {
            if (!patterns.TryGetValue(name, out var pattern))
            {
                Debug.LogWarning($"HapticController: unknown pattern '{name}'");
                return;
            }
            running.Add(StartCoroutine(PlayRoutine(pattern, loop, leg)));
        }

        /// <summary>Stop every running pattern and silence every unit.</summary>
        public void StopAll()
        {
            foreach (var routine in running)
                if (routine != null)
                    StopCoroutine(routine);
            running.Clear();
            SilenceActiveUnits();
        }

        IEnumerator PlayRoutine(HapticPattern pattern, bool loop, HapticLeg leg)
        {
            do
            {
                var start = Time.time;
                foreach (var command in pattern.Commands)
                {
                    while (Time.time - start < command.time)
                        yield return null;
                    if (leg == HapticLeg.Left || leg == HapticLeg.Both)
                        Send(command.addr, command.mode, command.duty, command.freq);
                    if (leg == HapticLeg.Right || leg == HapticLeg.Both)
                        Send(command.addr + 1, command.mode, command.duty, command.freq);
                }
                // Small breath between loops so replayed files stay in spec.
                if (loop)
                    yield return new WaitForSeconds(0.1f);
            } while (loop);
        }

        void Send(int addr, int mode, int duty, int freq)
        {
            if (mode == 1)
                activeUnits.Add(addr);
            else
                activeUnits.Remove(addr);

            CommandSent?.Invoke(addr, mode, duty);

            if (sender == null)
                return;
            var tcp = sender.GetComponent<TcpSender>();
            if (tcp != null && !tcp.isConnected)
            {
                if (!hardwareWarningShown)
                {
                    Debug.LogWarning(
                        "HapticController: VibraForge TCP server not connected. " +
                        "Haptic commands are skipped (visuals and audio still run). " +
                        "Start the VibraForge Python server and restart the scene.");
                    hardwareWarningShown = true;
                }
                return;
            }
            try
            {
                sender.SendCommand(addr, mode, duty, freq);
            }
            catch (Exception e)
            {
                if (!hardwareWarningShown)
                {
                    Debug.LogWarning($"HapticController: send failed ({e.Message})");
                    hardwareWarningShown = true;
                }
            }
        }

        void SilenceActiveUnits()
        {
            if (activeUnits.Count == 0)
                return;
            var units = new List<int>(activeUnits);
            foreach (var addr in units)
                Send(addr, 0, 0, 0);
            activeUnits.Clear();
        }

        void OnDisable()
        {
            SilenceActiveUnits();
        }

        void OnApplicationQuit()
        {
            SilenceActiveUnits();
        }
    }
}
