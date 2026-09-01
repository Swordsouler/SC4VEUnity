# Extraction d'intention — configuration « llm_Qwen3.5-4B_trim »

- Date : 2026-09-01 15:10
- Conditions : mode LLM, modèle « qwen3.5-4b » via http://localhost:1234/v1 ; prompt ALLÉGÉ (TrimExamplesSection : les exemples sont retirés, ~6 500 → ~3 200 tokens — condition expérimentale différente des modèles évalués avec le prompt complet) ; un seul appel par cas (pas de cascade fast→precise) ; latence totale = appel HTTP + post-traitement, colonne http_ms = aller-retour HTTP seul (réseau + inférence, non séparables côté client).
- Conditions communes : movePointDelayMs = 0 ; context.history non injecté ; « no_match » compté comme « executed » au niveau extraction ; comparaison structurelle (type de commande, paramètres appariés par type puis contenu — filtres [type, valeur, horodatage], opérateurs AND/OR positionnels, limit, order).

## Métriques globales

| Métrique | Valeur |
|---|---|
| Exactitude du type | 34/35 = 97.1 % |
| Paramètres corrects — appariement strict | 17/39 |
| Paramètres corrects — sans horodatages | 27/39 |
| Paramètres — précision (stricte) | 42.5 % (17 VP / 23 FP / 22 FN) |
| Paramètres — rappel (strict) | 43.6 % |
| Paramètres — F-mesure (stricte) | 43.0 % |
| Paramètres — F-mesure sans horodatages (diagnostic) | 68.4 % |
| Paramètres — F-mesure sans horodatages, conjonctions non ordonnées (diagnostic) | 68.4 % |
| Clarifications légitimes | 2/3 |
| Clarifications à tort | 1 |
| Clarifications manquées | 1 |
| Exactitude de l'issue | 33/35 = 94.3 % |
| Latence médiane | 1583.6 ms |
| Latence HTTP médiane (réseau + inférence) | 1583.4 ms |

## Détail par catégorie

| Catégorie | n | Type OK | Params OK (strict) | Params OK (sans ts) | F1 stricte | F1 sans horodatages | Issue OK | Latence médiane (ms) |
|---|---|---|---|---|---|---|---|---|
| anaphorique | 5 | 5/5 | 2/7 | 2/7 | 29 % | 29 % | 4/5 | 1451.5 |
| clarification | 3 | 3/3 | 0/3 | 1/3 | 0 % | 29 % | 2/3 | 2032.5 |
| descriptif | 12 | 11/12 | 4/12 | 10/12 | 35 % | 87 % | 12/12 | 2149.3 |
| déictique | 7 | 7/7 | 8/10 | 9/10 | 76 % | 86 % | 7/7 | 2064.5 |
| no_match | 1 | 1/1 | 0/1 | 1/1 | 0 % | 100 % | 1/1 | 2096.2 |
| quantifié | 5 | 5/5 | 3/6 | 4/6 | 50 % | 67 % | 5/5 | 1567.3 |
| rejet | 2 | 2/2 | 0/0 | 0/0 | 100 % | 100 % | 2/2 | 260.6 |

## Causes d'écart les plus fréquentes

- 10 × SelectionParameter: horodatages seuls
- 9 × SelectionParameter: contenu des filtres/valeurs
- 1 × ColorParameter en trop
- 1 × SelectionParameter manquant
- 1 × PointParameter manquant
- 1 × PointParameter en trop
- 1 × SelectionParameter en trop
- 1 × SelectionParameter: limit (+ horodatages ?)

Détail par cas : `intent_extraction_llm_Qwen3.5-4B_trim_cases.csv` ; sorties brutes : `intent_extraction_llm_Qwen3.5-4B_trim_outputs.jsonl`.
