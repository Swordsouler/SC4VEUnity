using Sven.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers(
        // « fais », « faire » et « make » sont volontairement absents : trop généraux, ils
        // entraient en collision avec ColorizeCommand (« make it red »). RuleBasedIntentRecognizer
        // les traite par une pré-vérification qui exige la présence d'un nom de recette.
        "prépare", "préparer", "prepare", "cuisine", "cuisiner", "cook")]
    [Serializable, CommandDescription(
        "Annonce la recette que le joueur veut préparer, et rappelle à voix haute ce qu'elle " +
        "exige. Ne fabrique rien : c'est le joueur qui assemble les ingrédients. " +
        "Générer pour « prépare une salade de fruits », « fais une soupe de carottes ». " +
        "Paramètre: RecipeParameter (la recette). Si la phrase ne nomme qu'une famille de plats " +
        "(« prépare une soupe »), la commande demande laquelle.")]
    public class PrepareCommand : Command
    {
        private RecipeParameter RecipeParameter => GetParameter<RecipeParameter>();

        /// <summary>
        /// La recette en cours. CheckCommand la vérifie par défaut quand la question n'en
        /// nomme aucune, et le reste du jeu la consulte. Une seule à la fois : le joueur
        /// prépare un plat, puis le suivant.
        /// </summary>
        public static string CurrentRecipe { get; private set; }

        /// <summary>
        /// Aucun SelectionParameter : une recette ne se pointe pas. C'est le seul énoncé du jeu
        /// qui soit intégralement linguistique, et c'est voulu (§3 du README) — il fait
        /// contrepoids aux commandes qui exigent un geste.
        /// </summary>
        public override List<Parameter> BuildRuleBasedParameters(RuleBasedContext ctx)
        {
            if (ctx.Recipe == null) return new List<Parameter>();
            return new List<Parameter>
            {
                new RecipeParameter { Type = "RecipeParameter", Value = ctx.Recipe }
            };
        }

        public override List<SemantizationCore> Execute()
        {
            string recipe = RecipeParameter?.Value;
            if (recipe == null)
            {
                // Le paramètre manquant déclenche normalement la clarification déclarée dans
                // sc4ve.ttl ; ce repli couvre le cas où la commande arrive quand même vide.
                Speak(UserData.Locale == "fr" ? "Quelle recette ?" : "Which recipe?");
                return new();
            }

            _ = Announce(recipe);
            return new();
        }

        private static async Task Announce(string recipe)
        {
            try
            {
                bool french = UserData.Locale == "fr";
                List<RecipeVocabulary.Recipe> known =
                    await RecipeVocabulary.GetAvailableRecipesAsync(UserData.Locale);

                RecipeVocabulary.Recipe match = known.FirstOrDefault(r => r.Uri == recipe);

                // Recette inconnue de l'ontologie — un LLM peut inventer « sven:PizzaMargherita ».
                // Sans ce garde-fou, CurrentRecipe prenait cette valeur fantôme et la commande
                // annonçait « … : il faut . », la liste d'exigences étant vide.
                if (match.Uri == null)
                {
                    Debug.LogWarning($"[Prepare] Recette inconnue : {recipe}");
                    Speak(french ? "Je ne connais pas cette recette." : "I do not know that recipe.");
                    return;
                }

                // Famille de plats plutôt que recette précise : « prépare une soupe » désigne
                // trois candidates. On demande laquelle au lieu d'en choisir une arbitrairement
                // — c'est exactement la clarification que le système sait le mieux montrer.
                if (!match.IsConcrete)
                {
                    List<string> children = await ConcreteChildren(recipe, known);
                    string list = string.Join(french ? " ou " : " or ", children);
                    Speak(french ? $"Laquelle : {list} ?" : $"Which one: {list}?");
                    return;
                }

                CurrentRecipe = recipe;

                List<RecipeConformity.Requirement> requirements =
                    await RecipeConformity.Requirements(recipe);

                string label = match.Label ?? recipe;
                string ingredients = string.Join(", ", requirements.Select(r => r.ToString()));

                Debug.Log($"[Prepare] Recette en cours : {label} — {ingredients}");
                Speak(french
                    ? $"{label} : il faut {ingredients}."
                    : $"{label}: you need {ingredients}.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Prepare] Recette illisible : {e}");
            }
        }

        /// <summary>Les recettes concrètes d'une famille — les trois soupes, par exemple.</summary>
        private static async Task<List<string>> ConcreteChildren(
            string family, List<RecipeVocabulary.Recipe> known)
        {
            var children = new List<string>();
            // GroupBy sur l'URI : GetAvailableRecipesAsync crée UN Recipe par couple
            // (uri, label), et sven:PumpkinSoup porte deux labels @fr — sans ce
            // dédoublonnage, la clarification énoncerait QUATRE soupes pour trois, à
            // l'instant exact où le critère 7 du lot 4 est mis en scène.
            foreach (RecipeVocabulary.Recipe candidate in known.Where(r => r.IsConcrete)
                                                               .GroupBy(r => r.Uri)
                                                               .Select(g => g.First()))
                if (await RecipeVocabulary.IsSubClassOf(candidate.Uri, family))
                    children.Add(candidate.Label);
            return children;
        }
    }
}
