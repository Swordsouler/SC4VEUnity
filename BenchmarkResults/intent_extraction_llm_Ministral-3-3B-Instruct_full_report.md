# Extraction d'intention — configuration « llm_Ministral-3-3B-Instruct_full »

- Date : 2026-09-01 15:06
- Conditions : mode LLM, modèle « ministral-3-3b-instruct-2512 » via http://localhost:1234/v1 ; prompt complet ; un seul appel par cas (pas de cascade fast→precise) ; latence totale = appel HTTP + post-traitement, colonne http_ms = aller-retour HTTP seul (réseau + inférence, non séparables côté client).
- Conditions communes : movePointDelayMs = 0 ; context.history non injecté ; « no_match » compté comme « executed » au niveau extraction ; comparaison structurelle (type de commande, paramètres appariés par type puis contenu — filtres [type, valeur, horodatage], opérateurs AND/OR positionnels, limit, order).

## Métriques globales

| Métrique | Valeur |
|---|---|
| Exactitude du type | 32/35 = 91.4 % |
| Paramètres corrects — appariement strict | 13/39 |
| Paramètres corrects — sans horodatages | 25/39 |
| Paramètres — précision (stricte) | 33.3 % (13 VP / 26 FP / 26 FN) |
| Paramètres — rappel (strict) | 33.3 % |
| Paramètres — F-mesure (stricte) | 33.3 % |
| Paramètres — F-mesure sans horodatages (diagnostic) | 64.1 % |
| Paramètres — F-mesure sans horodatages, conjonctions non ordonnées (diagnostic) | 64.1 % |
| Clarifications légitimes | 2/3 |
| Clarifications à tort | 1 |
| Clarifications manquées | 1 |
| Exactitude de l'issue | 33/35 = 94.3 % |
| Latence médiane | 1448.4 ms |
| Latence HTTP médiane (réseau + inférence) | 1448.1 ms |

## Détail par catégorie

| Catégorie | n | Type OK | Params OK (strict) | Params OK (sans ts) | F1 stricte | F1 sans horodatages | Issue OK | Latence médiane (ms) |
|---|---|---|---|---|---|---|---|---|
| anaphorique | 5 | 4/5 | 2/7 | 6/7 | 29 % | 86 % | 5/5 | 1015.8 |
| clarification | 3 | 2/3 | 0/3 | 2/3 | 0 % | 67 % | 2/3 | 1549.6 |
| descriptif | 12 | 12/12 | 3/12 | 6/12 | 25 % | 50 % | 12/12 | 1566.7 |
| déictique | 7 | 6/7 | 7/10 | 8/10 | 70 % | 80 % | 6/7 | 1549.5 |
| no_match | 1 | 1/1 | 0/1 | 1/1 | 0 % | 100 % | 1/1 | 1600.0 |
| quantifié | 5 | 5/5 | 1/6 | 2/6 | 17 % | 33 % | 5/5 | 1482.5 |
| rejet | 2 | 2/2 | 0/0 | 0/0 | 100 % | 100 % | 2/2 | 216.6 |

## Causes d'écart les plus fréquentes

- 12 × SelectionParameter: horodatages seuls
- 11 × SelectionParameter: contenu des filtres/valeurs
- 2 × SelectionParameter manquant
- 2 × SentenceParameter en trop
- 1 × PointParameter manquant
- 1 × PointParameter en trop

Détail par cas : `intent_extraction_llm_Ministral-3-3B-Instruct_full_cases.csv` ; sorties brutes : `intent_extraction_llm_Ministral-3-3B-Instruct_full_outputs.jsonl`.
