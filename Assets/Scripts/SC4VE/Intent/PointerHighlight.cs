using Sven.Content;
using UnityEngine;
using Pointer = Sven.Context.Pointer;

namespace Sc4ve.Multimodality.Intent
{
    /// <summary>
    /// Contoure en blanc l'objet que ce pointeur vise, pour que le joueur voie ce qu'il
    /// désigne AVANT de parler — « sélectionne ça » n'a de sens que si l'on sait ce qu'est
    /// « ça ». La couleur prime sur le cyan de la sélection (cf. SelectionManager).
    ///
    /// Pourquoi un lancer de rayon à nous plutôt que la liste du Pointer de SVEN : celui-ci
    /// échantillonne à intervalle (CheckInteractor) et en CÔNE, parce qu'il alimente le graphe
    /// — un objet frôlé compte comme pointé, ce qui est le bon choix pour la résolution
    /// multimodale mais donne un retour visuel qui saute d'un objet à l'autre. Ici on veut
    /// exactement ce que le rayon touche, à chaque image. Les deux coexistent sans se gêner :
    /// ce composant ne fait que peindre, il n'écrit rien dans le graphe.
    ///
    /// À poser sur le même objet que le Pointer (DemoSceneBuilder s'en charge) : c'est de ce
    /// transform que partent l'origine et la direction du rayon.
    /// </summary>
    [DisallowMultipleComponent]
    public class PointerHighlight : MonoBehaviour
    {
        [SerializeField, Tooltip("Portée du rayon si aucun Pointer SVEN n'est présent pour la fournir.")]
        private float _fallbackDistance = 4f;

        private Pointer _pointer;

        /// <summary>
        /// Le dernier objet signalé PAR CE POINTEUR. Sans cette mémoire, la main qui ne vise
        /// rien effacerait à chaque image ce que l'autre main est en train de viser.
        /// </summary>
        private SemantizationCore _reported;

        private void Awake() => _pointer = GetComponent<Pointer>();

        // Trace unique : sans elle, « il n'y a pas de contour » ne distingue pas un composant
        // absent de la scène d'un rayon qui ne touche rien.
        private void Start() =>
            Debug.Log($"[PointerHighlight] Actif sur « {name} » " +
                      $"(portée {(_pointer != null ? _pointer.PointerDistance : _fallbackDistance)} m).");

        private void Update()
        {
            SemantizationCore target = Aimed();
            if (ReferenceEquals(target, _reported)) return;

            if (target != null) SelectionManager.SetHovered(target);
            else SelectionManager.ClearHovered(_reported);

            _reported = target;
        }

        /// <summary>Le pointage s'éteint avec le pointeur : une main rangée ne vise plus rien.</summary>
        private void OnDisable()
        {
            SelectionManager.ClearHovered(_reported);
            _reported = null;
        }

        /// <summary>
        /// L'objet sémantisé le plus proche sur le rayon, ou null.
        ///
        /// RaycastAll et non Raycast : le premier collider touché n'est pas forcément
        /// sémantisé — le rayon traverse volontiers un collier de table ou un mur avant
        /// d'atteindre ce qui nous intéresse, et un simple Raycast s'arrêterait dessus.
        /// </summary>
        private SemantizationCore Aimed()
        {
            float distance = _pointer != null ? _pointer.PointerDistance : _fallbackDistance;
            RaycastHit[] hits = Physics.RaycastAll(transform.position, transform.forward, distance);

            SemantizationCore nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (RaycastHit hit in hits)
            {
                if (hit.distance >= nearestDistance) continue;

                // On ne se contoure pas soi-même. Le contrôleur porte un SemantizationCore (il
                // le faut : c'est lui l'émetteur des CollisionEvent de pointage), et son propre
                // collider est le premier que le rayon rencontre — la main restait donc éclairée
                // en permanence et aucune cible ne passait jamais devant elle.
                if (hit.transform.IsChildOf(transform.root)) continue;

                if (!hit.collider.TryGetComponent(out SemantizationCore core)) continue;

                nearest = core;
                nearestDistance = hit.distance;
            }

            return nearest;
        }
    }
}
