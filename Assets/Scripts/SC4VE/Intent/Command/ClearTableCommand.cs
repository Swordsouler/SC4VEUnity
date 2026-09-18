using Sven.Content;
using System;
using System.Collections.Generic;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers(
        // « range » NU est disponible : « range … dans/sur … » est intercepté AVANT la
        // boucle des déclencheurs par la pré-vérification PutInCommand de DetectCommandType
        // (verbe de rangement + préposition) — ici, pas de préposition : « range l'assiette
        // de cette table ». « débarrasse » est le mot du métier ; « vide » et « nettoie »
        // n'appartiennent à personne. « clear » nu ne gêne pas « clear selection »
        // (UnselectCommand) : DetectCommandType compare du plus long au plus court.
        // L'ontologie PRIME sur cet attribut (sc4ve.ttl) : les deux listes restent alignées.
        "range", "ranger", "rangez", "débarrasse", "débarrasser", "débarrassez",
        // Whisper écrit souvent « débarasse » (un seul R) : la faute d'orthographe est un
        // déclencheur à part entière, sans quoi le stem « debaras » ne rejoint jamais
        // « debarras » et la commande du métier reste incomprise en jeu.
        "débarasse", "débarasser", "débarassez",
        "vide", "vider", "videz", "nettoie", "nettoyer", "nettoyez",
        "clear", "clean", "tidy up", "bus the table")]
    [Serializable, CommandDescription(
        "Envoie un serveur DÉBARRASSER une table : il prend l'assiette vidée par le client " +
        "parti et la range sur le plan de travail, où elle redevient utilisable — les " +
        "assiettes sont en nombre fini, ce circuit est leur cycle de vie (« range l'assiette " +
        "de cette table », « vide cette table », « débarrasse cette table-là »). " +
        "Paramètres: UN SelectionParameter — la table, désignée au pointage. " +
        "Ne PAS générer pour « range X dans Y » : c'est PutInCommand.")]
    public class ClearTableCommand : Command
    {
        /// <summary>
        /// Une seule sélection, comme ServeCommand : « débarrasse cette table-là 👆 » n'a pas
        /// de mot pivot, les rôles se lisent dans les objets (DelegationRoles). Les tables
        /// n'ayant pas de nom, le pointage est le seul chemin — la deixis est FORCÉE par la
        /// scène elle-même (§3 du README), pas par la grammaire.
        /// </summary>
        public override List<Parameter> BuildRuleBasedParameters(RuleBasedContext ctx)
            => new() { ctx.BuildSelectionParameter(fallbackToSelection: true) };

        /// <summary>La réponse attendue désigne une cible (« cette table-là 👆 »), pas un paramètre.</summary>
        public override bool ExpectsTargetAnswer => true;

        public override List<SemantizationCore> Execute()
        {
            (Delegation agent, SemantizationCore table, List<SemantizationCore> known) =
                DelegationRoles.Resolve(this);

            bool french = UserData.Locale == "fr";

            // Rôle manquant : même schéma que ServeCommand — on demande, et on renvoie ce
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

            // Clear parle lui-même en cas de refus (occupé, repas en cours, rien à
            // débarrasser) : un échec est une information, pas un bug (§8 du README).
            if (!agent.Clear(table)) return new();

            // Liste de directives (« débarrasse cette table et celle-là ») : même file que
            // le service — le serveur unique enchaîne (Delegation).
            foreach (SemantizationCore extra in DelegationRoles.Tables(this))
                if (extra != table)
                    agent.Enqueue(() => agent.Clear(extra));

            return new List<SemantizationCore> { agent.GetComponent<SemantizationCore>() };
        }
    }
}
