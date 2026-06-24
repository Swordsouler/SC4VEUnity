using Sc4ve.Voice;
using System;
using System.Collections.Generic;
using System.Linq;

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

        public IReadOnlyList<RuleBasedColor> SourceColors =>
            Colors?.Where(c => !c.IsTarget).ToList() ?? new List<RuleBasedColor>();
        public IReadOnlyList<RuleBasedColor> TargetColors =>
            Colors?.Where(c =>  c.IsTarget).ToList() ?? new List<RuleBasedColor>();

        // ──────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────

        // Mots indiquant une destination spatiale (« mets ça ici », « déplace-le là-bas »).
        private static readonly string[] DestinationWords =
        {
            "ici", "là-bas", "là-haut", "là", "dessus", "dessous",
            "devant", "derrière", "à droite", "à gauche"
        };

        /// <summary>Vrai si la phrase indique une destination spatiale (pour Move/Duplicate).</summary>
        public bool HasDestination =>
            Text != null && DestinationWords.Any(w => Text.Contains(w, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Construit le SelectionParameter standard à partir des entités extraites.
        /// <paramref name="useStartedAt"/> = true pour MoveCommand (source pointée avant de parler).
        /// </summary>
        public SelectionParameter BuildSelectionParameter(bool useStartedAt = false)
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

            return new SelectionParameter { Type = "SelectionParameter", Filters = filters, Limit = Limit };
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
