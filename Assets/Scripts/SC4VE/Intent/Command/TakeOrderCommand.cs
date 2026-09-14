using Sven.Content;
using System;
using System.Collections.Generic;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers(
        // « prend la commande » SANS s : Whisper transcrit volontiers l'impératif « prends »
        // en 3e personne. Sans cette forme, aucun déclencheur multi-mots ne matchait et le
        // stem « prend » (= prendre) routait vers GrabCommand — qui tentait de saisir la table.
        "prends la commande", "prend la commande", "prenez la commande",
        "prendre la commande", "va prendre la commande",
        // « chercher » plutôt que « prendre » : on va chercher une commande comme on va la
        // prendre, et rien d'autre dans le jeu ne se « cherche ».
        "va chercher la commande", "cherche la commande", "chercher la commande",
        "take the order", "go take the order", "go get the order", "get the order")]
    [Serializable, CommandDescription(
        "Envoie un serveur prendre la commande d'une table (« va prendre la commande de cette " +
        "table-là »). Le serveur s'y rend ; le client ne parle qu'une fois qu'il est arrivé. " +
        "Paramètres: DEUX SelectionParameter — le serveur et la table. Leur ORDRE est " +
        "indifférent : les rôles se lisent dans les objets désignés.")]
    public class TakeOrderCommand : Command
    {
        /// <summary>
        /// Même forme que ServeCommand, dont elle ne diffère que par la macro exécutée : si
        /// l'une marche, l'autre marche. Elles sont écrites ensemble à dessein.
        /// </summary>
        public override List<Parameter> BuildRuleBasedParameters(RuleBasedContext ctx)
            => new() { ctx.BuildSelectionParameter(fallbackToSelection: true) };

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> targets = DelegationRoles.AllTargets(this);
            Delegation agent = DelegationRoles.Agent(targets);
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

            if (!agent.TakeOrder(table)) return new();

            return new List<SemantizationCore> { agent.GetComponent<SemantizationCore>() };
        }
    }
}
