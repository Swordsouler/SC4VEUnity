# Extraction d'intention — configuration « llm_gpt-4o-mini »

- Date : 2026-07-31 11:07
- Conditions : mode LLM, modèle « gpt-4o-mini » via API OpenAI ; prompt complet ; un seul appel par cas (pas de cascade fast→precise) ; latence totale = appel HTTP + post-traitement, colonne http_ms = aller-retour HTTP seul (réseau + inférence, non séparables côté client).
- Conditions communes : movePointDelayMs = 0 ; context.history non injecté ; « no_match » compté comme « executed » au niveau extraction ; comparaison structurelle (type de commande, paramètres appariés par type puis contenu — filtres [type, valeur, horodatage], opérateurs AND/OR positionnels, limit, order).

## Métriques globales

| Métrique | Valeur |
|---|---|
| Exactitude du type | 30/35 = 85.7 % |
| Paramètres — précision (stricte) | 34.1 % (15 VP / 29 FP / 24 FN) |
| Paramètres — rappel (strict) | 38.5 % |
| Paramètres — F-mesure (stricte) | 36.1 % |
| Paramètres — F-mesure sans horodatages (diagnostic) | 65.1 % |
| Paramètres — F-mesure sans horodatages, conjonctions non ordonnées (diagnostic) | 65.1 % |
| Clarifications légitimes | 0/3 |
| Clarifications à tort | 2 |
| Clarifications manquées | 3 |
| Exactitude de l'issue | 27/35 = 77.1 % |
| Latence médiane | 1723.8 ms |
| Latence HTTP médiane (réseau + inférence) | 1723.3 ms |

## Détail par catégorie

| Catégorie | n | Type OK | F1 stricte | F1 sans horodatages | Issue OK | Latence médiane (ms) |
|---|---|---|---|---|---|---|
| anaphorique | 5 | 4/5 | 31 % | 77 % | 4/5 | 1761.0 |
| clarification | 3 | 3/3 | 0 % | 0 % | 0/3 | 1723.8 |
| descriptif | 12 | 10/12 | 24 % | 64 % | 10/12 | 1839.3 |
| déictique | 7 | 6/7 | 76 % | 76 % | 6/7 | 1435.9 |
| no_match | 1 | 1/1 | 0 % | 0 % | 1/1 | 2239.1 |
| quantifié | 5 | 5/5 | 33 % | 100 % | 5/5 | 1621.2 |
| rejet | 2 | 1/2 | 0 % | 0 % | 1/2 | 2035.5 |

## Causes d'écart les plus fréquentes

- 12 × SelectionParameter: horodatages seuls
- 8 × SelectionParameter: contenu des filtres/valeurs
- 2 × ColorParameter en trop
- 2 × SelectionParameter: limit (+ horodatages ?)
- 2 × SelectionParameter en trop
- 2 × PointParameter en trop
- 1 × PointParameter manquant
- 1 × SelectionParameter manquant
- 1 × SentenceParameter en trop

Détail par cas : `intent_extraction_llm_gpt-4o-mini_cases.csv` ; sorties brutes : `intent_extraction_llm_gpt-4o-mini_outputs.jsonl`.
