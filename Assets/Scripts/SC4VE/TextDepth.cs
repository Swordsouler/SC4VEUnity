using UnityEngine;

namespace Sc4ve.Multimodality
{
    /// <summary>
    /// Les textes 3D (TextMesh) respectent la profondeur. Leur shader d'origine (GUI/Text
    /// Shader, celui du matériau de la police intégrée) tire son test de profondeur de
    /// unity_GUIZTestMode, qui ne filtrait rien en jeu : chaque texte se dessinait PAR-DESSUS
    /// tout — un prénom ou une bulle à l'autre bout de la salle passait devant la tablette
    /// tenue sous les yeux. Régler cette valeur sur le matériau n'y a rien changé.
    ///
    /// Le matériau de la police, que partagent tous les textes — prénoms, bulles, panneaux,
    /// aide des manettes, tablette —, prend donc le même shader au test de profondeur actif
    /// (Resources/Texte3D.shader). Chaque texte est posé au moins 0,3 mm devant son propre
    /// fond et reste visible ; ce qui est derrière un objet est caché, comme le reste de la
    /// scène. Le matériau est une ressource intégrée : il retrouve son shader en fin de
    /// partie, sans quoi l'éditeur le garderait.
    /// </summary>
    internal static class TextDepth
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Shader depthTested = Resources.Load<Shader>("Texte3D");
            if (font == null || font.material == null || depthTested == null)
            {
                Debug.LogWarning("[Texte] Police ou shader « Texte3D » introuvable : les textes 3D " +
                                 "restent dessinés par-dessus tout.");
                return;
            }

            Material material = font.material;
            Shader original = material.shader;
            material.shader = depthTested;
            Application.quitting += () => material.shader = original;
        }
    }
}
