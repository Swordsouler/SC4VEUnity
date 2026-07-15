using Sven.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sc4ve.Multimodality.Intent
{
    /// <summary>
    /// Résultat de l'analyse d'une phrase par le RuleBasedIntentRecognizer.
    /// Passé à <see cref="Command.BuildRuleBasedParameters"/> pour construire les paramètres.
    /// </summary>
    public class RuleBasedContext
    {
        public string               Text            { get; init; }
        public IReadOnlyList<Word>  Words           { get; init; }
        public string               PointerName     { get; init; }
        public int                  MovePointDelayMs { get; init; }

        public IReadOnlyList<RuleBasedAnnotation> Annotations  { get; init; }
        public IReadOnlyList<RuleBasedColor>      Colors       { get; init; }
        public IReadOnlyList<RuleBasedAnnotation> Deictics     { get; init; }
        public bool HasCoreference { get; init; }
        public int  Limit          { get; init; }
        // Facteur d'échelle explicite (« double » → 2, « triple » → 3) ; 0 = non spécifié.
        public float ScaleFactor   { get; init; }
        // Taille absolue cible (« mets la taille à 50 » → 50) ; 0 = non spécifié.
        public float ScaleValue    { get; init; }
        // Référence au singulier (« la pomme ») → candidate à la désambiguïsation si plusieurs cibles.
        public bool SingularIntent { get; init; }

        public IReadOnlyList<RuleBasedColor> SourceColors =>
            Colors?.Where(c => !c.IsTarget).ToList() ?? new List<RuleBasedColor>();
        public IReadOnlyList<RuleBasedColor> TargetColors =>
            Colors?.Where(c =>  c.IsTarget).ToList() ?? new List<RuleBasedColor>();

        // ──────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────

        // Mots indiquant une destination spatiale (« mets ça ici » / « put it here »), par langue.
        private static readonly string[] DestinationWordsFr =
        {
            "ici", "là-bas", "là-haut", "là", "dessus", "dessous",
            "devant", "derrière", "à droite", "à gauche"
        };
        private static readonly string[] DestinationWordsEn =
        {
            "here", "there", "over there", "up there", "on top", "underneath",
            "in front", "behind", "to the right", "to the left"
        };

        /// <summary>
        /// Vrai si la phrase indique une destination spatiale (pour Move/Duplicate). Frontière de
        /// mot pour éviter les faux positifs (« sphere » ne contient pas le déclencheur « here »).
        /// </summary>
        public bool HasDestination
        {
            get
            {
                if (string.IsNullOrEmpty(Text)) return false;
                // Ablation (benchmark) : sans pointage, pas de destination (« ici » = au pointeur).
                if (!MultimodalitySettings.PointingEnabled) return false;
                bool fr = string.IsNullOrEmpty(UserData.Locale) || UserData.Locale.StartsWith("fr");
                string[] words = fr ? DestinationWordsFr : DestinationWordsEn;
                return words.Any(w => Regex.IsMatch(Text, $@"\b{Regex.Escape(w)}\b", RegexOptions.IgnoreCase));
            }
        }

        /// <summary>
        /// Construit le SelectionParameter standard à partir des entités extraites.
        /// <paramref name="useStartedAt"/> = true pour MoveCommand (source pointée avant de parler).
        /// <paramref name="fallbackToSelection"/> = true : si la cible (déictique/pointage, ou
        /// absence de cible) résout à vide, on retombe sur la sélection courante au lieu de laisser
        /// le paramètre vide — pour les commandes qui transforment l'existant (Move, Duplicate).
        /// Une cible explicite PAR TYPE (annotation/couleur) désactive ce repli.
        /// </summary>
        public SelectionParameter BuildSelectionParameter(bool useStartedAt = false, bool fallbackToSelection = false)
        {
            var filters = new List<FilterElement>();

            if (HasCoreference)
            {
                filters.Add(new FilterElement
                {
                    IsOperator = false,
                    Condition  = new Condition { Type = "Coreference" }
                });
            }
            else
            {
                bool needsOp = false;

                foreach (RuleBasedAnnotation d in Deictics ?? Enumerable.Empty<RuleBasedAnnotation>())
                {
                    if (needsOp) filters.Add(new FilterElement { IsOperator = true, Operator = "AND" });
                    filters.Add(new FilterElement
                    {
                        IsOperator = false,
                        Condition  = new Condition { Type = "Event", Value = d.Value, Timestamp = d.Timestamp }
                    });
                    needsOp = true;
                }

                foreach (RuleBasedAnnotation a in Annotations ?? Enumerable.Empty<RuleBasedAnnotation>())
                {
                    if (needsOp) filters.Add(new FilterElement { IsOperator = true, Operator = "AND" });
                    filters.Add(new FilterElement
                    {
                        IsOperator = false,
                        Condition  = new Condition { Type = "Annotation", Value = a.Value, Timestamp = a.Timestamp }
                    });
                    needsOp = true;
                }

                foreach (RuleBasedColor c in SourceColors)
                {
                    if (needsOp) filters.Add(new FilterElement { IsOperator = true, Operator = "AND" });
                    filters.Add(new FilterElement
                    {
                        IsOperator = false,
                        Condition  = new Condition { Type = "Color", Value = c.Value, Timestamp = c.Timestamp }
                    });
                    needsOp = true;
                }
            }

            // Repli sur la sélection courante (Move/Duplicate) : posé si AUCUNE cible explicite
            // PAR TYPE (annotation/couleur) n'est donnée. Un déictique (« ça ») ou l'absence de
            // cible peuvent retomber sur la sélection quand le pointage résout à vide ; « les
            // pommes » non (cible explicite → NoMatch si aucune pomme). Résolu dans Semanticize.
            bool explicitTypeTarget = (Annotations?.Count > 0) || (SourceColors?.Count > 0);

            return new SelectionParameter
            {
                Type = "SelectionParameter",
                Filters = filters,
                Limit = Limit,
                FallbackToSelection = fallbackToSelection && !explicitTypeTarget,
                SingularIntent = SingularIntent
            };
        }

        /// <summary>
        /// Construit le PointParameter pour GrabCommand :
        /// utilise le timestamp du premier déictique si présent, sinon la fin de phrase.
        /// </summary>
        public PointParameter BuildGrabPointParameter()
        {
            DateTime grabTs = Deictics?.Count > 0
                ? Deictics[0].Timestamp
                : (Words?.Count > 0 ? Words[Words.Count - 1].EndedAt : DateTime.Now);
            return new PointParameter { Type = "PointParameter", Value = PointerName, Timestamp = grabTs };
        }

        /// <summary>
        /// Construit le PointParameter de destination (fin de phrase + délai configurable).
        /// </summary>
        public PointParameter BuildDestinationParameter()
        {
            DateTime sentenceEnd = Words?.Count > 0 ? Words[^1].EndedAt : DateTime.Now;
            return new PointParameter
            {
                Type      = "PointParameter",
                Value     = PointerName,
                Timestamp = sentenceEnd.AddMilliseconds(MovePointDelayMs)
            };
        }
    }

    public struct RuleBasedAnnotation
    {
        public string   Value;
        public DateTime Timestamp;
    }

    public struct RuleBasedColor
    {
        public string   Value;
        public DateTime Timestamp;
        public bool     IsTarget;
    }
}
