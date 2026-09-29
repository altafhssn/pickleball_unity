using UnityEngine;

namespace Pickleball.Data
{
    /// <summary>A cosmetic kit: the shirt and shorts colours the player's character wears. Outfits
    /// carry no stats -- nothing in the match reads them except <see cref="VFX.OutfitVisual"/>.</summary>
    public struct Outfit
    {
        public string id;
        public string name;
        public Color shirt;
        public Color shorts;

        public Outfit(string id, string name, Color shirt, Color shorts)
        {
            this.id = id;
            this.name = name;
            this.shirt = shirt;
            this.shorts = shorts;
        }
    }

    /// <summary>
    /// The outfits a player can choose from. All are free to select: outfit pricing and unlocks are
    /// not decided yet. Red is left out on purpose -- it is the opponent's kit, and the two sides
    /// must never be mistaken for each other on court.
    /// </summary>
    public static class OutfitCatalog
    {
        public const string DefaultId = "classic";

        public static readonly Outfit[] All =
        {
            // The character's own authored colours (PB_Player_shirt1 / shorts1).
            new Outfit(DefaultId, "CLASSIC", new Color(0.20f, 0.55f, 0.95f), new Color(0.10f, 0.20f, 0.38f)),
            new Outfit("volt", "VOLT", new Color(0.84f, 0.98f, 0.00f), new Color(0.12f, 0.13f, 0.15f)),
            new Outfit("ocean", "OCEAN", new Color(0.10f, 0.78f, 0.74f), new Color(0.96f, 0.96f, 0.97f)),
            new Outfit("royal", "ROYAL", new Color(0.49f, 0.33f, 0.90f), new Color(0.98f, 0.82f, 0.25f)),
            new Outfit("bubblegum", "BUBBLEGUM", new Color(1.00f, 0.52f, 0.78f), new Color(0.15f, 0.18f, 0.36f)),
            new Outfit("midnight", "MIDNIGHT", new Color(0.13f, 0.14f, 0.18f), new Color(0.30f, 0.32f, 0.38f)),
        };

        public static bool Exists(string id)
        {
            foreach (Outfit o in All) if (o.id == id) return true;
            return false;
        }

        /// <summary>The outfit with this id, or the default for an unknown id.</summary>
        public static Outfit Find(string id)
        {
            foreach (Outfit o in All) if (o.id == id) return o;
            return All[0];
        }
    }
}
