using UnityEngine;
using Pickleball.Data;

namespace Pickleball.VFX
{
    /// <summary>
    /// Dresses the player's character in the selected outfit by recolouring its shirt and shorts
    /// meshes. Purely visual: it sets a MaterialPropertyBlock and nothing else, so an outfit can
    /// never touch a match stat. The authored materials stay untouched, which is also why the
    /// default outfit clears the override instead of writing its own colours.
    /// </summary>
    [DisallowMultipleComponent]
    public class OutfitVisual : MonoBehaviour
    {
        private const string ShirtMesh = "shirt1";
        private const string ShortsMesh = "shorts1";
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColor = Shader.PropertyToID("_Color");

        private void OnEnable()
        {
            MetaGameState.OnOutfitChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            MetaGameState.OnOutfitChanged -= Refresh;
        }

        public void Refresh()
        {
            Apply(gameObject, MetaGameState.CurrentOutfit);
        }

        public static void Apply(GameObject character, Outfit outfit)
        {
            if (character == null) return;
            bool authored = outfit.id == OutfitCatalog.DefaultId;
            foreach (SkinnedMeshRenderer r in character.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.name == ShirtMesh) Tint(r, outfit.shirt, authored);
                else if (r.name == ShortsMesh) Tint(r, outfit.shorts, authored);
            }
        }

        private static void Tint(Renderer renderer, Color color, bool clear)
        {
            if (clear)
            {
                renderer.SetPropertyBlock(null);
                return;
            }
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColor, color);
            block.SetColor(LegacyColor, color);
            renderer.SetPropertyBlock(block);
        }
    }
}
