using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace FillItUp
{
    public enum Sfx
    {
        UiClick, UiHover, Select, Correct, Wrong, Splash, Pour, Fill, Overflow, Sparkle, Droplet, Streak,
        LevelComplete, Confetti, Whoosh, Unlock, Boing, Land, Giggle, Aww, StarStamp, Drumroll, Yawn, Tick
    }

    public enum Mat
    {
        Grass, GrassDark, Sand, Wood, WoodDark, Stone, White, Cream, Red, Orange, Yellow, Green, Leaf, Teal,
        Blue, Navy, Purple, Pink, Metal, Gold, Black, Clay, Glass, Water, MascotBody, Cheek, EyeWhite, Pupil,
        Cloud, Sun, Sky, Shadow, Pedestal, PedestalRim, Fish, Rubber
    }

    /// <summary>Generated assets the runtime needs (filled in by the editor builder, loaded from Resources).</summary>
    public class GameConfig : ScriptableObject
    {
        public const string ResourcePath = "FIU_Config";
        static GameConfig cached;

        [System.Serializable]
        public class SfxEntry
        {
            public Sfx id;
            public AudioClip clip;
            [Range(0, 1)] public float volume = 1f;
        }

        [System.Serializable]
        public class ContainerEntry
        {
            public ContainerKind kind;
            public GameObject prefab;
        }

        [Header("Text")]
        public TMP_FontAsset font;
        public Material fontOutline;

        [Header("Sprites")]
        public Sprite softCircle, star, droplet, play, pause, home, replay, next, back, speakerOn, speakerOff,
            lockIcon, check, trophy, chart, close, fitsIcon, spillsIcon, level1Icon, level2Icon, level3Icon, flame;

        [Header("Materials (indexed by Mat)")]
        public Material[] materials;
        public Material particleSoft, particleStar, particleDrop;

        [Header("Audio")]
        public List<SfxEntry> sfx = new List<SfxEntry>();
        public List<AudioClip> voiceLines = new List<AudioClip>();
        public AudioClip music;
        [Range(0, 1)] public float musicVolume = 0.32f;
        [Range(0, 1)] public float voiceVolume = 1f;

        [Header("Prefabs")]
        public List<ContainerEntry> containers = new List<ContainerEntry>();
        public GameObject edovuLogo;
        public Sprite logoSprite; // crisp pre-scaled copy of the Edovu logo (see FillItUpBuilder.BuildLogo)

        public Material Get(Mat m) => materials != null && (int)m < materials.Length ? materials[(int)m] : null;

        public GameObject ContainerPrefab(ContainerKind k)
        {
            foreach (var c in containers) if (c.kind == k) return c.prefab;
            return null;
        }

        public static GameConfig Load()
        {
            if (cached == null) cached = Resources.Load<GameConfig>(ResourcePath);
            return cached;
        }
    }
}
