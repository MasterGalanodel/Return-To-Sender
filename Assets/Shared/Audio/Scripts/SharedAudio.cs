using System.Collections.Generic;
using UnityEngine;

namespace Shared.Audio
{
    // Scene-local 2D audio: one-shot SFX and a looping BGM section. The object is created on first use
    // and dies with the scene, so each scene only plays what it asks for.
    [DisallowMultipleComponent]
    public sealed class SharedAudio : MonoBehaviour
    {
        private static SharedAudio instance;
        private AudioSource sfx;
        private AudioSource bgm;
        private int bgmLoopStart, bgmLoopEnd;
        private readonly Dictionary<AudioClip, float> lastPlayed = new Dictionary<AudioClip, float>();

        private static SharedAudio Instance
        {
            get
            {
                if (instance) return instance;
                var go = new GameObject("Audio");
                instance = go.AddComponent<SharedAudio>();
                instance.sfx = go.AddComponent<AudioSource>();
                instance.sfx.playOnAwake = false;
                instance.sfx.spatialBlend = 0;
                // Scenes are built by setup tools that don't add a listener; without one nothing is audible.
                if (!FindAnyObjectByType<AudioListener>())
                {
                    var cam = Camera.main;
                    (cam ? cam.gameObject : go).AddComponent<AudioListener>();
                }
                return instance;
            }
        }

        // Plays a one-shot sound. minInterval stops rapid repeats of the same clip (e.g. many towers firing at once).
        public static void PlaySfx(AudioClip clip, float volume = 1, float minInterval = 0)
        {
            if (!clip || volume <= 0) return;
            var audio = Instance;
            float now = Time.unscaledTime;
            if (minInterval > 0 && audio.lastPlayed.TryGetValue(clip, out float last) && now - last < minInterval) return;
            audio.lastPlayed[clip] = now;
            audio.sfx.PlayOneShot(clip, volume);
        }

        // Loops clip between loopStart and loopEnd seconds (loopEnd <= 0 or past the clip end = loop the whole clip).
        public static void PlayBgm(AudioClip clip, float volume = 1, float loopStart = 0, float loopEnd = 0)
        {
            if (!clip) return;
            var audio = Instance;
            if (!audio.bgm)
            {
                audio.bgm = audio.gameObject.AddComponent<AudioSource>();
                audio.bgm.playOnAwake = false;
                audio.bgm.spatialBlend = 0;
                audio.bgm.loop = true;
            }
            int hz = clip.frequency;
            audio.bgmLoopStart = Mathf.Clamp(Mathf.RoundToInt(loopStart * hz), 0, clip.samples - 1);
            audio.bgmLoopEnd = loopEnd > 0 ? Mathf.Clamp(Mathf.RoundToInt(loopEnd * hz), audio.bgmLoopStart + 1, clip.samples) : clip.samples;
            audio.bgm.clip = clip;
            audio.bgm.volume = volume;
            audio.bgm.timeSamples = audio.bgmLoopStart;
            audio.bgm.Play();
        }

        public static void StopBgm()
        {
            if (instance && instance.bgm) instance.bgm.Stop();
        }

        private void Update()
        {
            if (!bgm || !bgm.isPlaying || bgmLoopEnd >= bgm.clip.samples) return;
            int t = bgm.timeSamples;
            if (t >= bgmLoopEnd) bgm.timeSamples = bgmLoopStart + (t - bgmLoopEnd) % Mathf.Max(1, bgmLoopEnd - bgmLoopStart);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
