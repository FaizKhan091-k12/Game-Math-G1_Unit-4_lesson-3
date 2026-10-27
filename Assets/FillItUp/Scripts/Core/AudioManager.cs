using System.Collections.Generic;
using UnityEngine;

namespace FillItUp
{
    /// <summary>SFX, Splashy's voice lines and the music loop. One switch mutes everything.</summary>
    public class AudioManager : MonoBehaviour
    {
        const int SfxVoices = 8;
        const float MusicDuck = 0.4f;
        const string VoicePrefix = "vo_";

        public static AudioManager I { get; private set; }

        readonly Dictionary<Sfx, GameConfig.SfxEntry> sfx = new Dictionary<Sfx, GameConfig.SfxEntry>();
        readonly Dictionary<string, AudioClip> voice = new Dictionary<string, AudioClip>();
        readonly Dictionary<Sfx, float> lastPlayed = new Dictionary<Sfx, float>();
        AudioSource[] sfxSources;
        AudioSource voiceSource, musicSource, loopSource;
        int nextSfx;
        float duck = 1f;
        GameConfig config;

        public bool SoundOn
        {
            get => SaveSystem.Data == null || SaveSystem.Data.soundOn;
            set
            {
                SaveSystem.Data.soundOn = value;
                SaveSystem.Save();
                AudioListener.volume = value ? 1f : 0f;
            }
        }

        public bool IsVoicePlaying => voiceSource != null && voiceSource.isPlaying;

        /// <summary>Raised with the line key and its length whenever Splashy starts speaking (drives subtitles).</summary>
        public event System.Action<string, float> Spoke;
        public event System.Action Silenced;

        public void Init(GameConfig cfg)
        {
            I = this;
            config = cfg;
            foreach (var e in cfg.sfx) if (e.clip != null) sfx[e.id] = e;
            foreach (var c in cfg.voiceLines)
                if (c != null) voice[c.name.StartsWith(VoicePrefix) ? c.name.Substring(VoicePrefix.Length) : c.name] = c;

            sfxSources = new AudioSource[SfxVoices];
            for (int i = 0; i < SfxVoices; i++) sfxSources[i] = MakeSource("Sfx" + i, false);
            voiceSource = MakeSource("Voice", false);
            loopSource = MakeSource("Loop", true);
            musicSource = MakeSource("Music", true);
            AudioListener.volume = SoundOn ? 1f : 0f;
            if (cfg.music != null)
            {
                musicSource.clip = cfg.music;
                musicSource.volume = cfg.musicVolume;
                musicSource.Play();
            }
        }

        AudioSource MakeSource(string n, bool loop)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            return s;
        }

        void Update()
        {
            if (musicSource == null) return;
            float target = IsVoicePlaying ? MusicDuck : 1f;
            duck = Mathf.MoveTowards(duck, target, Time.unscaledDeltaTime * 3f);
            musicSource.volume = config.musicVolume * duck;
        }

        public void Play(Sfx id, float pitch = 1f, float volumeScale = 1f)
        {
            if (!sfx.TryGetValue(id, out var e)) return;
            if (lastPlayed.TryGetValue(id, out float t) && Time.unscaledTime - t < 0.04f) return;
            lastPlayed[id] = Time.unscaledTime;
            var s = sfxSources[nextSfx];
            nextSfx = (nextSfx + 1) % sfxSources.Length;
            s.pitch = pitch;
            s.PlayOneShot(e.clip, e.volume * volumeScale);
        }

        /// <summary>Starts a looping effect (the pour stream); StopLoop ends it.</summary>
        public void PlayLoop(Sfx id, float volume = 1f)
        {
            if (!sfx.TryGetValue(id, out var e)) return;
            loopSource.clip = e.clip;
            loopSource.volume = e.volume * volume;
            loopSource.Play();
        }

        public void StopLoop() => loopSource.Stop();

        /// <summary>Speaks a voice line (file vo_key). Returns its length in seconds, 0 when missing.</summary>
        public float Speak(string key)
        {
            if (string.IsNullOrEmpty(key) || !voice.TryGetValue(key, out var clip)) return 0f;
            voiceSource.Stop();
            voiceSource.clip = clip;
            voiceSource.volume = config.voiceVolume;
            voiceSource.Play();
            Spoke?.Invoke(key, clip.length);
            return clip.length;
        }

        public void StopVoice()
        {
            voiceSource.Stop();
            Silenced?.Invoke();
        }

        public float VoiceLength(string key) => voice.TryGetValue(key, out var c) ? c.length : 0f;

        /// <summary>
        /// Mouth opening while a line plays (0..1). Procedural syllable flapping: AudioClip.GetData is not
        /// available on WebGL, and this reads just as well on a cartoon mouth.
        /// </summary>
        public float VoiceLevel()
        {
            if (!IsVoicePlaying) return 0f;
            float t = voiceSource.time;
            float syllable = Mathf.Abs(Mathf.Sin(t * 11f)) * (0.55f + 0.45f * Mathf.PerlinNoise(t * 3f, 0.3f));
            float fadeOut = Mathf.Clamp01((voiceSource.clip.length - t) * 6f);
            return syllable * fadeOut;
        }
    }
}
