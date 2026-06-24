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
        public static Task<Graph> GetGraphAsync() => _loadTask ??= LoadAsync();

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
