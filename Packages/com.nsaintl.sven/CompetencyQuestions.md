# SVEN's Competency Questions

## Prefixes for SPARQL queries

```sparql
PREFIX : <https://sven.lisn.upsaclay.fr/ve/Buffer/>
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
PREFIX sven: <https://sven.lisn.upsaclay.fr/ontology#>
PREFIX time: <http://www.w3.org/2006/time#>
PREFIX xsd: <http://www.w3.org/2001/XMLSchema#>
PREFIX ofn: <http://www.ontotext.com/sparql/functions/>
```

## Spatial reasoning

### Which objects are present in the scene at a given moment?

```sparql
SELECT DISTINCT ?object ?objectName
WHERE {
    ?object a sven:VirtualObject ;
    		sven:hasTemporalExtent ?interval ;
    		rdfs:label ?objectName .

    BIND("2025-12-04T11:57:41.400+01:00"^^xsd:dateTime AS ?instantTime)
    ?interval a time:Interval ;
    		  time:hasBeginning/time:inXSDDateTime ?startTime .
    OPTIONAL {
        ?interval time:hasEnd/time:inXSDDateTime ?_endTime .
    }
    BIND(IF(BOUND(?_endTime), ?_endTime, NOW()) AS ?endTime)
    FILTER(?startTime <= ?instantTime && ?instantTime < ?endTime)
}
```

### Which of these objects are visible, reachable, or occluded from the user’s point of view?

```sparql
SELECT DISTINCT ?object ?objectName ?visible ?reachable
WHERE {
    BIND("2025-12-04T11:57:41.400+01:00"^^xsd:dateTime AS ?instantTime)

    ?object a sven:VirtualObject ;
            sven:hasTemporalExtent ?objectInterval ;
            rdfs:label ?objectName .

    ?objectInterval a time:Interval ;
    				time:hasBeginning/time:inXSDDateTime ?objectStartTime .
    OPTIONAL {
        ?objectInterval time:hasEnd/time:inXSDDateTime ?_objectEndTime .
    }
    BIND(IF(BOUND(?_objectEndTime), ?_objectEndTime, NOW()) AS ?objectEndTime)
    FILTER(?objectStartTime <= ?instantTime && ?instantTime < ?objectEndTime)

    OPTIONAL{
        ?user a sven:User ;
              sven:pointOfView ?pov .
        ?lookEvent a sven:Event ;
                   sven:sender ?pov ;
                   sven:receiver ?object ;
                   sven:hasTemporalExtent ?visibleInterval .

        ?visibleInterval a time:Interval ;
                        time:hasBeginning/time:inXSDDateTime ?visibleStartTime .
        OPTIONAL {
            ?visibleInterval time:hasEnd/time:inXSDDateTime ?_visibleEndTime .
        }
        BIND(IF(BOUND(?_visibleEndTime), ?_visibleEndTime, NOW()) AS ?visibleEndTime)
        FILTER(?visibleStartTime <= ?instantTime && ?instantTime < ?visibleEndTime)
    }
    BIND(BOUND(?user) AS ?visible)

    OPTIONAL{
        ?user a sven:User ;
              sven:graspArea ?graspArea .
        ?lookEvent a sven:Event ;
                   sven:sender ?graspArea ;
                   sven:receiver ?object ;
                   sven:hasTemporalExtent ?reachableInterval .

        ?reachableInterval a time:Interval ;
                           time:hasBeginning/time:inXSDDateTime ?reachableStartTime .
        OPTIONAL {
            ?reachableInterval time:hasEnd/time:inXSDDateTime ?_reachableEndTime .
        }
        BIND(IF(BOUND(?_reachableEndTime), ?_reachableEndTime, NOW()) AS ?reachableEndTime)
        FILTER(?reachableStartTime <= ?instantTime && ?instantTime < ?reachableEndTime)
    }

    BIND(BOUND(?graspArea) AS ?reachable)
}
```

## Temporal reasoning

### How do object states or properties (e.g., position, color, or activation) evolve throughout an experiment?

```sparql
SELECT ?object ?objectName ?x ?y ?z
WHERE {
    BIND("Interactable Banana 2" AS ?objectName)
    ?object a sven:VirtualObject ;
    		rdfs:label ?objectName ;
    		sven:component ?component .
    ?component sven:position ?position .
    ?position sven:hasTemporalExtent ?interval ;
    		  sven:x ?x ;
    		  sven:y ?y ;
    		  sven:z ?z .
    ?interval time:hasBeginning/time:inXSDDateTime ?time .
} ORDER BY ?time
```

### What sequence of actions or events occurs before or after a given instant?

```sparql
SELECT ?event ?occuredBefore ?occuredAfter
WHERE {
    BIND("2025-12-04T14:14:34.600+01:00"^^xsd:dateTime AS ?instantTime)

    ?event a sven:Event ;
    	   sven:hasTemporalExtent ?interval .
    ?interval time:hasBeginning/time:inXSDDateTime ?startTime .

    BIND(?startTime < ?instantTime AS ?occuredBefore)
    BIND(?startTime > ?instantTime AS ?occuredAfter)
}
```

## Interaction analysis

### Which interactions—such as collisions, grasping, or pointing—take place within a specified time window?

```sparql
SELECT ?event ?occuredBefore ?occuredAfter
WHERE {
    BIND("2025-12-04T14:14:34.600+01:00"^^xsd:dateTime AS ?startTime)
    BIND("2025-12-04T14:14:38.600+01:00"^^xsd:dateTime AS ?endTime)

    ?event a sven:Event ;
    	   sven:hasTemporalExtent ?interval .
    ?interval time:hasBeginning/time:inXSDDateTime ?time .

    FILTER(?startTime <= ?time && ?time <= ?endTime)
}
```

### Which entities participate in these interactions?

```sparql
SELECT ?event ?sender ?receiver
WHERE {
    ?event a sven:Event ;
    	   sven:sender ?sender ;
    	   sven:receiver ?receiver .
}
```

## User behaviour interpretation

### What type of food did the user look at the most?

```sparql
SELECT DISTINCT ?objectType (SUM(?totalSeconds) AS ?sumSeconds)
WHERE {
    ?user a sven:User ;
          sven:pointOfView ?pov .
    ?lookEvent a sven:Event ;
               sven:sender ?pov ;
               sven:receiver ?lookedObject ;
               sven:hasTemporalExtent/time:hasXSDDuration ?duration .
    ?lookedObject a ?objectType .
    ?objectType rdfs:subClassOf sven:Food .
    BIND(ofn:asMillis(?duration) / 1000 AS ?totalSeconds)
    FILTER(!isBLANK(?objectType))
}
GROUP BY ?objectType
ORDER BY DESC(?sumSeconds)
```

### Which categories of domain-specific objects (e.g., tools, fruits, medical items) elicit the highest interaction rates?

```sparql
SELECT DISTINCT ?objectType (SUM(?totalSeconds) AS ?sumSeconds)
WHERE {
    ?user a sven:User ;
          sven:pointOfView ?pov .
    ?lookEvent a sven:Event ;
               sven:sender ?pov ;
               sven:receiver ?lookedObject ;
               sven:hasTemporalExtent/time:hasXSDDuration ?duration .
    ?lookedObject a ?objectType .
    BIND(ofn:asMillis(?duration) / 1000 AS ?totalSeconds)
    FILTER(!isBLANK(?objectType))
}
GROUP BY ?objectType
ORDER BY DESC(?sumSeconds)
```

### Which objects receive the most visual attention or engagement?

```sparql
SELECT DISTINCT ?objectType (SUM(?totalSeconds) AS ?sumSeconds)
WHERE {
    ?user a sven:User ;
          sven:own ?modality .
    ?lookEvent a sven:Event ;
               sven:sender ?modality ;
               sven:receiver ?lookedObject ;
               sven:hasTemporalExtent/time:hasXSDDuration ?duration .
    ?lookedObject a ?objectType .
    BIND(ofn:asMillis(?duration) / 1000 AS ?totalSeconds)
    FILTER(!isBLANK(?objectType))
}
GROUP BY ?objectType
ORDER BY DESC(?sumSeconds)
```
