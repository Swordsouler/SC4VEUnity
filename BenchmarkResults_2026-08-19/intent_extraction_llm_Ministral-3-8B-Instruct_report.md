# Extraction d'intention — configuration « llm_Ministral-3-8B-Instruct »

- Date : 2026-08-19 17:00
- Conditions : mode LLM, modèle « ministral-3-8b-instruct-2512 » via http://localhost:1234/v1 ; prompt ALLÉGÉ (TrimExamplesSection : les exemples sont retirés, ~6 500 → ~3 200 tokens — condition expérimentale différente des modèles évalués avec le prompt complet) ; un seul appel par cas (pas de cascade fast→precise) ; latence totale = appel HTTP + post-traitement, colonne http_ms = aller-retour HTTP seul (réseau + inférence, non séparables côté client).
- Conditions communes : movePointDelayMs = 0 ; context.history non injecté ; « no_match » compté comme « executed » au niveau extraction ; comparaison structurelle (type de commande, paramètres appariés par type puis contenu — filtres [type, valeur, horodatage], opérateurs AND/OR positionnels, limit, order).

## Métriques globales

| Métrique | Valeur |
|---|---|
| Exactitude du type | 32/35 = 91.4 % |
| Paramètres corrects — appariement strict | 10/39 |
| Paramètres corrects — sans horodatages | 17/39 |
| Paramètres — précision (stricte) | 22.7 % (10 VP / 34 FP / 29 FN) |
| Paramètres — rappel (strict) | 25.6 % |
| Paramètres — F-mesure (stricte) | 24.1 % |
| Paramètres — F-mesure sans horodatages (diagnostic) | 41.0 % |
| Paramètres — F-mesure sans horodatages, conjonctions non ordonnées (diagnostic) | 45.8 % |
| Clarifications légitimes | 0/3 |
| Clarifications à tort | 1 |
| Clarifications manquées | 3 |
| Exactitude de l'issue | 31/35 = 88.6 % |
| Latence médiane | 2749.5 ms |
| Latence HTTP médiane (réseau + inférence) | 2749.1 ms |

## Détail par catégorie

| Catégorie | n | Type OK | Params OK (strict) | Params OK (sans ts) | F1 stricte | F1 sans horodatages | Issue OK | Latence médiane (ms) |
|---|---|---|---|---|---|---|---|---|
| anaphorique | 5 | 5/5 | 2/7 | 2/7 | 29 % | 29 % | 5/5 | 1918.0 |
| clarification | 3 | 1/3 | 0/3 | 1/3 | 0 % | 22 % | 0/3 | 3017.6 |
| descriptif | 12 | 12/12 | 3/12 | 7/12 | 25 % | 58 % | 12/12 | 2849.4 |
| déictique | 7 | 6/7 | 4/10 | 4/10 | 36 % | 36 % | 6/7 | 2735.5 |
| no_match | 1 | 1/1 | 0/1 | 1/1 | 0 % | 100 % | 1/1 | 2750.7 |
| quantifié | 5 | 5/5 | 1/6 | 2/6 | 17 % | 33 % | 5/5 | 2916.4 |
| rejet | 2 | 2/2 | 0/0 | 0/0 | 100 % | 100 % | 2/2 | 331.0 |

## Causes d'écart les plus fréquentes

- 18 × SelectionParameter: contenu des filtres/valeurs
- 7 × SelectionParameter: horodatages seuls
- 2 × ColorParameter en trop
- 2 × SentenceParameter en trop
- 2 × SelectionParameter: ordre des filtres (+ horodatages)
- 2 × SelectionParameter en trop
- 2 × JSON produit non désérialisable
- 1 × PointParameter manquant
- 1 × PointParameter en trop
- 1 × SelectionParameter manquant

Détail par cas : `intent_extraction_llm_Ministral-3-8B-Instruct_cases.csv` ; sorties brutes : `intent_extraction_llm_Ministral-3-8B-Instruct_outputs.jsonl`.
