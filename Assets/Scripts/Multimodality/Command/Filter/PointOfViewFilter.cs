using Sven.Content;
using Sven.GraphManagement;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using VDS.RDF.Query;

namespace Sven.Command
{
    public class PointOfViewFilter : QueryFilter<CommandSettings>
    {
        public override async Task<List<SemantizationCore>> Execute()
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
        ?sender a sven:PointOfView .
    }}
    
{GraphManager.RetrieveIntervalQuery(Instant)}
}}";
            SparqlResultSet resultSet = await GraphManager.QueryMemoryAsync(query);
            List<SemantizationCore> semantizationCores = new();
            foreach (SparqlResult result in resultSet.Cast<SparqlResult>())
            {
                string objectUUID = result["object"].ToString()[(result["object"].ToString().LastIndexOf("/") + 1)..];

                Component component = SemantizationExtensions.GetComponentByUUID(objectUUID);

                // if component is not null and is a SemantizationCore, then add it to the list
                if (component != null && component is SemantizationCore semantizationCore)
                    semantizationCores.Add(semantizationCore);
                else
                    Debug.LogWarning($"Component with UUID {objectUUID} not found or is not a SemantizationCore.");
            }
            return semantizationCores;
        }
    }
}