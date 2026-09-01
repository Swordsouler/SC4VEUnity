# Extraction d'intention — configuration « llm_Ministral-3-8B-Instruct_full »

- Date : 2026-09-01 15:14
- Conditions : mode LLM, modèle « ministral-3-8b-instruct-2512 » via http://localhost:1234/v1 ; prompt complet ; un seul appel par cas (pas de cascade fast→precise) ; latence totale = appel HTTP + post-traitement, colonne http_ms = aller-retour HTTP seul (réseau + inférence, non séparables côté client).
- Conditions communes : movePointDelayMs = 0 ; context.history non injecté ; « no_match » compté comme « executed » au niveau extraction ; comparaison structurelle (type de commande, paramètres appariés par type puis contenu — filtres [type, valeur, horodatage], opérateurs AND/OR positionnels, limit, order).

## Métriques globales

| Métrique | Valeur |
|---|---|
| Exactitude du type | 33/35 = 94.3 % |
| Paramètres corrects — appariement strict | 14/39 |
| Paramètres corrects — sans horodatages | 31/39 |
| Paramètres — précision (stricte) | 34.1 % (14 VP / 27 FP / 25 FN) |
| Paramètres — rappel (strict) | 35.9 % |
| Paramètres — F-mesure (stricte) | 35.0 % |
| Paramètres — F-mesure sans horodatages (diagnostic) | 77.5 % |
| Paramètres — F-mesure sans horodatages, conjonctions non ordonnées (diagnostic) | 77.5 % |
| Clarifications légitimes | 3/3 |
| Clarifications à tort | 1 |
| Clarifications manquées | 0 |
| Exactitude de l'issue | 34/35 = 97.1 % |
| Latence médiane | 5482.9 ms |
| Latence HTTP médiane (réseau + inférence) | 5482.6 ms |

## Détail par catégorie

| Catégorie | n | Type OK | Params OK (strict) | Params OK (sans ts) | F1 stricte | F1 sans horodatages | Issue OK | Latence médiane (ms) |
|---|---|---|---|---|---|---|---|---|
| anaphorique | 5 | 4/5 | 2/7 | 6/7 | 29 % | 86 % | 5/5 | 3635.0 |
| clarification | 3 | 2/3 | 0/3 | 2/3 | 0 % | 57 % | 3/3 | 5616.4 |
| descriptif | 12 | 12/12 | 3/12 | 10/12 | 25 % | 83 % | 12/12 | 5909.9 |
| déictique | 7 | 7/7 | 8/10 | 9/10 | 76 % | 86 % | 6/7 | 5482.9 |
| no_match | 1 | 1/1 | 0/1 | 1/1 | 0 % | 100 % | 1/1 | 6215.9 |
| quantifié | 5 | 5/5 | 1/6 | 3/6 | 17 % | 50 % | 5/5 | 6165.2 |
| rejet | 2 | 2/2 | 0/0 | 0/0 | 100 % | 100 % | 2/2 | 2117.0 |

## Causes d'écart les plus fréquentes

- 17 × SelectionParameter: horodatages seuls
- 6 × SelectionParameter: contenu des filtres/valeurs
- 2 × SentenceParameter en trop
- 2 × JSON produit non désérialisable
- 1 × PointParameter manquant
- 1 × PointParameter en trop
- 1 × SelectionParameter en trop
- 1 × SelectionParameter manquant

Détail par cas : `intent_extraction_llm_Ministral-3-8B-Instruct_full_cases.csv` ; sorties brutes : `intent_extraction_llm_Ministral-3-8B-Instruct_full_outputs.jsonl`.
