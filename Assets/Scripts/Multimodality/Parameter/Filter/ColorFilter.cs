using Sven.Content;
using Sven.GraphManagement;
using Sven.OwlTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using VDS.RDF.Nodes;
using VDS.RDF.Query;

namespace Sven.Command
{
    public class ColorFilter : QueryFilter<ColorFilterSettings>
    {
        private readonly ColorParameter _colorParameter;
        public ColorParameter ColorParameter => _colorParameter;

        public ColorFilter() : base()
        {
            _colorParameter = new ColorParameter();
        }

        public ColorFilter(ColorParameter colorParameter) : base()
        {
            _colorParameter = colorParameter;
        }

        public ColorFilter(ColorParameter colorParameter, DateTime dateTime) : base(dateTime)
        {
            _colorParameter = colorParameter;
        }

        public ColorFilter(ColorParameter colorParameter, Instant instant) : base(instant)
        {
            _colorParameter = colorParameter;
        }

        public override async Task<List<SemantizationCore>> Query()
        {
            string query = $@"PREFIX time: <http://www.w3.org/2006/time#>
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
PREFIX sven: <https://sven.lisn.upsaclay.fr/ontology#>
PREFIX xsd: <http://www.w3.org/2001/XMLSchema#>

SELECT DISTINCT ?object ?r ?g ?b ?a 
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
            SparqlResultSet resultSet = await GraphManager.QueryMemoryAsync(query, false);
            List<SemantizationCore> semantizationCores = new();
            foreach (SparqlResult result in resultSet.Cast<SparqlResult>())
            {
                float r = Convert.ToSingle(result["r"].AsValuedNode().ToValue());
                float g = Convert.ToSingle(result["g"].AsValuedNode().ToValue());
                float b = Convert.ToSingle(result["b"].AsValuedNode().ToValue());

                if (!ColorParameter.IsMatching(new Color(r, g, b)))
                    continue;

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