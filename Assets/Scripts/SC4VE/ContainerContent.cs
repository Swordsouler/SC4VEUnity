using Sven.Content;
using Sven.GraphManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VDS.RDF;

namespace Sc4ve.Multimodality
{
    /// <summary>
    /// Ce qu'un contenant contient, exposé au graphe RDF.
    ///
    /// SVEN ne modélise PAS la contenance : sven:Transform porte position, rotation et
    /// échelle, et rien qui relie un objet à son parent ou à son contenant. Sans cette
    /// relation, aucune requête ne peut dire ce qu'il y a dans l'assiette — et toute la
    /// vérification de conformité des recettes s'effondre (§6.3 du README).
    ///
    /// Le motif est celui de SemanticAnnotator : une liste exposée en ComponentProperty,
    /// dont le délégué d'assertion émet un triplet par élément. La seule différence est que
    /// les valeurs sont des URI d'objets (:uuid) et non des URI de classes (sven:Apple).
    ///
    /// À poser sur tout sven:Container — assiette, poubelle, station — et à sémantiser en
    /// **Dynamic** : le contenu change en cours de partie, et en Static il serait figé au
    /// démarrage.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(SemantizationCore))]
    public class ContainerContent : MonoBehaviour, IComponentMapping
    {
        private readonly List<SemantizationCore> _content = new();

        /// <summary>Les objets actuellement contenus.</summary>
        public IReadOnlyList<SemantizationCore> Content => _content;

        /// <summary>Ajoute un objet. Sans effet s'il est déjà là. Vrai si le contenu a changé.</summary>
        public bool Add(SemantizationCore obj)
        {
            if (obj == null || _content.Contains(obj)) return false;
            _content.Add(obj);
            return true;
        }

        /// <summary>Retire un objet. Vrai si le contenu a changé.</summary>
        public bool Remove(SemantizationCore obj) => obj != null && _content.Remove(obj);

        public bool Contains(SemantizationCore obj) => obj != null && _content.Contains(obj);

        /// <summary>Vide le contenant. Vrai si le contenu a changé.</summary>
        public bool Clear()
        {
            if (_content.Count == 0) return false;
            _content.Clear();
            return true;
        }

        public static ComponentMapping ComponentMapping()
        {
            return new("ContainerContent",
                new List<Delegate>
                {
                    (Func<ContainerContent, ComponentProperty>)(container => new ComponentProperty(
                        "enabled",
                        () => container.enabled,
                        value => container.enabled = value.ToString() == "true",
                        1)),

                    (Func<ContainerContent, ComponentProperty>)(container => new ComponentProperty(
                        "content",
                        // La chaîne sert à la détection de changement : dès qu'un objet entre
                        // ou sort, elle diffère et SVEN ouvre un nouvel intervalle temporel.
                        () => string.Join(",", container._content.Select(o => o.GetUUID())),
                        // Rejeu non pris en charge : reconstituer un contenu depuis le graphe
                        // suppose de retrouver les objets par UUID au moment du rejeu, ce que
                        // le mini-jeu ne fait pas. À implémenter le jour où l'on rejouera une
                        // partie enregistrée.
                        _ => { },
                        1,
                        propertyNode =>
                        {
                            foreach (SemantizationCore obj in container._content)
                            {
                                if (obj == null) continue;
                                GraphManager.Assert(new Triple(
                                    propertyNode,
                                    GraphManager.CreateUriNode("sven:value"),
                                    GraphManager.CreateUriNode(":" + obj.GetUUID())));
                            }
                        })),
                });
        }
    }
}
