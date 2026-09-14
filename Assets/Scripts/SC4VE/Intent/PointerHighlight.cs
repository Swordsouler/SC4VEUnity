using Sven.Content;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Pointer = Sven.Context.Pointer;

namespace Sc4ve.Multimodality.Intent
{
    /// <summary>
    /// Contoure en blanc ce que ce pointeur vise, pour que le joueur voie ce qu'il désigne
    /// AVANT de parler — « sélectionne ça » n'a de sens que si l'on sait ce qu'est « ça ».
    /// Le blanc prime sur le cyan de la sélection (cf. SelectionManager).
    ///
    /// Ce composant ne lance AUCUN rayon : il lit la liste que le Pointer de SVEN tient déjà
    /// à jour. C'est la seule façon d'afficher exactement ce que la résolution multimodale va
    /// considérer — même cône (PointerConeAngle), même portée, même critère de sémantisation.
    /// Un rayon à nous, si fin soit-il, aurait montré autre chose que ce que la commande
    /// vocale allait retenir, et le retour visuel aurait menti.
    ///
    /// PLUSIEURS objets à la fois, donc : le rayon est transperçant, il touche tout ce qui se
    /// trouve sur sa trajectoire et la sélection les considère tous. Le contour le montre.
    ///
    /// Conséquence à connaître : la liste se rafraîchit à la cadence de sémantisation
    /// (SvenSettings.SemanticizeFrequency), pas à chaque image. Le contour suit donc le
    /// pointeur par paliers — c'est le prix de l'exactitude.
    /// </summary>
    [DisallowMultipleComponent]
    public class PointerHighlight : MonoBehaviour
    {
        private Pointer _pointer;

        /// <summary>
        /// Ce que ce pointeur a signalé en dernier. Sert à ne réveiller SelectionManager que
        /// lorsque la liste change vraiment : sans cette comparaison, on repeindrait tout à
        /// chaque image pour un résultat identique.
        /// </summary>
        private readonly HashSet<SemantizationCore> _reported = new();

        private void Awake() => _pointer = GetComponent<Pointer>();

        private void Start()
        {
            if (_pointer == null)
            {
                Debug.LogWarning($"[PointerHighlight] « {name} » n'a pas de Pointer SVEN : " +
                                 "rien à contourer. Le composant se pose sur le pointeur lui-même.");
                enabled = false;
                return;
            }

            // Trace unique : sans elle, « il n'y a pas de contour » ne distingue pas un
            // composant absent d'un rayon qui ne touche rien.
            Debug.Log($"[PointerHighlight] Actif sur « {name} » — portée {_pointer.PointerDistance} m, " +
                      $"cône {_pointer.PointerConeAngle}°.");
        }

        private void Update()
        {
            if (_pointer == null) return;

            // On ne se contoure pas soi-même : le contrôleur porte un SemantizationCore, il le
            // faut — c'est lui l'émetteur des CollisionEvent de pointage.
            IEnumerable<SemantizationCore> aimed = _pointer.currentInteractedObjects
                .Where(o => o != null && !o.transform.IsChildOf(transform.root));

            if (_reported.SetEquals(aimed)) return;

            _reported.Clear();
            _reported.UnionWith(aimed);
            SelectionManager.SetHovered(this, _reported);
        }

        /// <summary>Le pointage s'éteint avec le pointeur : une main rangée ne vise plus rien.</summary>
        private void OnDisable()
        {
            _reported.Clear();
            SelectionManager.ClearHovered(this);
        }
    }
}
