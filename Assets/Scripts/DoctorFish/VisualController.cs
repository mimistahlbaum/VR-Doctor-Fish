using System;
using System.Collections;
using UnityEngine;

namespace DoctorFish
{
    /// <summary>
    /// Drives everything the user sees: per-stage lighting, fog and water
    /// moods, and the creature choreography. Creature contact events are
    /// forwarded so the ExperienceStateManager can trigger the matching
    /// audio and haptics.
    /// </summary>
    public class VisualController : MonoBehaviour
    {
        [Serializable]
        public class StageLook
        {
            public Color lightColor = Color.white;
            public float lightIntensity = 1f;
            public Color ambient = Color.grey;
            public Color fog = Color.grey;
            public Color water = Color.cyan;
            public float waterChoppiness = 1f;
        }

        public Light sun;
        public WaterSurface water;
        public SmallFishSchool smallFish;
        public BigFishController bigFish;
        public JellyfishSwarm jellyfish;

        public float transitionSeconds = 2.5f;

        /// <summary>
        /// A small fish nibbled a foot (audio cue plus a haptic tap at the
        /// unit nearest the world contact position).
        /// </summary>
        public event Action<HapticLeg, Vector3> FishNibbled;
        /// <summary>The big fish bit a foot (audio and haptic cue).</summary>
        public event Action<HapticLeg> BigFishBit;
        /// <summary>A jellyfish stung a leg (audio and haptic cue).</summary>
        public event Action<HapticLeg> JellyfishStung;

        Coroutine transition;

        StageLook LookFor(ExperienceStage stage)
        {
            switch (stage)
            {
                case ExperienceStage.Welcome:
                    return new StageLook
                    {
                        lightColor = new Color(1f, 0.96f, 0.88f),
                        lightIntensity = 1f,
                        ambient = new Color(0.5f, 0.62f, 0.68f),
                        fog = new Color(0.16f, 0.27f, 0.32f),
                        water = new Color(0.3f, 0.66f, 0.72f, 0.5f),
                        waterChoppiness = 1f
                    };
                case ExperienceStage.SmallFish:
                    return new StageLook
                    {
                        lightColor = new Color(1f, 0.97f, 0.9f),
                        lightIntensity = 1.05f,
                        ambient = new Color(0.52f, 0.64f, 0.68f),
                        fog = new Color(0.16f, 0.28f, 0.32f),
                        water = new Color(0.32f, 0.68f, 0.7f, 0.48f),
                        waterChoppiness = 1.3f
                    };
                case ExperienceStage.BigFish:
                    return new StageLook
                    {
                        lightColor = new Color(0.9f, 0.92f, 1f),
                        lightIntensity = 0.9f,
                        ambient = new Color(0.4f, 0.5f, 0.6f),
                        fog = new Color(0.1f, 0.2f, 0.28f),
                        water = new Color(0.24f, 0.5f, 0.62f, 0.55f),
                        waterChoppiness = 2f
                    };
                case ExperienceStage.Jellyfish:
                    return new StageLook
                    {
                        lightColor = new Color(0.75f, 0.7f, 1f),
                        lightIntensity = 0.65f,
                        ambient = new Color(0.32f, 0.28f, 0.5f),
                        fog = new Color(0.12f, 0.08f, 0.24f),
                        water = new Color(0.4f, 0.3f, 0.62f, 0.55f),
                        waterChoppiness = 1.4f
                    };
                case ExperienceStage.Calm:
                default:
                    return new StageLook
                    {
                        lightColor = new Color(1f, 0.9f, 0.78f),
                        lightIntensity = 0.85f,
                        ambient = new Color(0.55f, 0.55f, 0.55f),
                        fog = new Color(0.2f, 0.26f, 0.3f),
                        water = new Color(0.35f, 0.7f, 0.74f, 0.42f),
                        waterChoppiness = 0.45f
                    };
            }
        }

        void Start()
        {
            if (smallFish != null)
                smallFish.Nibbled += (leg, position) =>
                    FishNibbled?.Invoke(leg, position);
            if (bigFish != null)
                bigFish.Bit += leg => BigFishBit?.Invoke(leg);
            if (jellyfish != null)
                jellyfish.Stung += leg => JellyfishStung?.Invoke(leg);
        }

        public void ApplyStage(ExperienceStage stage)
        {
            if (transition != null)
                StopCoroutine(transition);
            transition = StartCoroutine(BlendTo(LookFor(stage)));

            switch (stage)
            {
                case ExperienceStage.Welcome:
                    break;
                case ExperienceStage.SmallFish:
                    smallFish?.Enter();
                    smallFish?.StartNibbling();
                    break;
                case ExperienceStage.BigFish:
                    smallFish?.Leave();
                    bigFish?.BeginEncounter();
                    break;
                case ExperienceStage.Jellyfish:
                    bigFish?.Retreat();
                    jellyfish?.Enter();
                    break;
                case ExperienceStage.Calm:
                    // Every creature stays in the water, swimming gently
                    // with no nibbles, bites or stings.
                    smallFish?.Enter();
                    bigFish?.SwimCalm();
                    jellyfish?.CalmDrift();
                    break;
                case ExperienceStage.Finished:
                    // The creatures keep swimming until the session is
                    // restarted.
                    break;
            }
        }

        IEnumerator BlendTo(StageLook look)
        {
            var startLight = sun != null ? sun.color : Color.white;
            var startIntensity = sun != null ? sun.intensity : 1f;
            var startAmbient = RenderSettings.ambientLight;
            var startFog = RenderSettings.fogColor;
            var startWater = water != null ? water.GetTint() : Color.white;
            var startChop = water != null ? water.choppiness : 1f;

            var elapsed = 0f;
            while (elapsed < transitionSeconds)
            {
                elapsed += Time.deltaTime;
                var k = Mathf.SmoothStep(0f, 1f, elapsed / transitionSeconds);
                if (sun != null)
                {
                    sun.color = Color.Lerp(startLight, look.lightColor, k);
                    sun.intensity = Mathf.Lerp(startIntensity,
                        look.lightIntensity, k);
                }
                RenderSettings.ambientLight =
                    Color.Lerp(startAmbient, look.ambient, k);
                RenderSettings.fogColor = Color.Lerp(startFog, look.fog, k);
                if (water != null)
                {
                    water.SetTint(Color.Lerp(startWater, look.water, k));
                    water.choppiness = Mathf.Lerp(startChop,
                        look.waterChoppiness, k);
                }
                yield return null;
            }
            transition = null;
        }
    }
}
