using Sven.GraphManagement;
using Sven.Utils;
using System.Threading.Tasks;
using VDS.RDF;

namespace Sc4ve.Multimodality.Intent
{
    /// <summary>
    /// La copie du graphe de scène qu'interrogent les paramètres d'une phrase (sélection,
    /// pointage), prise dès la FIN DE LA PAROLE plutôt qu'au moment de la recherche.
    ///
    /// Copier le graphe (25 000 triplets en cours de partie) coûtait presque autant que la
    /// requête, et se refaisait à chaque paramètre. Prise en fin de parole, la copie se fait
    /// pendant que Whisper transcrit, et toute la phrase la partage. Le texte des requêtes ne
    /// change pas, leurs résultats non plus : ce que les filtres cherchent — la scène aux
    /// instants prononcés — est dans le graphe dès que la parole s'arrête. La copie y gagne
    /// même : figée avant l'attente du LLM, elle ne perd rien à l'éviction de l'historique
    /// ancien (GraphManager.InMemoryHistorySeconds, que règle MultimodalityController).
    ///
    /// Sans fin de parole (commande injectée, tests), chaque paramètre copie le graphe
    /// lui-même, comme avant.
    /// </summary>
    public static class SceneGraphSnapshot
    {
        private static Task<Graph> _snapshot;

        /// <summary>La copie de la phrase en cours, ou null.</summary>
        public static Task<Graph> Current => _snapshot;

        /// <summary>
        /// Fin de la parole : la copie part sur un thread de fond, après un tick de
        /// sémantisation — le pointage de la fin de phrase, dont l'instant fait foi pour le
        /// déictique, doit y figurer — et le retour du ralenti (ListeningTimeScale, 0,15 s), qui
        /// étire ce tick tant qu'il dure.
        /// </summary>
        public static void Take()
        {
            int delayMs = (int)(1000f / SvenSettings.SemanticizeFrequency) + 150;
            _snapshot = Task.Run(async () =>
            {
                await Task.Delay(delayMs);
                return GraphManager.InstanceCopy();
            });
        }

        /// <summary>La copie à interroger : celle de la phrase en cours, sinon une copie fraîche.</summary>
        public static Task<Graph> GetAsync() => _snapshot ?? Task.Run(GraphManager.InstanceCopy);

        /// <summary>
        /// Phrase traitée : sa copie est rendue — sauf si une parole plus récente l'a déjà
        /// remplacée, et la phrase suivante la garde.
        /// </summary>
        public static void Release(Task<Graph> snapshot)
        {
            if (_snapshot == snapshot) _snapshot = null;
        }
    }
}
