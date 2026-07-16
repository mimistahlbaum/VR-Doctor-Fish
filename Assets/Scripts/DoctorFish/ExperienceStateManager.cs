using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace DoctorFish
{
    /// <summary>
    /// Steps through the staged sequence
    /// Welcome -> Small fish -> Big fish -> Jellyfish -> Calm -> Finished
    /// and keeps the visual, audio and haptic controllers in sync.
    ///
    /// Debug keys (host keyboard): N skips to the next stage, R restarts
    /// the whole session.
    /// </summary>
    public class ExperienceStateManager : MonoBehaviour
    {
        [Header("Stage durations (seconds)")]
        public float welcomeSeconds = 12f;
        public float smallFishSeconds = 45f;
        public float bigFishSeconds = 22f;
        public float jellyfishSeconds = 22f;
        public float calmSeconds = 20f;

        [Header("Controllers (wired by the bootstrap)")]
        public VisualController visual;
        public AudioController audioController;
        public HapticController haptics;

        public ExperienceStage CurrentStage { get; private set; }
            = ExperienceStage.Welcome;
        public event Action<ExperienceStage> StageChanged;

        bool skipRequested;
        bool started;

        void Start()
        {
            if (visual != null)
            {
                visual.FishNibbled += OnFishNibbled;
                visual.BigFishBit += OnBigFishBit;
                visual.JellyfishStung += OnJellyfishStung;
            }
            StartCoroutine(RunExperience());
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !started)
                return;
            if (keyboard.nKey.wasPressedThisFrame)
                skipRequested = true;
            if (keyboard.rKey.wasPressedThisFrame)
                Restart();
        }

        public void Restart()
        {
            haptics?.StopAll();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        IEnumerator RunExperience()
        {
            // Let every component finish Start (TCP connect, pattern load)
            // before the first stage fires.
            yield return null;
            var waited = 0f;
            while (haptics != null && !haptics.IsReady && waited < 3f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            started = true;

            // 1. Welcome: feet enter the water, a wave of vibration rolls
            // down both legs, music begins.
            EnterStage(ExperienceStage.Welcome);
            audioController?.StartMusic(2f);
            audioController?.PlayWaterPop();
            haptics?.PlayOneShot("welcome_experience", HapticLeg.Both);
            yield return StageTimer(welcomeSeconds);

            // 2. Small fish: ticklish nibbling at random points.
            EnterStage(ExperienceStage.SmallFish);
            haptics?.StopAll();
            haptics?.PlayLoop("small_fish_nibble");
            yield return StageTimer(smallFishSeconds);

            // 3. Big fish: ambient water, then strong one-shot bites driven
            // by the fish choreography (see OnBigFishBit).
            EnterStage(ExperienceStage.BigFish);
            haptics?.StopAll();
            haptics?.PlayLoop("idle_water");
            yield return StageTimer(bigFishSeconds);

            // 4. Jellyfish: sharp electric stings driven by contact events.
            EnterStage(ExperienceStage.Jellyfish);
            yield return StageTimer(jellyfishSeconds);

            // 5. Calm: the water settles, every creature swims gently
            // without touching the feet, vibrations fade, music fades out.
            EnterStage(ExperienceStage.Calm);
            haptics?.StopAll();
            haptics?.PlayLoop("idle_water");
            yield return StageTimer(calmSeconds * 0.5f);
            haptics?.StopAll();
            audioController?.FadeOutMusic(calmSeconds * 0.4f);
            yield return StageTimer(calmSeconds * 0.5f);

            EnterStage(ExperienceStage.Finished);
            Debug.Log("VR Doctor Fish: session complete. " +
                "Press R to run the experience again.");
        }

        void EnterStage(ExperienceStage stage)
        {
            CurrentStage = stage;
            visual?.ApplyStage(stage);
            StageChanged?.Invoke(stage);
            Debug.Log($"VR Doctor Fish: stage -> {stage}");
        }

        IEnumerator StageTimer(float seconds)
        {
            var elapsed = 0f;
            while (elapsed < seconds && !skipRequested)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            skipRequested = false;
        }

        void OnFishNibbled(HapticLeg leg)
        {
            audioController?.PlayNibble();
        }

        void OnBigFishBit(HapticLeg leg)
        {
            audioController?.PlayBigFishBite();
            haptics?.PlayOneShot("big_fish_bite", leg);
        }

        void OnJellyfishStung(HapticLeg leg)
        {
            audioController?.PlayJellyfishSting();
            haptics?.PlayOneShot("jellyfish_sting", leg);
        }
    }
}
