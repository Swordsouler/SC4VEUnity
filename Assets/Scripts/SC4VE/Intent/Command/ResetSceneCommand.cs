using Sven.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("réinitialise la scène", "remet la scène", "reset la scène",
                       "tout réinitialiser", "remet tout", "restaure la scène")]
    [Serializable, CommandDescription(
        "Remet tous les objets à leur état initial ; dans le mini-jeu, vide aussi la " +
        "salle, remet le score et rappelle l'écran de départ (« réinitialise la scène », " +
        "entre deux visiteurs). Pas de paramètre.")]
    public class ResetSceneCommand : Command
    {
        public override List<Parameter> BuildRuleBasedParameters(RuleBasedContext ctx)
            => new List<Parameter>();

        /// <summary>
        /// La remise à zéro d'ENTRE DEUX VISITEURS, dans l'ordre inverse d'une partie.
        /// Les agents s'arrêtent d'abord, EN SILENCE (Halt / ResetService) : deux « je ne
        /// fais rien » à chaque réinitialisation seraient du bruit, et un serveur encore
        /// en course lâcherait son assiette APRÈS la restauration. Les copies du
        /// garde-manger quittent le monde (seuls les ORIGINAUX se restaurent). Les
        /// contenants se VIDENT avant la restauration : l'état initial ne contient rien,
        /// et un objet resté « dans l'assiette » côté C# pendant que la restauration le
        /// repose au garde-manger serait un mensonge de contenance — chaque objet retiré
        /// est rendu au parent de son contenant, sans quoi il voyagerait ensuite avec
        /// l'assiette. Enfin le restaurant, s'il est dans la scène : salle vidée, score
        /// remis, écran de départ de retour, recette courante oubliée.
        /// </summary>
        public override List<SemantizationCore> Execute()
        {
            foreach (Delegation waiter in UnityEngine.Object.FindObjectsByType<Delegation>(FindObjectsInactive.Exclude))
                waiter.Halt();
            foreach (Cook cook in UnityEngine.Object.FindObjectsByType<Cook>(FindObjectsInactive.Exclude))
                cook.ResetService();

            foreach (SpawnedByCook spawned in UnityEngine.Object.FindObjectsByType<SpawnedByCook>(FindObjectsInactive.Include))
                if (spawned != null) UnityEngine.Object.Destroy(spawned.gameObject);

            foreach (ContainerContent container in UnityEngine.Object.FindObjectsByType<ContainerContent>(FindObjectsInactive.Exclude))
            {
                foreach (SemantizationCore item in container.Content.ToList())
                    if (item != null) item.transform.SetParent(container.transform.parent, true);
                container.Clear();
            }

            CommandHistory.Clear();
            OriginalStateStore.RestoreAll();

            UnityEngine.Object.FindAnyObjectByType<ServiceProgression>()?.ResetGame();
            PrepareCommand.Forget();

            return new List<SemantizationCore>();
        }
    }
}
