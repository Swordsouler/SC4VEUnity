# Extraction d'intention — configuration « rulebased »

- Date : 2026-07-31 12:34
- Conditions : mode règles (RuleBasedIntentRecognizer), sans réseau ; latence = Recognize() seul.
- Conditions communes : movePointDelayMs = 0 ; context.history non injecté ; « no_match » compté comme « executed » au niveau extraction ; comparaison structurelle (type de commande, paramètres appariés par type puis contenu — filtres [type, valeur, horodatage], opérateurs AND/OR positionnels, limit, order).

## Métriques globales

| Métrique | Valeur |
|---|---|
| Exactitude du type | 35/35 = 100.0 % |
| Paramètres corrects — appariement strict | 9/39 |
| Paramètres corrects — sans horodatages | 37/39 |
| Paramètres — précision (stricte) | 22.5 % (9 VP / 31 FP / 30 FN) |
| Paramètres — rappel (strict) | 23.1 % |
| Paramètres — F-mesure (stricte) | 22.8 % |
| Paramètres — F-mesure sans horodatages (diagnostic) | 93.7 % |
| Paramètres — F-mesure sans horodatages, conjonctions non ordonnées (diagnostic) | 93.7 % |
| Clarifications légitimes | 3/3 |
| Clarifications à tort | 0 |
| Clarifications manquées | 0 |
| Exactitude de l'issue | 35/35 = 100.0 % |
| Latence médiane | 3.7 ms |

## Détail par catégorie

| Catégorie | n | Type OK | Params OK (strict) | Params OK (sans ts) | F1 stricte | F1 sans horodatages | Issue OK | Latence médiane (ms) |
|---|---|---|---|---|---|---|---|---|
| anaphorique | 5 | 5/5 | 2/7 | 7/7 | 29 % | 100 % | 5/5 | 2.7 |
| clarification | 3 | 3/3 | 0/3 | 2/3 | 0 % | 67 % | 3/3 | 6.3 |
| descriptif | 12 | 12/12 | 3/12 | 11/12 | 25 % | 92 % | 12/12 | 3.3 |
| déictique | 7 | 7/7 | 3/10 | 10/10 | 29 % | 95 % | 7/7 | 4.2 |
| no_match | 1 | 1/1 | 0/1 | 1/1 | 0 % | 100 % | 1/1 | 7.6 |
| quantifié | 5 | 5/5 | 1/6 | 6/6 | 17 % | 100 % | 5/5 | 3.7 |
| rejet | 2 | 2/2 | 0/0 | 0/0 | 100 % | 100 % | 2/2 | 6.7 |

## Causes d'écart les plus fréquentes

- 27 × SelectionParameter: horodatages seuls
- 2 × SelectionParameter: limit (+ horodatages ?)
- 1 × PointParameter: horodatages seuls
- 1 × SelectionParameter en trop

Détail par cas : `intent_extraction_rulebased_cases.csv` ; sorties brutes : `intent_extraction_rulebased_outputs.jsonl`.
