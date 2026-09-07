using Sven.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("stop", "attends", "attendre", "arrête", "arrêter", "wait", "halt")]
    [Serializable, CommandDescription(
        "Interrompt la tâche d'un serveur et le remet au repos (« stop », « attends »). " +
        "Le plat qu'il portait est reposé sur place, il ne disparaît pas. " +
        "Paramètre: SelectionParameter (le serveur). Sans serveur désigné, TOUS les serveurs " +
        "occupés sont interrompus — c'est le geste d'urgence.")]
    public class StopCommand : Command
    {
        /// <summary>
        /// Pas de repli sur la sélection courante : « stop » crié alors qu'une pomme est
        /// sélectionnée doit arrêter les serveurs, pas échouer sur une pomme. L'absence de
        /// cible est donc une intention à part entière, traitée dans Execute.
        /// </summary>
        public override List<Parameter> BuildRuleBasedParameters(RuleBasedContext ctx)
            => new() { ctx.BuildSelectionParameter(fallbackToSelection: false) };

        public override List<SemantizationCore> Execute()
        {
            Delegation designated = DelegationRoles.Agent(DelegationRoles.AllTargets(this));

            if (designated != null)
            {
                designated.Stop();
                return new List<SemantizationCore> { designated.GetComponent<SemantizationCore>() };
            }

            // « Stop » sans cible arrête tout le monde. C'est le seul ordre du jeu qui vaille
            // pour plusieurs agents à la fois, et c'est voulu : quand le joueur crie stop, il
            // ne veut pas d'abord répondre à « lequel ? ».
            Delegation[] busy = UnityEngine.Object
                .FindObjectsByType<Delegation>(FindObjectsInactive.Exclude)
                .Where(w => w.IsBusy)
                .ToArray();

            if (busy.Length == 0)
            {
                Speak(UserData.Locale == "fr" ? "Personne ne fait rien." : "Nobody is busy.");
                return new();
            }

            foreach (Delegation waiter in busy) waiter.Stop();

            return busy.Select(w => w.GetComponent<SemantizationCore>())
                       .Where(c => c != null)
                       .ToList();
        }
    }
}
