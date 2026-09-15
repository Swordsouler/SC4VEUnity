using Sven.Content;
using System;
using System.Collections.Generic;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers(
        // « donne » et « apporte » : les formulations naturelles du service, entendues en
        // démo (« Donne la salade César à Florence ») — sans elles, la phrase partait dans
        // le repli plat-nommé et faisait REFAIRE le plat au lieu de le servir. « amène »
        // reste à MoveCommand (« amène ça ici 👆 »).
        "va servir", "sers", "servir", "donne", "donner", "donnez",
        "apporte", "apporter", "porte à",
        "go serve", "serve", "give", "bring")]
    [Serializable, CommandDescription(
        "Envoie un serveur porter un plat prêt jusqu'à une table (« toi, va servir cette " +
        "table-là »). Le serveur prend l'assiette pleine la plus proche de la passe, la porte " +
        "jusqu'à la table et l'y dépose. " +
        "Paramètres: DEUX SelectionParameter — le serveur et la table. Leur ORDRE est " +
        "indifférent : les rôles se lisent dans les objets désignés.")]
    public class ServeCommand : Command
    {
        /// <summary>
        /// Une seule sélection en RuleBased, contenant les deux pointages.
        ///
        /// Contrairement à PutInCommand, aucun mot ne sépare l'agent de la destination — « toi
        /// 👆 va servir cette table-là 👆 » n'a pas de préposition pivot. Découper par
        /// horodatage supposerait donc de deviner où couper. On ne coupe pas : Execute lit les
        /// rôles dans les objets (DelegationRoles), ce que la phrase ne dit de toute façon pas.
        /// </summary>
        public override List<Parameter> BuildRuleBasedParameters(RuleBasedContext ctx)
        {
            var parameters = new List<Parameter> { ctx.BuildSelectionParameter(fallbackToSelection: true) };

            // Le plat NOMMÉ (« donne la salade César à Florence ») voyage avec l'ordre : le
            // serveur choisira l'assiette préparée pour CETTE recette plutôt que la plus
            // proche de la passe (Delegation.FindReadyDish).
            if (ctx.Recipe != null)
                parameters.Add(new RecipeParameter { Type = "RecipeParameter", Value = ctx.Recipe });

            return parameters;
        }

        /// <summary>La réponse attendue désigne une cible (« cette table-là 👆 »), pas un paramètre.</summary>
        public override bool ExpectsTargetAnswer => true;

        public override List<SemantizationCore> Execute()
        {
            (Delegation agent, SemantizationCore table, List<SemantizationCore> known) =
                DelegationRoles.Resolve(this);

            bool french = UserData.Locale == "fr";

            // Rôle manquant : même schéma que TakeOrderCommand — on demande, et on renvoie ce
            // qui est déjà connu pour que la réponse s'y unisse au lieu de le remplacer.
            if (agent == null)
            {
                Ask(french ? "Quel serveur ?" : "Which waiter?");
                return known;
            }

            if (table == null)
            {
                Ask(french ? "Quelle table ?" : "Which table?");
                return known;
            }

            // La recette EXPLICITE ne vaut que pour une cible unique : « donne la salade
            // César à Florence, la salade de fruits à Patricia… » nomme plusieurs plats, et
            // le premier extrait s'appliquerait à tous. À plusieurs tables, recette nulle :
            // chaque service choisit le plat que SON client attend (Delegation.Serve).
            List<SemantizationCore> tables = DelegationRoles.Tables(this);
            string recipe = tables.Count > 1 ? null : GetParameter<RecipeParameter>()?.Value;

            // Serve parle lui-même en cas de refus (occupé) ou d'échec (aucun plat prêt) :
            // un échec est une information, pas un bug (§8 du README).
            if (!agent.Serve(table, recipe)) return new();

            // Liste de directives (« sers Jean et Florence ») : les tables au-delà de la
            // première s'enfilent — le serveur unique les sert en séquence (Delegation).
            foreach (SemantizationCore extra in tables)
                if (extra != table)
                    agent.Enqueue(() => agent.Serve(extra, recipe));

            return new List<SemantizationCore> { agent.GetComponent<SemantizationCore>() };
        }
    }
}
