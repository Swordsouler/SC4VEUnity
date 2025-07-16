using Sven.Content;
using Sven.GraphManagement;
using Sven.OwlTime;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VDS.RDF.Query;

namespace Sven.Command
{
    public class AnnotationFilter : QueryFilter<AnnotationFilterSettings>
    {
        private readonly string _semanticTypeName;
        public string SemanticTypeName => _semanticTypeName;

        public AnnotationFilter() : base()
        {
            _semanticTypeName = string.Empty;
        }

        public AnnotationFilter(string semanticTypeName) : base()
        {
            _semanticTypeName = semanticTypeName;
        }

        public AnnotationFilter(string semanticTypeName, DateTime dateTime) : base(dateTime)
        {
            _semanticTypeName = semanticTypeName;
        }

        public AnnotationFilter(string semanticTypeName, Instant instant) : base(instant)
        {
            _semanticTypeName = semanticTypeName;
        }

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
        ?object sven:component ?component .
        ?component a sven:Annotation ;
        		   sven:annotation ?property .
        ?property sven:hasTemporalExtent ?interval ;
        		  sven:value {SemanticTypeName} .
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