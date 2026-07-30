# Extraction d'intention — configuration « llm_Qwen3-4B-Instruct »

- Date : 2026-07-30 17:37
- Conditions : mode LLM, modèle « qwen/qwen3-4b-2507 » via http://localhost:1234/v1 ; prompt ALLÉGÉ (TrimExamplesSection : les exemples sont retirés, ~6 500 → ~3 200 tokens — condition expérimentale différente des modèles évalués avec le prompt complet) ; un seul appel par cas (pas de cascade fast→precise) ; latence totale = appel HTTP + post-traitement, colonne http_ms = aller-retour HTTP seul (réseau + inférence, non séparables côté client).
- Conditions communes : movePointDelayMs = 0 ; context.history non injecté ; « no_match » compté comme « executed » au niveau extraction ; comparaison structurelle (type de commande, paramètres appariés par type puis contenu — filtres [type, valeur, horodatage], opérateurs AND/OR positionnels, limit, order).

## Métriques globales

| Métrique | Valeur |
|---|---|
| Exactitude du type | 32/35 = 91.4 % |
| Paramètres — précision (stricte) | 29.8 % (14 VP / 33 FP / 25 FN) |
| Paramètres — rappel (strict) | 35.9 % |
| Paramètres — F-mesure (stricte) | 32.6 % |
| Paramètres — F-mesure sans horodatages (diagnostic) | 51.2 % |
| Paramètres — F-mesure sans horodatages, conjonctions non ordonnées (diagnostic) | 51.2 % |
| Clarifications légitimes | 0/3 |
| Clarifications à tort | 2 |
| Clarifications manquées | 3 |
| Exactitude de l'issue | 28/35 = 80.0 % |
| Latence médiane | 7932.4 ms |
| Latence HTTP médiane (réseau + inférence) | 7932.2 ms |

## Détail par catégorie

| Catégorie | n | Type OK | F1 stricte | F1 sans horodatages | Issue OK | Latence médiane (ms) |
|---|---|---|---|---|---|---|
| anaphorique | 5 | 5/5 | 29 % | 29 % | 5/5 | 7932.4 |
| clarification | 3 | 3/3 | 0 % | 44 % | 0/3 | 86618.5 |
| descriptif | 12 | 11/12 | 30 % | 52 % | 11/12 | 1603.5 |
| déictique | 7 | 7/7 | 70 % | 90 % | 6/7 | 6453.1 |
| no_match | 1 | 1/1 | 0 % | 0 % | 1/1 | 52226.3 |
| quantifié | 5 | 5/5 | 17 % | 33 % | 5/5 | 19001.5 |
| rejet | 2 | 0/2 | 0 % | 0 % | 0/2 | 21941.4 |

## Causes d'écart les plus fréquentes

- 16 × SelectionParameter: contenu des filtres/valeurs
- 8 × SelectionParameter: horodatages seuls
- 4 × SelectionParameter en trop
- 2 × ColorParameter en trop
- 2 × SentenceParameter en trop
- 1 × PointParameter manquant
- 1 × PointParameter en trop

Détail par cas : `intent_extraction_llm_Qwen3-4B-Instruct_cases.csv` ; sorties brutes : `intent_extraction_llm_Qwen3-4B-Instruct_outputs.jsonl`.
