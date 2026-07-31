# Extraction d'intention — configuration « llm_gpt-4o-mini »

- Date : 2026-07-31 12:47
- Conditions : mode LLM, modèle « gpt-4o-mini » via API OpenAI ; prompt complet ; un seul appel par cas (pas de cascade fast→precise) ; latence totale = appel HTTP + post-traitement, colonne http_ms = aller-retour HTTP seul (réseau + inférence, non séparables côté client).
- Conditions communes : movePointDelayMs = 0 ; context.history non injecté ; « no_match » compté comme « executed » au niveau extraction ; comparaison structurelle (type de commande, paramètres appariés par type puis contenu — filtres [type, valeur, horodatage], opérateurs AND/OR positionnels, limit, order).

## Métriques globales

| Métrique | Valeur |
|---|---|
| Exactitude du type | 34/35 = 97.1 % |
| Paramètres corrects — appariement strict | 14/39 |
| Paramètres corrects — sans horodatages | 29/39 |
| Paramètres — précision (stricte) | 34.1 % (14 VP / 27 FP / 25 FN) |
| Paramètres — rappel (strict) | 35.9 % |
| Paramètres — F-mesure (stricte) | 35.0 % |
| Paramètres — F-mesure sans horodatages (diagnostic) | 72.5 % |
| Paramètres — F-mesure sans horodatages, conjonctions non ordonnées (diagnostic) | 72.5 % |
| Clarifications légitimes | 1/3 |
| Clarifications à tort | 1 |
| Clarifications manquées | 2 |
| Exactitude de l'issue | 31/35 = 88.6 % |
| Latence médiane | 1451.5 ms |
| Latence HTTP médiane (réseau + inférence) | 1450.5 ms |

## Détail par catégorie

| Catégorie | n | Type OK | Params OK (strict) | Params OK (sans ts) | F1 stricte | F1 sans horodatages | Issue OK | Latence médiane (ms) |
|---|---|---|---|---|---|---|---|---|
| anaphorique | 5 | 4/5 | 2/7 | 5/7 | 31 % | 77 % | 4/5 | 1274.2 |
| clarification | 3 | 3/3 | 0/3 | 0/3 | 0 % | 0 % | 1/3 | 1649.4 |
| descriptif | 12 | 12/12 | 3/12 | 9/12 | 25 % | 75 % | 12/12 | 1458.2 |
| déictique | 7 | 7/7 | 8/10 | 8/10 | 76 % | 76 % | 6/7 | 1451.5 |
| no_match | 1 | 1/1 | 0/1 | 1/1 | 0 % | 100 % | 1/1 | 1640.4 |
| quantifié | 5 | 5/5 | 1/6 | 6/6 | 17 % | 100 % | 5/5 | 1577.8 |
| rejet | 2 | 2/2 | 0/0 | 0/0 | 100 % | 100 % | 2/2 | 548.2 |

## Causes d'écart les plus fréquentes

- 15 × SelectionParameter: horodatages seuls
- 8 × SelectionParameter: contenu des filtres/valeurs
- 2 × PointParameter en trop
- 2 × JSON produit non désérialisable
- 1 × ColorParameter en trop
- 1 × PointParameter manquant
- 1 × SelectionParameter en trop
- 1 × SelectionParameter manquant

Détail par cas : `intent_extraction_llm_gpt-4o-mini_cases.csv` ; sorties brutes : `intent_extraction_llm_gpt-4o-mini_outputs.jsonl`.
