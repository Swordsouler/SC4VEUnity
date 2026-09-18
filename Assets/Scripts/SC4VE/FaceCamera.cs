using UnityEngine;

namespace Sc4ve.Multimodality
{
    /// <summary>
    /// Billboard à taille apparente bornée, pour les textes FLOTTANTS de la salle : le bloc
    /// jauge + prénom des clients, les bulles des agents, les étiquettes de la scène
    /// d'inspection. À chaque image, l'objet pivote pour présenter sa face lisible à la
    /// caméra et se dilate avec la distance — la table du fond n'affiche plus un prénom
    /// minuscule vu de biais. La tablette au poignet et l'écran de départ n'en portent
    /// volontairement pas : l'une est orientée par la main qui la tient, l'autre naît face
    /// au joueur et un panneau CLIQUABLE qui pivote sous le pointeur ferait fuir sa cible.
    ///
    /// Orientation : un TextMesh se lit depuis le -z de son transform (la règle documentée
    /// par le porte-jauge et l'écran de départ), donc le +z du billboard FUIT la caméra —
    /// LookRotation(objet − caméra). Échelle : la BASE est celle trouvée à l'éveil, car les
    /// porte-jauge et porte-bulle annulent l'échelle de leur porteur ; jamais rétrécie sous
    /// sa taille d'origine (déjà lisible de près), grossie au plus ×3. LateUpdate : après
    /// le mouvement de la tête et des agents.
    /// </summary>
    public class FaceCamera : MonoBehaviour
    {
        /// <summary>Distance (m) à laquelle un texte garde sa taille d'origine.</summary>
        private const float ReferenceDistance = 2f;

        /// <summary>Grossissement plafond, atteint à 6 m — l'autre bout de la salle.</summary>
        private const float MaxBoost = 3f;

        private Vector3 _baseScale;

        private void Awake() => _baseScale = transform.localScale;

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 away = transform.position - cam.transform.position;
            if (away.sqrMagnitude < 0.0001f) return;

            transform.rotation = Quaternion.LookRotation(away);
            transform.localScale =
                _baseScale * Mathf.Clamp(away.magnitude / ReferenceDistance, 1f, MaxBoost);
        }
    }
}
