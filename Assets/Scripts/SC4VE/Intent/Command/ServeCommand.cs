using Sven.Content;
using System;
using System.Collections.Generic;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers(
        "va servir", "sers", "servir", "apporte le plat", "porte à",
        "go serve", "serve", "bring the dish")]
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
            => new() { ctx.BuildSelectionParameter(fallbackToSelection: true) };

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> targets = DelegationRoles.AllTargets(this);
            Waiter agent = DelegationRoles.Agent(targets);
            SemantizationCore table = DelegationRoles.Table(targets);

            bool french = UserData.Locale == "fr";

            if (agent == null)
            {
                Speak(french ? "Quel serveur ?" : "Which waiter?");
                return new();
            }

            if (table == null)
            {
                Speak(french ? "Quelle table ?" : "Which table?");
                return new();
            }

            // Serve parle lui-même en cas de refus (occupé) ou d'échec (aucun plat prêt) :
            // un échec est une information, pas un bug (§8 du README).
            if (!agent.Serve(table)) return new();

            return new List<SemantizationCore> { agent.GetComponent<SemantizationCore>() };
        }
    }
}
