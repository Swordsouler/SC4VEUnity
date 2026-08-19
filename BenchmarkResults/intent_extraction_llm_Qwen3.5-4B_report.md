# Extraction d'intention — configuration « llm_Qwen3.5-4B »

- Date : 2026-08-19 17:02
- Conditions : mode LLM, modèle « qwen3.5-4b » via http://localhost:1234/v1 ; prompt ALLÉGÉ (TrimExamplesSection : les exemples sont retirés, ~6 500 → ~3 200 tokens — condition expérimentale différente des modèles évalués avec le prompt complet) ; un seul appel par cas (pas de cascade fast→precise) ; latence totale = appel HTTP + post-traitement, colonne http_ms = aller-retour HTTP seul (réseau + inférence, non séparables côté client).
- Conditions communes : movePointDelayMs = 0 ; context.history non injecté ; « no_match » compté comme « executed » au niveau extraction ; comparaison structurelle (type de commande, paramètres appariés par type puis contenu — filtres [type, valeur, horodatage], opérateurs AND/OR positionnels, limit, order).

## Métriques globales

| Métrique | Valeur |
|---|---|
| Exactitude du type | 35/35 = 100.0 % |
| Paramètres corrects — appariement strict | 18/39 |
| Paramètres corrects — sans horodatages | 29/39 |
| Paramètres — précision (stricte) | 43.9 % (18 VP / 23 FP / 21 FN) |
| Paramètres — rappel (strict) | 46.2 % |
| Paramètres — F-mesure (stricte) | 45.0 % |
| Paramètres — F-mesure sans horodatages (diagnostic) | 72.5 % |
| Paramètres — F-mesure sans horodatages, conjonctions non ordonnées (diagnostic) | 72.5 % |
| Clarifications légitimes | 2/3 |
| Clarifications à tort | 1 |
| Clarifications manquées | 1 |
| Exactitude de l'issue | 33/35 = 94.3 % |
| Latence médiane | 1966.5 ms |
| Latence HTTP médiane (réseau + inférence) | 1966.3 ms |

## Détail par catégorie

| Catégorie | n | Type OK | Params OK (strict) | Params OK (sans ts) | F1 stricte | F1 sans horodatages | Issue OK | Latence médiane (ms) |
|---|---|---|---|---|---|---|---|---|
| anaphorique | 5 | 5/5 | 2/7 | 3/7 | 29 % | 43 % | 4/5 | 1400.3 |
| clarification | 3 | 3/3 | 0/3 | 1/3 | 0 % | 29 % | 2/3 | 1967.5 |
| descriptif | 12 | 12/12 | 5/12 | 11/12 | 42 % | 92 % | 12/12 | 2115.3 |
| déictique | 7 | 7/7 | 7/10 | 9/10 | 67 % | 86 % | 7/7 | 2014.9 |
| no_match | 1 | 1/1 | 0/1 | 1/1 | 0 % | 100 % | 1/1 | 2031.3 |
| quantifié | 5 | 5/5 | 4/6 | 4/6 | 67 % | 67 % | 5/5 | 1552.8 |
| rejet | 2 | 2/2 | 0/0 | 0/0 | 100 % | 100 % | 2/2 | 258.5 |

## Causes d'écart les plus fréquentes

- 11 × SelectionParameter: horodatages seuls
- 8 × SelectionParameter: contenu des filtres/valeurs
- 1 × ColorParameter en trop
- 1 × PointParameter manquant
- 1 × PointParameter en trop
- 1 × SelectionParameter en trop
- 1 × SelectionParameter: limit (+ horodatages ?)

Détail par cas : `intent_extraction_llm_Qwen3.5-4B_cases.csv` ; sorties brutes : `intent_extraction_llm_Qwen3.5-4B_outputs.jsonl`.
