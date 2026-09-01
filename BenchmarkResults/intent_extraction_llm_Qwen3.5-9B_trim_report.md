# Extraction d'intention — configuration « llm_Qwen3.5-9B_trim »

- Date : 2026-09-01 15:22
- Conditions : mode LLM, modèle « qwen3.5-9b » via http://localhost:1234/v1 ; prompt ALLÉGÉ (TrimExamplesSection : les exemples sont retirés, ~6 500 → ~3 200 tokens — condition expérimentale différente des modèles évalués avec le prompt complet) ; un seul appel par cas (pas de cascade fast→precise) ; latence totale = appel HTTP + post-traitement, colonne http_ms = aller-retour HTTP seul (réseau + inférence, non séparables côté client).
- Conditions communes : movePointDelayMs = 0 ; context.history non injecté ; « no_match » compté comme « executed » au niveau extraction ; comparaison structurelle (type de commande, paramètres appariés par type puis contenu — filtres [type, valeur, horodatage], opérateurs AND/OR positionnels, limit, order).

## Métriques globales

| Métrique | Valeur |
|---|---|
| Exactitude du type | 34/35 = 97.1 % |
| Paramètres corrects — appariement strict | 17/39 |
| Paramètres corrects — sans horodatages | 26/39 |
| Paramètres — précision (stricte) | 40.5 % (17 VP / 25 FP / 22 FN) |
| Paramètres — rappel (strict) | 43.6 % |
| Paramètres — F-mesure (stricte) | 42.0 % |
| Paramètres — F-mesure sans horodatages (diagnostic) | 64.2 % |
| Paramètres — F-mesure sans horodatages, conjonctions non ordonnées (diagnostic) | 64.2 % |
| Clarifications légitimes | 1/3 |
| Clarifications à tort | 0 |
| Clarifications manquées | 2 |
| Exactitude de l'issue | 33/35 = 94.3 % |
| Latence médiane | 4314.0 ms |
| Latence HTTP médiane (réseau + inférence) | 4313.9 ms |

## Détail par catégorie

| Catégorie | n | Type OK | Params OK (strict) | Params OK (sans ts) | F1 stricte | F1 sans horodatages | Issue OK | Latence médiane (ms) |
|---|---|---|---|---|---|---|---|---|
| anaphorique | 5 | 5/5 | 2/7 | 2/7 | 29 % | 29 % | 5/5 | 3564.8 |
| clarification | 3 | 2/3 | 0/3 | 0/3 | 0 % | 0 % | 1/3 | 4731.4 |
| descriptif | 12 | 12/12 | 3/12 | 11/12 | 24 % | 88 % | 12/12 | 4340.3 |
| déictique | 7 | 7/7 | 8/10 | 8/10 | 76 % | 76 % | 7/7 | 4316.9 |
| no_match | 1 | 1/1 | 0/1 | 1/1 | 0 % | 100 % | 1/1 | 4948.9 |
| quantifié | 5 | 5/5 | 4/6 | 4/6 | 67 % | 67 % | 5/5 | 3900.4 |
| rejet | 2 | 2/2 | 0/0 | 0/0 | 100 % | 100 % | 2/2 | 1225.2 |

## Causes d'écart les plus fréquentes

- 10 × SelectionParameter: contenu des filtres/valeurs
- 9 × SelectionParameter: horodatages seuls
- 3 × PointParameter en trop
- 2 × SelectionParameter en trop
- 1 × PointParameter manquant
- 1 × SelectionParameter manquant
- 1 × SelectionParameter: limit (+ horodatages ?)

Détail par cas : `intent_extraction_llm_Qwen3.5-9B_trim_cases.csv` ; sorties brutes : `intent_extraction_llm_Qwen3.5-9B_trim_outputs.jsonl`.
