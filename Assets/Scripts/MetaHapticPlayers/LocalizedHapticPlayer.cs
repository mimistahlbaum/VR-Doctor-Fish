using System.Collections;
using UnityEngine;
using static OVRInput;

/// <summary>
/// Plays localized OVRInput haptics from editable amplitude and frequency curves.
/// </summary>
[AddComponentMenu("Haptics/Localized Haptic Player")]
public class LocalizedHapticPlayer : HapticPlayerBase
{
    [Header("Playback")]
    [Tooltip("The controller location to vibrate.")]
    public HapticsLocation HapticsLocation = HapticsLocation.Hand;

    [Tooltip("How long the generated haptic should play.")]
    [Min(0.01f)]
    [SerializeField] private float duration = 1f;

    [Tooltip("Automatically play this haptic when the scene starts.")]
    [SerializeField] private bool playOnStart = false;

    [Header("Curves")]
    [Tooltip("Regenerate both curves when preset settings change. Turn this off before hand-editing the curves.")]
    [SerializeField] private bool autoGenerateCurvesFromPresets = true;

    [Tooltip("Controls vibration strength over time.")]
    [SerializeField] private OscillatorCurveSettings amplitudeCurve = OscillatorCurveSettings.CreateDefaultAmplitude();

    [Tooltip("Controls vibration frequency over time.")]
    [SerializeField] private OscillatorCurveSettings frequencyCurve = OscillatorCurveSettings.CreateDefaultFrequency();

    private Coroutine m_playRoutine;

    private void OnValidate()
    {
        duration = Mathf.Max(0.01f, duration);

        if (amplitudeCurve == null)
        {
            amplitudeCurve = OscillatorCurveSettings.CreateDefaultAmplitude();
        }

        if (frequencyCurve == null)
        {
            frequencyCurve = OscillatorCurveSettings.CreateDefaultFrequency();
        }

        if (autoGenerateCurvesFromPresets)
        {
            GenerateCurvesFromPresets();
        }
    }

    private void Start()
    {
        if (playOnStart)
        {
            Play();
        }
    }

    public override void Play()
    {
        if (m_playRoutine != null)
        {
            StopCoroutine(m_playRoutine);
        }

        m_playRoutine = StartCoroutine(PlayForDuration());
    }

    public override void Stop()
    {
        if (m_playRoutine != null)
        {
            StopCoroutine(m_playRoutine);
            m_playRoutine = null;
        }

        SetControllerLocalizedVibration(HapticsLocation, 0f, 0f);
    }

    [ContextMenu("Generate Curves From Presets")]
    public void GenerateCurvesFromPresets()
    {
        amplitudeCurve.Generate(duration);
        frequencyCurve.Generate(duration);
    }

    private IEnumerator PlayForDuration()
    {
        float timer = 0f;

        while (timer <= duration)
        {
            float frequency = Mathf.Clamp01(frequencyCurve.Evaluate(timer, 1f));
            float amplitude = Mathf.Clamp01(amplitudeCurve.Evaluate(timer, 1f));
            SetControllerLocalizedVibration(HapticsLocation, frequency, amplitude);

            timer += Time.deltaTime;
            yield return null;
        }

        SetControllerLocalizedVibration(HapticsLocation, 0f, 0f);
        m_playRoutine = null;
    }
}
