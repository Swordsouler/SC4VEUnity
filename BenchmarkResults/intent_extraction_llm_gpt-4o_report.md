# Extraction d'intention — configuration « llm_gpt-4o »

- Date : 2026-07-31 11:17
- Conditions : mode LLM, modèle « gpt-4o » via API OpenAI ; prompt complet ; un seul appel par cas (pas de cascade fast→precise) ; latence totale = appel HTTP + post-traitement, colonne http_ms = aller-retour HTTP seul (réseau + inférence, non séparables côté client).
- Conditions communes : movePointDelayMs = 0 ; context.history non injecté ; « no_match » compté comme « executed » au niveau extraction ; comparaison structurelle (type de commande, paramètres appariés par type puis contenu — filtres [type, valeur, horodatage], opérateurs AND/OR positionnels, limit, order).

## Métriques globales

| Métrique | Valeur |
|---|---|
| Exactitude du type | 35/35 = 100.0 % |
| Paramètres — précision (stricte) | 32.6 % (14 VP / 29 FP / 25 FN) |
| Paramètres — rappel (strict) | 35.9 % |
| Paramètres — F-mesure (stricte) | 34.1 % |
| Paramètres — F-mesure sans horodatages (diagnostic) | 78.0 % |
| Paramètres — F-mesure sans horodatages, conjonctions non ordonnées (diagnostic) | 78.0 % |
| Clarifications légitimes | 0/3 |
| Clarifications à tort | 0 |
| Clarifications manquées | 3 |
| Exactitude de l'issue | 32/35 = 91.4 % |
| Latence médiane | 1324.5 ms |
| Latence HTTP médiane (réseau + inférence) | 1324.0 ms |

## Détail par catégorie

| Catégorie | n | Type OK | F1 stricte | F1 sans horodatages | Issue OK | Latence médiane (ms) |
|---|---|---|---|---|---|---|
| anaphorique | 5 | 5/5 | 29 % | 100 % | 5/5 | 1243.5 |
| clarification | 3 | 3/3 | 0 % | 44 % | 0/3 | 1899.8 |
| descriptif | 12 | 12/12 | 25 % | 75 % | 12/12 | 1196.0 |
| déictique | 7 | 7/7 | 76 % | 76 % | 7/7 | 1452.7 |
| no_match | 1 | 1/1 | 0 % | 0 % | 1/1 | 1689.0 |
| quantifié | 5 | 5/5 | 17 % | 100 % | 5/5 | 1337.1 |
| rejet | 2 | 2/2 | 100 % | 100 % | 2/2 | 750.6 |

## Causes d'écart les plus fréquentes

- 19 × 1 relance(s) après 429 (rate limit)
- 18 × SelectionParameter: horodatages seuls
- 3 × SelectionParameter: limit (+ horodatages ?)
- 3 × SelectionParameter: contenu des filtres/valeurs
- 2 × ColorParameter en trop
- 2 × PointParameter en trop
- 2 × JSON produit non désérialisable
- 1 × PointParameter manquant
- 1 × SelectionParameter en trop

Détail par cas : `intent_extraction_llm_gpt-4o_cases.csv` ; sorties brutes : `intent_extraction_llm_gpt-4o_outputs.jsonl`.
