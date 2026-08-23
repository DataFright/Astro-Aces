using UnityEngine;

namespace AstroAces.Core
{
    /// <summary>
    /// Runtime mirror of the layer indices created by Astro Aces > Setup Project.
    /// Never hard-code a layer number or a LayerMask literal anywhere else.
    /// </summary>
    public static class Layers
    {
        public const int Aircraft = 6;
        public const int Projectile = 7;
        public const int Ground = 8;
        public const int MinimapIcon = 9;
        public const int Cloud = 10;

        /// <summary>What a bullet is allowed to hit. Clouds and other bullets are excluded.</summary>
        public static readonly LayerMask ProjectileHitMask = (1 << Aircraft) | (1 << Ground);

        /// <summary>What the enemy's line-of-sight check treats as opaque.</summary>
        public static readonly LayerMask VisionBlockMask = 1 << Ground;

        /// <summary>What the minimap camera renders: icons only, never the world.</summary>
        public static readonly LayerMask MinimapMask = 1 << MinimapIcon;

        /// <summary>What the main camera renders: everything except minimap icons.</summary>
        public static readonly LayerMask MainCameraMask = ~(1 << MinimapIcon);
    }
}
