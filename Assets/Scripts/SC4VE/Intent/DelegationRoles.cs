using Sven.Content;
using Sven.Multimodality;
using System.Collections.Generic;
using System.Linq;

namespace Sc4ve.Multimodality.Intent
{
    /// <summary>
    /// Qui joue quel rôle dans un ordre de délégation.
    ///
    /// « Toi 👆, va servir cette table-là 👆 » désigne deux objets, et il faut savoir lequel est
    /// l'agent et lequel est la destination. La tentation est de s'appuyer sur l'ordre — premier
    /// SelectionParameter, puis second — comme le fait PutInCommand autour de sa préposition.
    /// Ici ce serait fragile : les deux pointages ne sont séparés par aucun mot pivot, et le
    /// mode RuleBased n'émet qu'une seule sélection alors que le LLM en émet deux.
    ///
    /// On lit donc les rôles dans les OBJETS et non dans la phrase : un serveur porte un
    /// composant Delegation, une table porte l'annotation sven:Table. Aucune des deux ne peut être
    /// prise pour l'autre, donc l'ordre des mots n'a plus à être fiable — et l'énoncé inversé
    /// (« va servir cette table-là 👆 avec toi 👆 ») marche sans code supplémentaire.
    ///
    /// La lecture des annotations est synchrone parce que la hiérarchie est matérialisée sur
    /// l'objet à l'édition : interroger le graphe serait asynchrone, donc inutilisable depuis
    /// un Execute() qui doit répondre immédiatement.
    /// </summary>
    internal static class DelegationRoles
    {
        /// <summary>Le serveur de la sélection, ou null.</summary>
        public static Delegation Agent(IEnumerable<SemantizationCore> selection)
            => selection?
                .Where(o => o != null)
                .Select(o => o.GetComponent<Delegation>())
                .FirstOrDefault(w => w != null);

        /// <summary>La table de la sélection, ou null.</summary>
        public static SemantizationCore Table(IEnumerable<SemantizationCore> selection)
            => Annotated(selection, "sven:Table");

        private static SemantizationCore Annotated(
            IEnumerable<SemantizationCore> selection, string semanticType)
            => selection?
                .Where(o => o != null
                            && o.TryGetComponent(out SemanticAnnotator annotator)
                            && annotator.Annotations.Contains(semanticType))
                .FirstOrDefault();

        /// <summary>
        /// Tous les objets désignés, quel que soit le SelectionParameter qui les porte. Le
        /// mode RuleBased en produit un seul, le LLM deux : la résolution par rôle rend la
        /// différence sans conséquence, à condition de regarder partout.
        /// </summary>
        public static List<SemantizationCore> AllTargets(Command command)
            => command.Parameters?
                .OfType<SelectionParameter>()
                .SelectMany(p => p.Objects ?? new List<SemantizationCore>())
                .Distinct()
                .ToList()
               ?? new List<SemantizationCore>();
    }
}
