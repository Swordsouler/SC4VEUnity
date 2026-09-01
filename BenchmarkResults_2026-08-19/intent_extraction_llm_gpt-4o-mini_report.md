# Extraction d'intention — configuration « llm_gpt-4o-mini »

- Date : 2026-08-19 17:12
- Conditions : mode LLM, modèle « gpt-4o-mini » via API OpenAI ; prompt complet ; un seul appel par cas (pas de cascade fast→precise) ; latence totale = appel HTTP + post-traitement, colonne http_ms = aller-retour HTTP seul (réseau + inférence, non séparables côté client).
- Conditions communes : movePointDelayMs = 0 ; context.history non injecté ; « no_match » compté comme « executed » au niveau extraction ; comparaison structurelle (type de commande, paramètres appariés par type puis contenu — filtres [type, valeur, horodatage], opérateurs AND/OR positionnels, limit, order).

## Métriques globales

| Métrique | Valeur |
|---|---|
| Exactitude du type | 32/35 = 91.4 % |
| Paramètres corrects — appariement strict | 14/39 |
| Paramètres corrects — sans horodatages | 33/39 |
| Paramètres — précision (stricte) | 35.9 % (14 VP / 25 FP / 25 FN) |
| Paramètres — rappel (strict) | 35.9 % |
| Paramètres — F-mesure (stricte) | 35.9 % |
| Paramètres — F-mesure sans horodatages (diagnostic) | 84.6 % |
| Paramètres — F-mesure sans horodatages, conjonctions non ordonnées (diagnostic) | 84.6 % |
| Clarifications légitimes | 1/3 |
| Clarifications à tort | 1 |
| Clarifications manquées | 2 |
| Exactitude de l'issue | 31/35 = 88.6 % |
| Latence médiane | 3009.4 ms |
| Latence HTTP médiane (réseau + inférence) | 3009.2 ms |

## Détail par catégorie

| Catégorie | n | Type OK | Params OK (strict) | Params OK (sans ts) | F1 stricte | F1 sans horodatages | Issue OK | Latence médiane (ms) |
|---|---|---|---|---|---|---|---|---|
| anaphorique | 5 | 4/5 | 2/7 | 6/7 | 31 % | 92 % | 4/5 | 2428.4 |
| clarification | 3 | 2/3 | 0/3 | 2/3 | 0 % | 67 % | 1/3 | 3175.3 |
| descriptif | 12 | 11/12 | 3/12 | 10/12 | 25 % | 83 % | 12/12 | 3197.3 |
| déictique | 7 | 7/7 | 8/10 | 8/10 | 76 % | 76 % | 6/7 | 3020.3 |
| no_match | 1 | 1/1 | 0/1 | 1/1 | 0 % | 100 % | 1/1 | 2917.1 |
| quantifié | 5 | 5/5 | 1/6 | 6/6 | 17 % | 100 % | 5/5 | 3379.1 |
| rejet | 2 | 2/2 | 0/0 | 0/0 | 100 % | 100 % | 2/2 | 664.5 |

## Causes d'écart les plus fréquentes

- 19 × SelectionParameter: horodatages seuls
- 3 × SelectionParameter: contenu des filtres/valeurs
- 2 × SelectionParameter manquant
- 2 × PointParameter en trop
- 2 × JSON produit non désérialisable
- 1 × PointParameter manquant
- 1 × SelectionParameter en trop

Détail par cas : `intent_extraction_llm_gpt-4o-mini_cases.csv` ; sorties brutes : `intent_extraction_llm_gpt-4o-mini_outputs.jsonl`.
