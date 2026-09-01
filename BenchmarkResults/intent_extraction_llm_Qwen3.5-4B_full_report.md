# Extraction d'intention — configuration « llm_Qwen3.5-4B_full »

- Date : 2026-09-01 15:08
- Conditions : mode LLM, modèle « qwen3.5-4b » via http://localhost:1234/v1 ; prompt complet ; un seul appel par cas (pas de cascade fast→precise) ; latence totale = appel HTTP + post-traitement, colonne http_ms = aller-retour HTTP seul (réseau + inférence, non séparables côté client).
- Conditions communes : movePointDelayMs = 0 ; context.history non injecté ; « no_match » compté comme « executed » au niveau extraction ; comparaison structurelle (type de commande, paramètres appariés par type puis contenu — filtres [type, valeur, horodatage], opérateurs AND/OR positionnels, limit, order).

## Métriques globales

| Métrique | Valeur |
|---|---|
| Exactitude du type | 35/35 = 100.0 % |
| Paramètres corrects — appariement strict | 20/39 |
| Paramètres corrects — sans horodatages | 32/39 |
| Paramètres — précision (stricte) | 50.0 % (20 VP / 20 FP / 19 FN) |
| Paramètres — rappel (strict) | 51.3 % |
| Paramètres — F-mesure (stricte) | 50.6 % |
| Paramètres — F-mesure sans horodatages (diagnostic) | 81.0 % |
| Paramètres — F-mesure sans horodatages, conjonctions non ordonnées (diagnostic) | 81.0 % |
| Clarifications légitimes | 3/3 |
| Clarifications à tort | 1 |
| Clarifications manquées | 0 |
| Exactitude de l'issue | 34/35 = 97.1 % |
| Latence médiane | 1782.4 ms |
| Latence HTTP médiane (réseau + inférence) | 1782.2 ms |

## Détail par catégorie

| Catégorie | n | Type OK | Params OK (strict) | Params OK (sans ts) | F1 stricte | F1 sans horodatages | Issue OK | Latence médiane (ms) |
|---|---|---|---|---|---|---|---|---|
| anaphorique | 5 | 5/5 | 5/7 | 5/7 | 71 % | 71 % | 4/5 | 1316.3 |
| clarification | 3 | 3/3 | 0/3 | 2/3 | 0 % | 67 % | 3/3 | 2031.7 |
| descriptif | 12 | 12/12 | 5/12 | 11/12 | 42 % | 92 % | 12/12 | 1848.4 |
| déictique | 7 | 7/7 | 8/10 | 9/10 | 76 % | 86 % | 7/7 | 2016.5 |
| no_match | 1 | 1/1 | 0/1 | 1/1 | 0 % | 100 % | 1/1 | 2083.4 |
| quantifié | 5 | 5/5 | 2/6 | 4/6 | 33 % | 67 % | 5/5 | 2134.4 |
| rejet | 2 | 2/2 | 0/0 | 0/0 | 100 % | 100 % | 2/2 | 274.1 |

## Causes d'écart les plus fréquentes

- 12 × SelectionParameter: horodatages seuls
- 5 × SelectionParameter: contenu des filtres/valeurs
- 1 × PointParameter manquant
- 1 × PointParameter en trop
- 1 × SelectionParameter en trop
- 1 × SelectionParameter: limit (+ horodatages ?)

Détail par cas : `intent_extraction_llm_Qwen3.5-4B_full_cases.csv` ; sorties brutes : `intent_extraction_llm_Qwen3.5-4B_full_outputs.jsonl`.
