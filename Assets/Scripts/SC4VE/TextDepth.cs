using UnityEngine;
using UnityEngine.Rendering;

namespace Sc4ve.Multimodality
{
    /// <summary>
    /// Les textes 3D (TextMesh) respectent la profondeur. Leur shader (GUI/Text Shader, celui
    /// du matériau de la police intégrée) tire son test de profondeur de unity_GUIZTestMode,
    /// que rien ne règle sous URP : chaque texte se dessinait PAR-DESSUS tout — un prénom ou
    /// une bulle à l'autre bout de la salle passait devant la tablette tenue sous les yeux.
    ///
    /// Réglé une fois sur le matériau de la police, que partagent tous les textes : prénoms,
    /// bulles, panneaux, aide des manettes, tablette. Chacun est posé au moins 0,3 mm devant
    /// son propre fond, il reste visible ; ce qui est derrière un objet est caché, comme le
    /// reste de la scène.
    /// </summary>
    internal static class TextDepth
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null && font.material != null)
                font.material.SetFloat("unity_GUIZTestMode", (float)CompareFunction.LessEqual);
        }
    }
}
