# Extraction d'intention — configuration « llm_Ministral-3-3B-Instruct »

- Date : 2026-08-19 16:58
- Conditions : mode LLM, modèle « ministral-3-3b-instruct-2512 » via http://localhost:1234/v1 ; prompt ALLÉGÉ (TrimExamplesSection : les exemples sont retirés, ~6 500 → ~3 200 tokens — condition expérimentale différente des modèles évalués avec le prompt complet) ; un seul appel par cas (pas de cascade fast→precise) ; latence totale = appel HTTP + post-traitement, colonne http_ms = aller-retour HTTP seul (réseau + inférence, non séparables côté client).
- Conditions communes : movePointDelayMs = 0 ; context.history non injecté ; « no_match » compté comme « executed » au niveau extraction ; comparaison structurelle (type de commande, paramètres appariés par type puis contenu — filtres [type, valeur, horodatage], opérateurs AND/OR positionnels, limit, order).

## Métriques globales

| Métrique | Valeur |
|---|---|
| Exactitude du type | 32/35 = 91.4 % |
| Paramètres corrects — appariement strict | 11/39 |
| Paramètres corrects — sans horodatages | 18/39 |
| Paramètres — précision (stricte) | 25.0 % (11 VP / 33 FP / 28 FN) |
| Paramètres — rappel (strict) | 28.2 % |
| Paramètres — F-mesure (stricte) | 26.5 % |
| Paramètres — F-mesure sans horodatages (diagnostic) | 43.4 % |
| Paramètres — F-mesure sans horodatages, conjonctions non ordonnées (diagnostic) | 45.8 % |
| Clarifications légitimes | 0/3 |
| Clarifications à tort | 1 |
| Clarifications manquées | 3 |
| Exactitude de l'issue | 31/35 = 88.6 % |
| Latence médiane | 1399.9 ms |
| Latence HTTP médiane (réseau + inférence) | 1399.7 ms |

## Détail par catégorie

| Catégorie | n | Type OK | Params OK (strict) | Params OK (sans ts) | F1 stricte | F1 sans horodatages | Issue OK | Latence médiane (ms) |
|---|---|---|---|---|---|---|---|---|
| anaphorique | 5 | 5/5 | 2/7 | 2/7 | 29 % | 29 % | 5/5 | 949.5 |
| clarification | 3 | 0/3 | 0/3 | 1/3 | 0 % | 20 % | 0/3 | 2099.1 |
| descriptif | 12 | 12/12 | 3/12 | 5/12 | 25 % | 42 % | 12/12 | 1465.5 |
| déictique | 7 | 7/7 | 5/10 | 8/10 | 48 % | 76 % | 6/7 | 1382.6 |
| no_match | 1 | 1/1 | 0/1 | 1/1 | 0 % | 100 % | 1/1 | 1430.1 |
| quantifié | 5 | 5/5 | 1/6 | 1/6 | 17 % | 17 % | 5/5 | 1482.9 |
| rejet | 2 | 2/2 | 0/0 | 0/0 | 100 % | 100 % | 2/2 | 183.9 |

## Causes d'écart les plus fréquentes

- 18 × SelectionParameter: contenu des filtres/valeurs
- 7 × SelectionParameter: horodatages seuls
- 3 × SentenceParameter en trop
- 2 × ColorParameter en trop
- 1 × PointParameter manquant
- 1 × PointParameter en trop
- 1 × SelectionParameter en trop
- 1 × SelectionParameter: ordre des filtres (+ horodatages)
- 1 × SelectionParameter manquant

Détail par cas : `intent_extraction_llm_Ministral-3-3B-Instruct_cases.csv` ; sorties brutes : `intent_extraction_llm_Ministral-3-3B-Instruct_outputs.jsonl`.
