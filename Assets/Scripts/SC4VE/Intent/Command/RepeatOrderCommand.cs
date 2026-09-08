using Sven.Content;
using System;
using System.Collections.Generic;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers(
        // Aucun déclencheur mono-mot : « commande » seul entrerait en collision frontale avec
        // TakeOrderCommand. Toutes les formes sont multi-mots et portent leur verbe ;
        // « c'est quoi la commande » (22) bat « c'est quoi » (10, DescribeCommand) parce que
        // DetectCommandType compare du plus long au plus court.
        "répète la commande", "répéter la commande", "redis la commande",
        "rappelle la commande", "quelle est la commande", "c'est quoi la commande",
        "repeat the order", "say the order again", "what is the order")]
    [Serializable, CommandDescription(
        "Réénonce à voix haute la commande DÉJÀ prise à une table, sans envoyer de serveur " +
        "et sans rien changer au monde. Générer pour « répète la commande de cette table », " +
        "« c'est quoi la commande ? ». Paramètre: UN SelectionParameter — la table, ou le " +
        "client, désigné au pointage. Ne PAS générer pour « va prendre la commande » : c'est " +
        "TakeOrderCommand, qui envoie un serveur.")]
    public class RepeatOrderCommand : Command
    {
        /// <summary>
        /// Pas de BuildRuleBasedParameters : le défaut de Command — un SelectionParameter avec
        /// repli sur la sélection courante — est exactement ce qu'il faut. « Répète » suit
        /// souvent un « va prendre la commande de cette table 👆 » qui a laissé la table
        /// sélectionnée.
        ///
        /// Elle est instantanée, pas déléguée, et c'est un choix du README (§7) : on pourrait
        /// renvoyer un serveur demander au client — plus cohérent avec la fiction — mais ce
        /// serait punir un besoin légitime. Un visiteur qui n'a pas compris n'a pas commis
        /// d'erreur de jeu. C'est une commande d'accessibilité autant que de confort.
        /// </summary>
        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> targets = DelegationRoles.AllTargets(this);
            CustomerOrder customer = DelegationRoles.Customer(targets);

            bool french = UserData.Locale == "fr";

            if (customer == null)
            {
                Speak(french ? "Quelle table ?" : "Which table?");
                return new();
            }

            if (!customer.Repeat())
                Speak(french
                    ? "Cette table n'a pas encore commandé."
                    : "That table has not ordered yet.");

            // La TABLE et non le client, et jamais une liste vide : MultimodalityController
            // fait SetSelection(LastObjects) après chaque exécution. Retourner le client
            // basculerait la sélection sur lui — le « va servir cette table » qui suit ne
            // trouverait plus de sven:Table qu'au travers du détour DelegationRoles — et une
            // liste vide EFFACERAIT la sélection.
            SemantizationCore anchor = customer.Table != null
                ? customer.Table
                : customer.GetComponent<SemantizationCore>();

            return anchor != null
                ? new List<SemantizationCore> { anchor }
                : new();
        }
    }
}
