using Sven.Content;
using Sven.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers(
        "mets dans", "met dans", "mettre dans", "mets sur", "met sur", "mettre sur",
        "range dans", "ranger dans", "pose dans", "poser dans", "pose sur", "poser sur",
        "dépose dans", "déposer dans", "déposez dans", "dépose sur", "déposer sur", "déposez sur",
        "ajoute dans", "ajouter dans", "ajoute à", "verse dans", "verser dans",
        "put in", "put into", "put on", "place in", "place into", "place on", "add to")]
    [Serializable, CommandDescription(
        "Met des objets dans un contenant — assiette, poubelle ou station de transformation. " +
        "Générer quand la phrase relie des objets à un contenant par « dans » ou « sur » " +
        "(« mets tous les fruits rouges dans le saladier », « mets le steak sur la plaque »). " +
        "Paramètres: DEUX SelectionParameter, dans cet ordre — le PREMIER désigne les objets à " +
        "déplacer, le SECOND désigne le contenant qui les reçoit.")]
    public class PutInCommand : Command
    {
        /// <summary>
        /// Prépositions qui séparent les objets du contenant. Le mot prononcé sert de pivot :
        /// ce qui est dit avant désigne les objets, ce qui est dit après désigne le contenant.
        /// </summary>
        private static readonly string[] Pivots =
        {
            "dans", "sur", "dedans", "into", "onto", "in", "on", "to",
        };

        private SelectionParameter ContainerParameter => GetParameter<SelectionParameter>(2);

        /// <summary>
        /// Découpe la phrase en deux zones référentielles autour de la préposition.
        ///
        /// C'est le seul endroit du moteur RuleBased où une phrase porte DEUX cibles
        /// distinctes. Plutôt que de reconstruire un contexte filtré — ce qui coupleraient
        /// cette commande à tous les champs de RuleBasedContext — on laisse le contexte
        /// construire la sélection complète, puis on partage sa liste de filtres selon
        /// l'horodatage de chaque condition.
        /// </summary>
        public override List<Parameter> BuildRuleBasedParameters(RuleBasedContext ctx)
        {
            SelectionParameter full = ctx.BuildSelectionParameter();
            DateTime? pivot = FindPivot(ctx);

            if (pivot == null)
            {
                // Pas de préposition : on ne sait pas où s'arrêtent les objets et où commence
                // le contenant. On rend la sélection telle quelle ; le contenant manquant
                // déclenchera une demande de clarification plutôt qu'une action silencieuse.
                return new List<Parameter> { full };
            }

            SelectionParameter objects = WithFilters(full, TakeFilters(full, t => t < pivot.Value));
            SelectionParameter container = WithFilters(full, TakeFilters(full, t => t >= pivot.Value));

            // Un contenant est toujours singulier : « dans le saladier », jamais « dans les
            // saladiers ». S'il y en a plusieurs de candidats, on demande lequel.
            container.SingularIntent = true;
            container.FallbackToSelection = false;

            return new List<Parameter> { objects, container };
        }

        /// <summary>Horodatage de la première préposition prononcée, ou null s'il n'y en a pas.</summary>
        private static DateTime? FindPivot(RuleBasedContext ctx)
        {
            if (ctx.Words == null) return null;

            foreach (Word word in ctx.Words)
            {
                string text = word.Text?.Trim().ToLowerInvariant().Trim('.', ',', ';', '!', '?', '\'');
                if (!string.IsNullOrEmpty(text) && Pivots.Contains(text))
                    return word.StartedAt;
            }
            return null;
        }

        /// <summary>
        /// Conserve les conditions dont l'horodatage satisfait <paramref name="keep"/>, en
        /// rétablissant les opérateurs entre celles qui restent — sans quoi « les pommes OU les
        /// bananes dans l'assiette » perdrait sa disjonction.
        /// </summary>
        private static List<FilterElement> TakeFilters(SelectionParameter full, Func<DateTime, bool> keep)
        {
            var result = new List<FilterElement>();
            string pendingOperator = null;

            foreach (FilterElement element in full.Filters ?? new List<FilterElement>())
            {
                if (element.IsOperator)
                {
                    pendingOperator = element.Operator;
                    continue;
                }

                if (element.Condition == null || !keep(element.Condition.Timestamp))
                {
                    pendingOperator = null;
                    continue;
                }

                if (result.Count > 0)
                    result.Add(new FilterElement { IsOperator = true, Operator = pendingOperator ?? "AND" });

                result.Add(element);
                pendingOperator = null;
            }

            return result;
        }

        private static SelectionParameter WithFilters(SelectionParameter model, List<FilterElement> filters)
            => new()
            {
                Type = "SelectionParameter",
                Filters = filters,
                Limit = model.Limit,
                Order = model.Order,
                FallbackToSelection = model.FallbackToSelection,
                SingularIntent = model.SingularIntent
            };

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter?.Objects ?? new();
            List<SemantizationCore> candidates = ContainerParameter?.Objects ?? new();

            if (objects.Count == 0) return new();

            ContainerContent container = candidates
                .Where(c => c != null)
                .Select(c => c.GetComponent<ContainerContent>())
                .FirstOrDefault(c => c != null);

            if (container == null)
            {
                Debug.LogWarning("[PutIn] Aucun contenant dans la cible : " +
                                 (candidates.Count == 0
                                     ? "aucun objet désigné après la préposition."
                                     : "l'objet désigné ne porte pas de ContainerContent."));
                return new();
            }

            // On ne met pas un contenant dans lui-même.
            objects = objects.Where(o => o != null && o.gameObject != container.gameObject).ToList();

            return ExecuteReversible(objects, obj =>
            {
                Transform t = obj.transform;
                Transform previousParent = t.parent;
                Vector3 previousPosition = t.position;

                Vector3 target = DropPosition(container, obj);

                // Un objet n'est que dans un seul contenant à la fois : sans ce retrait, une
                // pomme passée de la planche à l'assiette figurerait dans les deux, et la
                // planche continuerait de la « couper » indéfiniment.
                ContainerContent previousContainer = ContainerOf(obj);

                void Put()
                {
                    previousContainer?.Remove(obj);
                    t.SetParent(container.transform);
                    t.position = target;
                    Rest(obj);
                    container.Add(obj);
                }

                void Undo()
                {
                    container.Remove(obj);
                    previousContainer?.Add(obj);
                    t.SetParent(previousParent);
                    t.position = previousPosition;
                }

                Put();
                Debug.Log($"[PutIn] {obj.GetUUID()} placé dans {container.name}.");
                return (Undo, Put);
            });
        }

        /// <summary>
        /// Empile les objets au-dessus du contenant plutôt que de les superposer au même point :
        /// un plat à moitié fait doit rester lisible, c'est un état de jeu à part entière.
        /// </summary>
        private static Vector3 DropPosition(ContainerContent container, SemantizationCore obj)
        {
            Bounds bounds = container.GetComponent<Renderer>() != null
                ? container.GetComponent<Renderer>().bounds
                : new Bounds(container.transform.position, Vector3.zero);

            float height = bounds.extents.y + 0.03f + container.Content.Count * 0.04f;
            return new Vector3(container.transform.position.x,
                               bounds.center.y + height,
                               container.transform.position.z);
        }

        /// <summary>Le contenant qui détient actuellement cet objet, s'il y en a un.</summary>
        private static ContainerContent ContainerOf(SemantizationCore obj)
            => UnityEngine.Object
                .FindObjectsByType<ContainerContent>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.Contains(obj));

        /// <summary>Coupe l'élan de l'objet, sinon il roule hors du contenant juste après y avoir été posé.</summary>
        private static void Rest(SemantizationCore obj)
        {
            if (!obj.TryGetComponent(out Rigidbody body)) return;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }
}
