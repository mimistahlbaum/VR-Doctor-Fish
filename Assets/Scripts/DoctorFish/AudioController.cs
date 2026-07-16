using System.Collections;
using UnityEngine;

namespace DoctorFish
{
    /// <summary>
    /// Background music and one-shot sound effects. Clips are loaded from
    /// Assets/Resources/Audio (see the README in that folder for the mapping
    /// between files and interactions).
    /// </summary>
    public class AudioController : MonoBehaviour
    {
        [Range(0f, 1f)] public float musicVolume = 0.55f;
        [Range(0f, 1f)] public float effectsVolume = 0.9f;

        AudioSource music;
        AudioSource effects;

        AudioClip bgm;
        AudioClip fishNibble1;
        AudioClip fishNibble2;
        AudioClip bigFishBite;
        AudioClip jellyfishSting;
        AudioClip waterPop;

        float lastNibbleTime = -10f;

        void Awake()
        {
            bgm = Resources.Load<AudioClip>("Audio/drfish");
            fishNibble1 = Resources.Load<AudioClip>("Audio/drfish_fish_se1");
            fishNibble2 = Resources.Load<AudioClip>("Audio/drfish_fish_se2");
            bigFishBite = Resources.Load<AudioClip>("Audio/drfish_big_se1");
            jellyfishSting = Resources.Load<AudioClip>("Audio/drfish_jelly_se1");
            waterPop = Resources.Load<AudioClip>("Audio/drfish_pop_se1");

            music = gameObject.AddComponent<AudioSource>();
            music.clip = bgm;
            music.loop = true;
            music.playOnAwake = false;
            music.spatialBlend = 0f;
            music.volume = 0f;

            // Effects live on a child so they can sit at the pool while the
            // non-spatial music stays wherever this controller is.
            var effectsGo = new GameObject("Effects");
            effectsGo.transform.SetParent(transform, false);
            effects = effectsGo.AddComponent<AudioSource>();
            effects.playOnAwake = false;
            effects.spatialBlend = 0.6f;
            effects.volume = effectsVolume;
        }

        /// <summary>Place the effects source where the interactions happen.</summary>
        public void SetEffectsPosition(Vector3 worldPosition)
        {
            effects.transform.position = worldPosition;
        }

        public void StartMusic(float fadeSeconds = 2f)
        {
            if (bgm == null)
                return;
            music.Play();
            StartCoroutine(FadeMusic(musicVolume, fadeSeconds));
        }

        public void FadeOutMusic(float fadeSeconds = 5f)
        {
            StartCoroutine(FadeMusic(0f, fadeSeconds, stopAtEnd: true));
        }

        IEnumerator FadeMusic(float target, float seconds, bool stopAtEnd = false)
        {
            var start = music.volume;
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                music.volume = Mathf.Lerp(start, target, elapsed / seconds);
                yield return null;
            }
            music.volume = target;
            if (stopAtEnd)
                music.Stop();
        }

        /// <summary>Ticklish nibble, alternating the two variations at random.</summary>
        public void PlayNibble()
        {
            // Small fish arrive in bursts; keep the soundscape from clipping.
            if (Time.time - lastNibbleTime < 0.25f)
                return;
            lastNibbleTime = Time.time;
            var clip = Random.value < 0.5f ? fishNibble1 : fishNibble2;
            PlayEffect(clip, Random.Range(0.92f, 1.08f), 0.8f);
        }

        public void PlayBigFishBite()
        {
            PlayEffect(bigFishBite, 1f, 1f);
        }

        public void PlayJellyfishSting()
        {
            PlayEffect(jellyfishSting, Random.Range(0.95f, 1.05f), 1f);
        }

        /// <summary>Soft splash used when the feet first enter the water.</summary>
        public void PlayWaterPop()
        {
            PlayEffect(waterPop, 1f, 0.7f);
        }

        void PlayEffect(AudioClip clip, float pitch, float volumeScale)
        {
            if (clip == null)
                return;
            effects.pitch = pitch;
            effects.PlayOneShot(clip, volumeScale * effectsVolume);
        }
    }
}
