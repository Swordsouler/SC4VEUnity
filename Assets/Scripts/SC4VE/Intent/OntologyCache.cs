using Sven.Utils;
using System.Collections.Generic;
using System.Threading.Tasks;
using VDS.RDF;
using VDS.RDF.Parsing;

namespace Sc4ve.Multimodality.Intent
{
    /// <summary>
    /// Charge et parse les ontologies (.ttl) une seule fois, puis partage le graphe.
    /// Évite de re-parser tous les fichiers à chaque init de vocabulaire ET à chaque commande
    /// (CommandToGraphOutputCommandAsync). Le graphe est en lecture seule après chargement ;
    /// les consommateurs qui doivent muter en font une copie (Merge).
    /// </summary>
    public static class OntologyCache
    {
        private static Task<Graph> _loadTask;

        /// <summary>Graphe ontologique partagé (parsé une seule fois).</summary>
        public static Task<Graph> GetGraphAsync()
        {
            // Ne jamais mettre en cache une tâche échouée : un .ttl illisible au premier essai
            // bloquerait vocabulaire et commandes pour toute la session. On retente au prochain appel.
            if (_loadTask == null || _loadTask.IsFaulted || _loadTask.IsCanceled)
                _loadTask = LoadAsync();
            return _loadTask;
        }

        private static async Task<Graph> LoadAsync()
        {
            Graph graph = new();
            Dictionary<string, string> ontologies = await SvenSettings.GetOntologiesAsync();
            TurtleParser parser = new();
            foreach (string ttl in ontologies.Values)
                parser.Load(graph, ttl);
            return graph;
        }
    }
}
