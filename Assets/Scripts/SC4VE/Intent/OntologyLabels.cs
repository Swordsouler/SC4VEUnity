using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VDS.RDF;
using VDS.RDF.Query;

namespace Sc4ve.Multimodality.Intent
{
    /// <summary>
    /// Le libellé localisé d'une classe de l'ontologie, mis en cache.
    ///
    /// Quatre appelants du lot 4 en ont besoin — la famille commandée, la contrainte, la
    /// classe fautive d'un refus, le plat reconnu — et sans cache, chaque refus coûterait une
    /// requête par mot prononcé.
    ///
    /// Interroge OntologyCache — le graphe ontologique statique — et jamais le graphe
    /// d'exécution : le libellé de sven:Chicken est une constante, la chercher dans la trace
    /// reviendrait à parcourir un graphe qui grossit à chaque image. C'est le motif exact de
    /// ColorParameter.GetAllAvailableColors.
    /// </summary>
    public static class OntologyLabels
    {
        // La locale fait partie de la clé : un cache aveugle servirait des libellés français
        // après un passage en anglais, pour toute la session.
        private static readonly Dictionary<(string, string), string> _cache = new();

        /// <summary>
        /// Le libellé de la classe dans la locale, ou son nom local à défaut — jamais null.
        ///
        /// Déterministe : plusieurs libellés dans la même langue existent (sven:PumpkinSoup en
        /// porte deux, « Soupe de citrouille » et « Soupe de potiron »), donc la requête trie
        /// et prend la première ligne. Sans le tri, le mot prononcé changerait d'un lancement
        /// à l'autre.
        /// </summary>
        public static async Task<string> GetAsync(string prefixedClass, string locale)
        {
            if (string.IsNullOrWhiteSpace(prefixedClass)) return "";
            if (_cache.TryGetValue((prefixedClass, locale), out string cached)) return cached;

            string label = LocalName(prefixedClass);

            Graph graph = await OntologyCache.GetGraphAsync();
            string query = $@"PREFIX sven: <https://sven.lisn.upsaclay.fr/ontology#>
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>

SELECT ?label
WHERE {{
    {prefixedClass} rdfs:label ?label .
    FILTER(langMatches(lang(?label), ""{locale}""))
}} ORDER BY ?label";

            if (graph.ExecuteQuery(query) is SparqlResultSet results)
            {
                string first = results.Cast<SparqlResult>()
                    .Select(r => (r["label"] as ILiteralNode)?.Value)
                    .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
                if (first != null) label = first;
            }

            _cache[(prefixedClass, locale)] = label;
            return label;
        }

        /// <summary>Vide le cache — utile surtout aux tests.</summary>
        public static void Clear() => _cache.Clear();

        private static string LocalName(string prefixedClass)
        {
            int cut = prefixedClass.LastIndexOfAny(new[] { ':', '#', '/' });
            return cut >= 0 && cut < prefixedClass.Length - 1 ? prefixedClass[(cut + 1)..] : prefixedClass;
        }
    }
}
