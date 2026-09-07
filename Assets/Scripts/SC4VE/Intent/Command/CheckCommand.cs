using Sven.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers(
        "est-ce que c'est prêt", "est-ce que c'est bon", "c'est prêt", "est-ce conforme",
        "est-ce que c'est conforme", "vérifie", "vérifier", "contrôle", "contrôler",
        "qu'est-ce que c'est", "is it ready", "is this ready", "is it correct", "check")]
    [Serializable, CommandDescription(
        "Vérifie qu'un contenant satisfait une recette, et énonce le verdict à voix haute. " +
        "Générer pour « est-ce que c'est prêt ? », « est-ce que c'est une salade de fruits ? », " +
        "« qu'est-ce que c'est ? ». " +
        "Paramètres: SelectionParameter (le contenant à inspecter) et, si la phrase la nomme, " +
        "RecipeParameter (la recette attendue). Sans RecipeParameter, toutes les recettes sont " +
        "essayées et le plat reconnu est annoncé.")]
    public class CheckCommand : Command
    {
        private RecipeParameter RecipeParameter => GetParameter<RecipeParameter>();

        public override List<Parameter> BuildRuleBasedParameters(RuleBasedContext ctx)
        {
            var parameters = new List<Parameter>
            {
                // Repli sur la sélection courante : « est-ce que c'est prêt ? » après avoir
                // rempli une assiette ne renomme pas le contenant.
                ctx.BuildSelectionParameter(fallbackToSelection: true)
            };

            if (ctx.Recipe != null)
                parameters.Add(new RecipeParameter { Type = "RecipeParameter", Value = ctx.Recipe });

            return parameters;
        }

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> targets = SelectionParameter?.Objects ?? new();
            SemantizationCore container = targets.FirstOrDefault(
                o => o != null && o.GetComponent<ContainerContent>() != null);

            if (container == null)
            {
                Speak(UserData.Locale == "fr" ? "Quel plat ?" : "Which dish?");
                return new();
            }

            // Le verdict arrive de façon asynchrone : la vérification interroge le graphe.
            // On ne bloque pas — la réponse vocale suit d'elle-même quelques instants plus tard.
            _ = Announce(container, RecipeParameter?.Value);

            return new List<SemantizationCore> { container };
        }

        private static async System.Threading.Tasks.Task Announce(SemantizationCore container, string recipe)
        {
            try
            {
                if (recipe != null)
                {
                    RecipeConformity.Report report = await RecipeConformity.Check(container, recipe);
                    Debug.Log($"[Check] {report}");
                    Speak(Verdict(report));
                    return;
                }

                // Aucune recette nommée : on cherche laquelle est satisfaite. C'est ce qui rend
                // « qu'est-ce que c'est ? » répondable, et c'est aussi ce dont le client aura
                // besoin pour accepter ou refuser un plat.
                List<RecipeVocabulary.Recipe> recipes =
                    await RecipeVocabulary.GetAvailableRecipesAsync(UserData.Locale);

                foreach (RecipeVocabulary.Recipe candidate in recipes.Where(r => r.IsConcrete))
                {
                    RecipeConformity.Report report = await RecipeConformity.Check(container, candidate.Uri);
                    if (!report.IsConformant) continue;

                    Debug.Log($"[Check] Plat reconnu : {candidate.Label}");
                    Speak(UserData.Locale == "fr"
                        ? $"C'est {candidate.Label}."
                        : $"This is {candidate.Label}.");
                    return;
                }

                Speak(UserData.Locale == "fr"
                    ? "Ça ne correspond à aucune recette."
                    : "This matches no recipe.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Check] Vérification impossible : {e}");
            }
        }

        private static string Verdict(RecipeConformity.Report report)
        {
            bool french = UserData.Locale == "fr";
            if (report.IsConformant) return french ? "Oui, c'est prêt." : "Yes, it is ready.";

            var reasons = new List<string>();
            if (report.Missing.Count > 0)
                reasons.Add((french ? "il manque " : "missing ") + string.Join(", ", report.Missing));
            if (report.Extra.Count > 0)
                reasons.Add((french ? "il y a en trop " : "extra ") + string.Join(", ", report.Extra));

            return (french ? "Non : " : "No: ") + string.Join(" et ", reasons) + ".";
        }
    }
}
