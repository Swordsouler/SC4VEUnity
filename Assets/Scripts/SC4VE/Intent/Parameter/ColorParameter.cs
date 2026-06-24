using Newtonsoft.Json;
using Sc4ve.Voice;
using Sven.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Query;

namespace Sc4ve.Multimodality.Intent
{
    [Serializable]
    public class Color
    {
        [SerializeField] private float _red;
        public float Red
        {
            get => _red;
            set => _red = value;
        }
        [SerializeField] private float _green;
        public float Green
        {
            get => _green;
            set => _green = value;
        }
        [SerializeField] private float _blue;
        public float Blue
        {
            get => _blue;
            set => _blue = value;
        }
        [SerializeField] private float _alpha;
        public float Alpha
        {
            get => _alpha;
            set => _alpha = value;
        }
        [SerializeField] private float _tolerance;
        public float Tolerance
        {
            get => _tolerance;
            set => _tolerance = value;
        }
        [JsonIgnore]
        public UnityEngine.Color Value => new(Red, Green, Blue, Alpha);

        public float MinRed => Mathf.Clamp(Red - Tolerance, 0f, 1f);
        public float MaxRed => Mathf.Clamp(Red + Tolerance, 0f, 1f);
        public float MinGreen => Mathf.Clamp(Green - Tolerance, 0f, 1f);
        public float MaxGreen => Mathf.Clamp(Green + Tolerance, 0f, 1f);
        public float MinBlue => Mathf.Clamp(Blue - Tolerance, 0f, 1f);
        public float MaxBlue => Mathf.Clamp(Blue + Tolerance, 0f, 1f);
    }

    public class ColorParameter : Parameter
    {
        [SerializeField] private string _value;
        [JsonProperty("value")]
        public string Value
        {
            get => _value;
            set => _value = value;
        }

        [SerializeField] private DateTime? _timestamp;
        [JsonProperty("timestamp")]
        public DateTime? Timestamp
        {
            get => _timestamp;
            set => _timestamp = value;
        }

        [SerializeField] private Color _color;
        [JsonIgnore]
        public Color Color
        {
            get => _color;
            set => _color = value;
        }

        /// <summary>
        /// Échappe une valeur pour un littéral de chaîne SPARQL (antislash et guillemets).
        /// Les valeurs viennent d'un vocabulaire contrôlé, mais on échappe par robustesse.
        /// </summary>
        private static string EscapeSparqlLiteral(string value)
            => value?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? string.Empty;

        public Task<Color> QueryColor(Graph queryGraph)
        {
            string locale = UserData.Locale;
            // execute sparql query to get color from value
            string query = $@"
PREFIX sven: <https://sven.lisn.upsaclay.fr/ontology#>
PREFIX sc4ve: <https://sc4ve.lisn.upsaclay.fr/ontology#>
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>

SELECT ?r ?g ?b ?a ?t
WHERE {{
    ?color a sven:Color ;
           rdfs:label ""{EscapeSparqlLiteral(Value)}""@{locale} ;
           sven:r ?r ;
           sven:g ?g ;
           sven:b ?b ;
           sven:a ?a ;
           sc4ve:tolerance ?t .
}}";
            // execute query and parse result
            // extract color components from first result
            if (queryGraph.ExecuteQuery(query) is SparqlResultSet results && results.Count > 0)
            {
                SparqlResult result = (SparqlResult)results.Results[0];

                // Use ILiteralNode.Value and invariant culture parsing to avoid formatting issues

                if (result["r"] is not ILiteralNode rNode ||
                    result["g"] is not ILiteralNode gNode ||
                    result["b"] is not ILiteralNode bNode ||
                    result["a"] is not ILiteralNode aNode ||
                    result["t"] is not ILiteralNode tNode)
                {
                    Debug.LogWarning("QueryColor: one or more color components are not literal nodes.");
                    return Task.FromResult<Color>(null);
                }

                // Parse strictly with invariant culture so decimal separator is '.'
                bool okR = float.TryParse(rNode.Value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float rVal);
                bool okG = float.TryParse(gNode.Value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float gVal);
                bool okB = float.TryParse(bNode.Value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float bVal);
                bool okA = float.TryParse(aNode.Value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float aVal);
                bool okT = float.TryParse(tNode.Value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float tVal);

                if (!okR || !okG || !okB || !okA || !okT)
                {
                    Debug.LogWarning($"QueryColor: failed to parse color components. r='{rNode.Value}', g='{gNode.Value}', b='{bNode.Value}', t='{tNode.Value}'");
                    return Task.FromResult<Color>(null);
                }

                return Task.FromResult(new Color
                {
                    Red = rVal,
                    Green = gVal,
                    Blue = bVal,
                    Alpha = aVal,
                    Tolerance = tVal
                });
            }
            else
            {
                return Task.FromResult<Color>(null);
            }
        }

        public override async Task<IUriNode> Semanticize(Graph graph)
        {
            IUriNode parameterNode = await base.Semanticize(graph);

            Color ??= await QueryColor(graph);
            if (Color != null)
            {
                IUriNode r = graph.CreateUriNode("sven:r");
                IUriNode g = graph.CreateUriNode("sven:g");
                IUriNode b = graph.CreateUriNode("sven:b");
                IUriNode a = graph.CreateUriNode("sven:a");
                IUriNode tolerance = graph.CreateUriNode("sc4ve:tolerance");
                // insert triples for color components (0 to 1) — use InvariantCulture so ToString uses '.'
                graph.Assert(new Triple(parameterNode, r, graph.CreateLiteralNode(Color.Red.ToString(CultureInfo.InvariantCulture), graph.CreateUriNode("xsd:float").Uri)));
                graph.Assert(new Triple(parameterNode, g, graph.CreateLiteralNode(Color.Green.ToString(CultureInfo.InvariantCulture), graph.CreateUriNode("xsd:float").Uri)));
                graph.Assert(new Triple(parameterNode, b, graph.CreateLiteralNode(Color.Blue.ToString(CultureInfo.InvariantCulture), graph.CreateUriNode("xsd:float").Uri)));
                graph.Assert(new Triple(parameterNode, a, graph.CreateLiteralNode(Color.Alpha.ToString(CultureInfo.InvariantCulture), graph.CreateUriNode("xsd:float").Uri)));
                graph.Assert(new Triple(parameterNode, tolerance, graph.CreateLiteralNode(Color.Tolerance.ToString(CultureInfo.InvariantCulture), graph.CreateUriNode("xsd:float").Uri)));
            }
            return parameterNode;
        }

        private static List<string> _availableColors;
        private static Language? _cachedLanguage;

        // Palette (nom + composantes RGB) mise en cache en même temps que les noms de couleurs,
        // pour permettre la résolution synchrone RGB → nom de couleur (cf. GetColorName).
        private struct ColorEntry { public string Name; public float R, G, B, Tolerance; }
        private static List<ColorEntry> _palette;

        public static async Task<List<string>> GetAvailableColorsAsync()
        {
            if (_availableColors == null || _cachedLanguage != UserData.Language)
            {
                _availableColors = await GetAllAvailableColors(UserData.Language);
                _cachedLanguage = UserData.Language;
            }
            return _availableColors;
        }

        public static async Task<List<string>> GetAllAvailableColors(Language language)
        {
            // Graphe ontologique partagé (parsé une seule fois) — évite de re-parser les .ttl.
            Graph graph = await OntologyCache.GetGraphAsync();

            string locale = UserData.Locale;
            // On récupère le label ET les composantes RGB (en OPTIONAL pour ne pas exclure une
            // couleur sans RGB : la liste des noms retournée reste identique à avant).
            string query = $@"
PREFIX sven: <https://sven.lisn.upsaclay.fr/ontology#>
PREFIX sc4ve: <https://sc4ve.lisn.upsaclay.fr/ontology#>
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>

SELECT ?label ?r ?g ?b ?t
WHERE {{
    ?color a sven:Color ;
           rdfs:label ?label .
    OPTIONAL {{ ?color sven:r ?r ; sven:g ?g ; sven:b ?b ; sc4ve:tolerance ?t . }}
    FILTER(langMatches(lang(?label), ""{locale}""))
}}";

            var names = new List<string>();
            var palette = new List<ColorEntry>();
            if (graph.ExecuteQuery(query) is SparqlResultSet results)
            {
                foreach (SparqlResult result in results.Cast<SparqlResult>())
                {
                    string name = (result["label"] as ILiteralNode)?.Value;
                    if (name == null) continue;
                    names.Add(name);

                    if (TryParseComponent(result, "r", out float r) &&
                        TryParseComponent(result, "g", out float g) &&
                        TryParseComponent(result, "b", out float b))
                    {
                        float tol = TryParseComponent(result, "t", out float t) ? t : 0.2f;
                        palette.Add(new ColorEntry { Name = name, R = r, G = g, B = b, Tolerance = tol });
                    }
                }
            }
            _palette = palette;
            return names;
        }

        private static bool TryParseComponent(SparqlResult result, string variable, out float value)
        {
            value = 0f;
            return result.HasBoundValue(variable)
                && result[variable] is ILiteralNode node
                && float.TryParse(node.Value, NumberStyles.Float | NumberStyles.AllowThousands,
                                  CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// Renvoie le nom de la couleur du vocabulaire qui correspond à la couleur donnée
        /// (chaque composante R/G/B dans [valeur ± tolérance], même logique que le filtre SPARQL),
        /// ou null si aucune ne correspond. On ne renvoie JAMAIS la couleur « la plus proche » :
        /// un objet hors palette (blanc, gris…) ne doit pas être nommé à tort (ex: « cyan »).
        /// En cas de chevauchement de plusieurs boîtes, on retient la plus proche.
        /// </summary>
        public static string GetColorName(UnityEngine.Color color)
        {
            if (_palette == null || _palette.Count == 0) return null;
            string best = null;
            float bestDist = float.MaxValue;
            foreach (ColorEntry c in _palette)
            {
                if (Mathf.Abs(color.r - c.R) > c.Tolerance) continue;
                if (Mathf.Abs(color.g - c.G) > c.Tolerance) continue;
                if (Mathf.Abs(color.b - c.B) > c.Tolerance) continue;
                float dr = color.r - c.R, dg = color.g - c.G, db = color.b - c.B;
                float dist = dr * dr + dg * dg + db * db;
                if (dist < bestDist) { bestDist = dist; best = c.Name; }
            }
            return best;
        }
    }
}