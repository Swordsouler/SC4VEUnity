# Extraction d'intention — configuration « llm_gpt-4o »

- Date : 2026-07-31 12:56
- Conditions : mode LLM, modèle « gpt-4o » via API OpenAI ; prompt complet ; un seul appel par cas (pas de cascade fast→precise) ; latence totale = appel HTTP + post-traitement, colonne http_ms = aller-retour HTTP seul (réseau + inférence, non séparables côté client).
- Conditions communes : movePointDelayMs = 0 ; context.history non injecté ; « no_match » compté comme « executed » au niveau extraction ; comparaison structurelle (type de commande, paramètres appariés par type puis contenu — filtres [type, valeur, horodatage], opérateurs AND/OR positionnels, limit, order).

## Métriques globales

| Métrique | Valeur |
|---|---|
| Exactitude du type | 35/35 = 100.0 % |
| Paramètres corrects — appariement strict | 14/39 |
| Paramètres corrects — sans horodatages | 36/39 |
| Paramètres — précision (stricte) | 35.9 % (14 VP / 25 FP / 25 FN) |
| Paramètres — rappel (strict) | 35.9 % |
| Paramètres — F-mesure (stricte) | 35.9 % |
| Paramètres — F-mesure sans horodatages (diagnostic) | 92.3 % |
| Paramètres — F-mesure sans horodatages, conjonctions non ordonnées (diagnostic) | 92.3 % |
| Clarifications légitimes | 2/3 |
| Clarifications à tort | 1 |
| Clarifications manquées | 1 |
| Exactitude de l'issue | 33/35 = 94.3 % |
| Latence médiane | 1340.1 ms |
| Latence HTTP médiane (réseau + inférence) | 1339.3 ms |

## Détail par catégorie

| Catégorie | n | Type OK | Params OK (strict) | Params OK (sans ts) | F1 stricte | F1 sans horodatages | Issue OK | Latence médiane (ms) |
|---|---|---|---|---|---|---|---|---|
| anaphorique | 5 | 5/5 | 2/7 | 7/7 | 29 % | 100 % | 5/5 | 1222.9 |
| clarification | 3 | 3/3 | 0/3 | 2/3 | 0 % | 57 % | 2/3 | 1382.5 |
| descriptif | 12 | 12/12 | 3/12 | 11/12 | 25 % | 92 % | 12/12 | 1306.1 |
| déictique | 7 | 7/7 | 8/10 | 9/10 | 84 % | 95 % | 6/7 | 1513.8 |
| no_match | 1 | 1/1 | 0/1 | 1/1 | 0 % | 100 % | 1/1 | 1155.9 |
| quantifié | 5 | 5/5 | 1/6 | 6/6 | 17 % | 100 % | 5/5 | 1470.5 |
| rejet | 2 | 2/2 | 0/0 | 0/0 | 100 % | 100 % | 2/2 | 554.8 |

## Causes d'écart les plus fréquentes

- 22 × SelectionParameter: horodatages seuls
- 1 × SelectionParameter: contenu des filtres/valeurs
- 1 × PointParameter manquant
- 1 × SelectionParameter: limit (+ horodatages ?)
- 1 × PointParameter en trop
- 1 × JSON produit non désérialisable

Détail par cas : `intent_extraction_llm_gpt-4o_cases.csv` ; sorties brutes : `intent_extraction_llm_gpt-4o_outputs.jsonl`.
