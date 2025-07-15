using Sven.Content;
using Sven.GraphManagement;
using System.Collections.Generic;
using System.Threading.Tasks;
using VDS.RDF.Query;

namespace Sven.Command
{
    public class PointerFilter : QueryFilter<CommandSettings>
    {
        public override async Task<List<SemantizationCore>> Query()
        {
            string query = $@"PREFIX : <{GraphManager.BaseUri}>
PREFIX time: <http://www.w3.org/2006/time#>
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
PREFIX sven: <https://sven.lisn.upsaclay.fr/ontology#>
PREFIX xsd: <http://www.w3.org/2001/XMLSchema#>

SELECT DISTINCT ?object
FROM :
WHERE {{
    {{
        ?event a sven:CollisionEvent ;
        	   sven:sender ?sender ;
        	   sven:receiver ?object ;
        	   sven:hasTemporalExtent ?interval .
        ?sender a sven:PointerObject .
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