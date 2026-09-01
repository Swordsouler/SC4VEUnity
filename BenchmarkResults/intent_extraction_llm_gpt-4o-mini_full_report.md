# Extraction d'intention — configuration « llm_gpt-4o-mini_full »

- Date : 2026-09-01 14:55
- Conditions : mode LLM, modèle « gpt-4o-mini » via API OpenAI ; prompt complet ; un seul appel par cas (pas de cascade fast→precise) ; latence totale = appel HTTP + post-traitement, colonne http_ms = aller-retour HTTP seul (réseau + inférence, non séparables côté client).
- Conditions communes : movePointDelayMs = 0 ; context.history non injecté ; « no_match » compté comme « executed » au niveau extraction ; comparaison structurelle (type de commande, paramètres appariés par type puis contenu — filtres [type, valeur, horodatage], opérateurs AND/OR positionnels, limit, order).

## Métriques globales

| Métrique | Valeur |
|---|---|
| Exactitude du type | 34/35 = 97.1 % |
| Paramètres corrects — appariement strict | 14/39 |
| Paramètres corrects — sans horodatages | 31/39 |
| Paramètres — précision (stricte) | 35.0 % (14 VP / 26 FP / 25 FN) |
| Paramètres — rappel (strict) | 35.9 % |
| Paramètres — F-mesure (stricte) | 35.4 % |
| Paramètres — F-mesure sans horodatages (diagnostic) | 78.5 % |
| Paramètres — F-mesure sans horodatages, conjonctions non ordonnées (diagnostic) | 78.5 % |
| Clarifications légitimes | 2/3 |
| Clarifications à tort | 1 |
| Clarifications manquées | 1 |
| Exactitude de l'issue | 32/35 = 91.4 % |
| Latence médiane | 1342.9 ms |
| Latence HTTP médiane (réseau + inférence) | 1342.6 ms |

## Détail par catégorie

| Catégorie | n | Type OK | Params OK (strict) | Params OK (sans ts) | F1 stricte | F1 sans horodatages | Issue OK | Latence médiane (ms) |
|---|---|---|---|---|---|---|---|---|
| anaphorique | 5 | 4/5 | 2/7 | 6/7 | 31 % | 92 % | 4/5 | 1236.6 |
| clarification | 3 | 3/3 | 0/3 | 1/3 | 0 % | 29 % | 2/3 | 1598.6 |
| descriptif | 12 | 12/12 | 3/12 | 10/12 | 25 % | 83 % | 12/12 | 1424.3 |
| déictique | 7 | 7/7 | 8/10 | 8/10 | 76 % | 76 % | 6/7 | 1520.0 |
| no_match | 1 | 1/1 | 0/1 | 1/1 | 0 % | 100 % | 1/1 | 1640.1 |
| quantifié | 5 | 5/5 | 1/6 | 5/6 | 17 % | 83 % | 5/5 | 1472.0 |
| rejet | 2 | 2/2 | 0/0 | 0/0 | 100 % | 100 % | 2/2 | 690.6 |

## Causes d'écart les plus fréquentes

- 17 × SelectionParameter: horodatages seuls
- 6 × SelectionParameter: contenu des filtres/valeurs
- 2 × PointParameter en trop
- 2 × JSON produit non désérialisable
- 1 × PointParameter manquant
- 1 × SelectionParameter en trop
- 1 × SelectionParameter manquant

Détail par cas : `intent_extraction_llm_gpt-4o-mini_full_cases.csv` ; sorties brutes : `intent_extraction_llm_gpt-4o-mini_full_outputs.jsonl`.
