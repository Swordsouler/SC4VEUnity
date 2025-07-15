using Sven.Content;
using Sven.GraphManagement;
using System.Collections.Generic;
using System.Threading.Tasks;
using VDS.RDF.Query;

namespace Sven.Command
{
    public class ColorFilter : QueryFilter<ColorFilterSettings>
    {
        public override async Task<List<SemantizationCore>> Query()
        {
            string query = $@"PREFIX : <{GraphManager.BaseUri}>
PREFIX time: <http://www.w3.org/2006/time#>
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
PREFIX sven: <https://sven.lisn.upsaclay.fr/ontology#>
PREFIX xsd: <http://www.w3.org/2001/XMLSchema#>

SELECT DISTINCT ?object ?r ?g ?b ?a 
FROM :
WHERE {{
    {{
        ?object sven:component ?component .
        ?component sven:color ?property .
        ?property a sven:Color ;
        		  sven:hasTemporalExtent ?interval ;
        		  sven:r ?r ;
        		  sven:g ?g ;
        		  sven:b ?b ;
        		  sven:a ?a .
    }}
    
{GraphManager.RetrieveIntervalQuery(Instant)}
}}";
            SparqlResultSet result = await GraphManager.QueryMemoryAsync(query);
            // list of ids to semantization core
            List<SemantizationCore> semantizationCores = new();
            return semantizationCores;
        }
    }
}