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
        private readonly ColorThreshold _colorThreshold;
        public ColorThreshold ColorThreshold => _colorThreshold;

        public ColorFilter() : base()
        {
            _colorThreshold = new ColorThreshold();
        }

        public ColorFilter(ColorThreshold colorThreshold) : base()
        {
            _colorThreshold = colorThreshold;
        }

        public ColorFilter(ColorThreshold colorThreshold, DateTime dateTime) : base(dateTime)
        {
            _colorThreshold = colorThreshold;
        }

        public ColorFilter(ColorThreshold colorThreshold, Instant instant) : base(instant)
        {
            _colorThreshold = colorThreshold;
        }

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
            SparqlResultSet resultSet = await GraphManager.QueryMemoryAsync(query);
            List<SemantizationCore> semantizationCores = new();
            foreach (SparqlResult result in resultSet.Cast<SparqlResult>())
            {
                string r = result["r"].AsValuedNode().ToString();
                string g = result["g"].AsValuedNode().ToString();
                string b = result["b"].AsValuedNode().ToString();

                if (!ColorThreshold.IsMatching(new Color(
                    float.Parse(r, System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(g, System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(b, System.Globalization.CultureInfo.InvariantCulture))))
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