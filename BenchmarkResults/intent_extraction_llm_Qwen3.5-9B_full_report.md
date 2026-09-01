# Extraction d'intention — configuration « llm_Qwen3.5-9B_full »

- Date : 2026-09-01 15:19
- Conditions : mode LLM, modèle « qwen3.5-9b » via http://localhost:1234/v1 ; prompt complet ; un seul appel par cas (pas de cascade fast→precise) ; latence totale = appel HTTP + post-traitement, colonne http_ms = aller-retour HTTP seul (réseau + inférence, non séparables côté client).
- Conditions communes : movePointDelayMs = 0 ; context.history non injecté ; « no_match » compté comme « executed » au niveau extraction ; comparaison structurelle (type de commande, paramètres appariés par type puis contenu — filtres [type, valeur, horodatage], opérateurs AND/OR positionnels, limit, order).

## Métriques globales

| Métrique | Valeur |
|---|---|
| Exactitude du type | 33/35 = 94.3 % |
| Paramètres corrects — appariement strict | 17/39 |
| Paramètres corrects — sans horodatages | 36/39 |
| Paramètres — précision (stricte) | 43.6 % (17 VP / 22 FP / 22 FN) |
| Paramètres — rappel (strict) | 43.6 % |
| Paramètres — F-mesure (stricte) | 43.6 % |
| Paramètres — F-mesure sans horodatages (diagnostic) | 92.3 % |
| Paramètres — F-mesure sans horodatages, conjonctions non ordonnées (diagnostic) | 92.3 % |
| Clarifications légitimes | 2/3 |
| Clarifications à tort | 0 |
| Clarifications manquées | 1 |
| Exactitude de l'issue | 33/35 = 94.3 % |
| Latence médiane | 3782.6 ms |
| Latence HTTP médiane (réseau + inférence) | 3782.4 ms |

## Détail par catégorie

| Catégorie | n | Type OK | Params OK (strict) | Params OK (sans ts) | F1 stricte | F1 sans horodatages | Issue OK | Latence médiane (ms) |
|---|---|---|---|---|---|---|---|---|
| anaphorique | 5 | 5/5 | 5/7 | 7/7 | 71 % | 100 % | 5/5 | 2800.1 |
| clarification | 3 | 2/3 | 0/3 | 2/3 | 0 % | 80 % | 2/3 | 4214.5 |
| descriptif | 12 | 11/12 | 3/12 | 11/12 | 25 % | 92 % | 11/12 | 3958.4 |
| déictique | 7 | 7/7 | 8/10 | 9/10 | 76 % | 86 % | 7/7 | 4132.8 |
| no_match | 1 | 1/1 | 0/1 | 1/1 | 0 % | 100 % | 1/1 | 4450.2 |
| quantifié | 5 | 5/5 | 1/6 | 6/6 | 17 % | 100 % | 5/5 | 4099.6 |
| rejet | 2 | 2/2 | 0/0 | 0/0 | 100 % | 100 % | 2/2 | 791.3 |

## Causes d'écart les plus fréquentes

- 19 × SelectionParameter: horodatages seuls
- 1 × SelectionParameter: contenu des filtres/valeurs
- 1 × JSON produit non désérialisable
- 1 × PointParameter manquant
- 1 × PointParameter en trop
- 1 × SelectionParameter en trop
- 1 × SelectionParameter manquant

Détail par cas : `intent_extraction_llm_Qwen3.5-9B_full_cases.csv` ; sorties brutes : `intent_extraction_llm_Qwen3.5-9B_full_outputs.jsonl`.
