using Sven.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers(
        "prépare", "préparer", "prepare", "fais", "faire", "cuisine", "cuisiner", "make", "cook")]
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
        /// La recette en cours, celle que CheckCommand vérifiera par défaut et que le reste du
        /// jeu consultera. Une seule à la fois : le joueur prépare un plat, puis le suivant.
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

                // Famille de plats plutôt que recette précise : « prépare une soupe » désigne
                // trois candidates. On demande laquelle au lieu d'en choisir une arbitrairement
                // — c'est exactement la clarification que le système sait le mieux montrer.
                if (match.Uri != null && !match.IsConcrete)
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
            foreach (RecipeVocabulary.Recipe candidate in known.Where(r => r.IsConcrete))
                if (await RecipeVocabulary.IsSubClassOf(candidate.Uri, family))
                    children.Add(candidate.Label);
            return children;
        }
    }
}
