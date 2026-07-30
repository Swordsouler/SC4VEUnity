# Extraction d'intention — configuration « rulebased »

- Date : 2026-07-30 16:26
- Conditions : mode règles (RuleBasedIntentRecognizer), sans réseau ; latence = Recognize() seul.
- Conditions communes : movePointDelayMs = 0 ; context.history non injecté ; « no_match » compté comme « executed » au niveau extraction ; comparaison structurelle (type de commande, paramètres appariés par type puis contenu — filtres [type, valeur, horodatage], opérateurs AND/OR positionnels, limit, order).

## Métriques globales

| Métrique | Valeur |
|---|---|
| Exactitude du type | 33/35 = 94.3 % |
| Paramètres — précision (stricte) | 21.6 % (8 VP / 29 FP / 31 FN) |
| Paramètres — rappel (strict) | 20.5 % |
| Paramètres — F-mesure (stricte) | 21.1 % |
| Paramètres — F-mesure sans horodatages (diagnostic) | 63.2 % |
| Paramètres — F-mesure sans horodatages, conjonctions non ordonnées (diagnostic) | 63.2 % |
| Clarifications légitimes | 3/3 |
| Clarifications à tort | 0 |
| Clarifications manquées | 0 |
| Exactitude de l'issue | 33/35 = 94.3 % |
| Latence médiane | 2.8 ms |

## Détail par catégorie

| Catégorie | n | Type OK | F1 stricte | F1 sans horodatages | Issue OK | Latence médiane (ms) |
|---|---|---|---|---|---|---|
| anaphorique | 5 | 5/5 | 29 % | 100 % | 5/5 | 2.4 |
| clarification | 3 | 3/3 | 0 % | 0 % | 3/3 | 2.3 |
| descriptif | 12 | 11/12 | 18 % | 73 % | 11/12 | 2.8 |
| déictique | 7 | 6/7 | 30 % | 40 % | 6/7 | 2.7 |
| no_match | 1 | 1/1 | 0 % | 100 % | 1/1 | 8.4 |
| quantifié | 5 | 5/5 | 17 % | 67 % | 5/5 | 2.5 |
| rejet | 2 | 2/2 | 100 % | 100 % | 2/2 | 8.0 |

## Causes d'écart les plus fréquentes

- 15 × SelectionParameter: horodatages seuls
- 6 × SelectionParameter: limit (+ horodatages ?)
- 4 × SelectionParameter: contenu des filtres/valeurs
- 3 × SelectionParameter: limit + ordre des filtres
- 1 × ColorParameter manquant
- 1 × SelectionParameter manquant
- 1 × PointParameter: horodatages seuls

Détail par cas : `intent_extraction_rulebased_cases.csv` ; sorties brutes : `intent_extraction_rulebased_outputs.jsonl`.
