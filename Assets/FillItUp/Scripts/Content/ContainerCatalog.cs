using UnityEngine;

namespace FillItUp
{
    public enum ContainerKind
    {
        Spoon, Teacup, Glass, Mug, Bowl, Bottle, Jug, Vase, Fishbowl, WateringCan, Pot, Bucket, FishTank, Barrel, Bathtub, Pool
    }

    /// <summary>Real-world capacities (millilitres) and how big each container is shown.</summary>
    public static class ContainerCatalog
    {
        public static readonly ContainerKind[] All = (ContainerKind[])System.Enum.GetValues(typeof(ContainerKind));

        public static float Millilitres(ContainerKind k)
        {
            switch (k)
            {
                case ContainerKind.Spoon: return 5f;
                case ContainerKind.Teacup: return 200f;
                case ContainerKind.Glass: return 300f;
                case ContainerKind.Mug: return 350f;
                case ContainerKind.Bowl: return 600f;
                case ContainerKind.Bottle: return 1000f;
                case ContainerKind.Jug: return 2000f;
                case ContainerKind.Vase: return 2500f;
                case ContainerKind.Fishbowl: return 4000f;
                case ContainerKind.WateringCan: return 5000f;
                case ContainerKind.Pot: return 8000f;
                case ContainerKind.Bucket: return 10000f;
                case ContainerKind.FishTank: return 60000f;
                case ContainerKind.Barrel: return 150000f;
                case ContainerKind.Bathtub: return 200000f;
                default: return 800000f; // paddling pool
            }
        }

        /// <summary>Containers that fit in a pour scene (the pool and tub are too big to lift and tip).</summary>
        public static bool CanPour(ContainerKind k) => k <= ContainerKind.Bucket;
    }
}
