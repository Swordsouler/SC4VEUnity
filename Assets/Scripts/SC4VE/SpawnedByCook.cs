using UnityEngine;

namespace Sc4ve.Multimodality
{
    /// <summary>
    /// Marqueur des copies d'ingrédients du garde-manger infini (Cook.CloneIngredient) :
    /// « réinitialise la scène » les détruit — seuls les ORIGINAUX se restaurent
    /// (OriginalStateStore), et sans ce marquage les copies s'accumuleraient de visiteur
    /// en visiteur. Un composant vide : l'information EST sa présence.
    /// </summary>
    public sealed class SpawnedByCook : MonoBehaviour { }
}
