# Extraction d'intention — configuration « llm_Qwen3.5-9B »

- Date : 2026-08-19 17:05
- Conditions : mode LLM, modèle « qwen3.5-9b » via http://localhost:1234/v1 ; prompt ALLÉGÉ (TrimExamplesSection : les exemples sont retirés, ~6 500 → ~3 200 tokens — condition expérimentale différente des modèles évalués avec le prompt complet) ; un seul appel par cas (pas de cascade fast→precise) ; latence totale = appel HTTP + post-traitement, colonne http_ms = aller-retour HTTP seul (réseau + inférence, non séparables côté client).
- Conditions communes : movePointDelayMs = 0 ; context.history non injecté ; « no_match » compté comme « executed » au niveau extraction ; comparaison structurelle (type de commande, paramètres appariés par type puis contenu — filtres [type, valeur, horodatage], opérateurs AND/OR positionnels, limit, order).

## Métriques globales

| Métrique | Valeur |
|---|---|
| Exactitude du type | 32/35 = 91.4 % |
| Paramètres corrects — appariement strict | 15/39 |
| Paramètres corrects — sans horodatages | 26/39 |
| Paramètres — précision (stricte) | 37.5 % (15 VP / 25 FP / 24 FN) |
| Paramètres — rappel (strict) | 38.5 % |
| Paramètres — F-mesure (stricte) | 38.0 % |
| Paramètres — F-mesure sans horodatages (diagnostic) | 65.8 % |
| Paramètres — F-mesure sans horodatages, conjonctions non ordonnées (diagnostic) | 65.8 % |
| Clarifications légitimes | 0/3 |
| Clarifications à tort | 0 |
| Clarifications manquées | 3 |
| Exactitude de l'issue | 32/35 = 91.4 % |
| Latence médiane | 3170.2 ms |
| Latence HTTP médiane (réseau + inférence) | 3170.0 ms |

## Détail par catégorie

| Catégorie | n | Type OK | Params OK (strict) | Params OK (sans ts) | F1 stricte | F1 sans horodatages | Issue OK | Latence médiane (ms) |
|---|---|---|---|---|---|---|---|---|
| anaphorique | 5 | 5/5 | 2/7 | 2/7 | 29 % | 29 % | 5/5 | 3116.1 |
| clarification | 3 | 0/3 | 0/3 | 0/3 | 0 % | 0 % | 0/3 | 772.0 |
| descriptif | 12 | 12/12 | 3/12 | 11/12 | 22 % | 81 % | 12/12 | 4406.8 |
| déictique | 7 | 7/7 | 8/10 | 8/10 | 76 % | 76 % | 7/7 | 3836.0 |
| no_match | 1 | 1/1 | 0/1 | 1/1 | 0 % | 100 % | 1/1 | 3941.1 |
| quantifié | 5 | 5/5 | 2/6 | 4/6 | 33 % | 67 % | 5/5 | 3128.8 |
| rejet | 2 | 2/2 | 0/0 | 0/0 | 100 % | 100 % | 2/2 | 928.8 |

## Causes d'écart les plus fréquentes

- 11 × SelectionParameter: horodatages seuls
- 9 × SelectionParameter: contenu des filtres/valeurs
- 4 × SelectionParameter en trop
- 3 × SelectionParameter manquant
- 1 × PointParameter manquant
- 1 × PointParameter en trop

Détail par cas : `intent_extraction_llm_Qwen3.5-9B_cases.csv` ; sorties brutes : `intent_extraction_llm_Qwen3.5-9B_outputs.jsonl`.
