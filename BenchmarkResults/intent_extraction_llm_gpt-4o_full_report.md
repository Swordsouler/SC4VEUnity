# Extraction d'intention — configuration « llm_gpt-4o_full »

- Date : 2026-09-01 14:48
- Conditions : mode LLM, modèle « gpt-4o » via API OpenAI ; prompt complet ; un seul appel par cas (pas de cascade fast→precise) ; latence totale = appel HTTP + post-traitement, colonne http_ms = aller-retour HTTP seul (réseau + inférence, non séparables côté client).
- Conditions communes : movePointDelayMs = 0 ; context.history non injecté ; « no_match » compté comme « executed » au niveau extraction ; comparaison structurelle (type de commande, paramètres appariés par type puis contenu — filtres [type, valeur, horodatage], opérateurs AND/OR positionnels, limit, order).

## Métriques globales

| Métrique | Valeur |
|---|---|
| Exactitude du type | 34/35 = 97.1 % |
| Paramètres corrects — appariement strict | 14/39 |
| Paramètres corrects — sans horodatages | 35/39 |
| Paramètres — précision (stricte) | 35.0 % (14 VP / 26 FP / 25 FN) |
| Paramètres — rappel (strict) | 35.9 % |
| Paramètres — F-mesure (stricte) | 35.4 % |
| Paramètres — F-mesure sans horodatages (diagnostic) | 88.6 % |
| Paramètres — F-mesure sans horodatages, conjonctions non ordonnées (diagnostic) | 88.6 % |
| Clarifications légitimes | 2/3 |
| Clarifications à tort | 0 |
| Clarifications manquées | 1 |
| Exactitude de l'issue | 33/35 = 94.3 % |
| Latence médiane | 1658.3 ms |
| Latence HTTP médiane (réseau + inférence) | 1658.0 ms |

## Détail par catégorie

| Catégorie | n | Type OK | Params OK (strict) | Params OK (sans ts) | F1 stricte | F1 sans horodatages | Issue OK | Latence médiane (ms) |
|---|---|---|---|---|---|---|---|---|
| anaphorique | 5 | 4/5 | 2/7 | 6/7 | 31 % | 92 % | 4/5 | 1613.1 |
| clarification | 3 | 3/3 | 0/3 | 2/3 | 0 % | 57 % | 2/3 | 2116.0 |
| descriptif | 12 | 12/12 | 3/12 | 11/12 | 25 % | 92 % | 12/12 | 1647.9 |
| déictique | 7 | 7/7 | 8/10 | 9/10 | 76 % | 86 % | 7/7 | 1658.3 |
| no_match | 1 | 1/1 | 0/1 | 1/1 | 0 % | 100 % | 1/1 | 1610.3 |
| quantifié | 5 | 5/5 | 1/6 | 6/6 | 17 % | 100 % | 5/5 | 1708.5 |
| rejet | 2 | 2/2 | 0/0 | 0/0 | 100 % | 100 % | 2/2 | 741.9 |

## Causes d'écart les plus fréquentes

- 21 × SelectionParameter: horodatages seuls
- 2 × PointParameter en trop
- 2 × JSON produit non désérialisable
- 1 × SelectionParameter: contenu des filtres/valeurs
- 1 × PointParameter manquant
- 1 × SelectionParameter en trop
- 1 × SelectionParameter manquant
- 1 × SelectionParameter: limit (+ horodatages ?)

Détail par cas : `intent_extraction_llm_gpt-4o_full_cases.csv` ; sorties brutes : `intent_extraction_llm_gpt-4o_full_outputs.jsonl`.
