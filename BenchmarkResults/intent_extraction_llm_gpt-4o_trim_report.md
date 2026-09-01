# Extraction d'intention — configuration « llm_gpt-4o_trim »

- Date : 2026-09-01 14:54
- Conditions : mode LLM, modèle « gpt-4o » via API OpenAI ; prompt ALLÉGÉ (TrimExamplesSection : les exemples sont retirés, ~6 500 → ~3 200 tokens — condition expérimentale différente des modèles évalués avec le prompt complet) ; un seul appel par cas (pas de cascade fast→precise) ; latence totale = appel HTTP + post-traitement, colonne http_ms = aller-retour HTTP seul (réseau + inférence, non séparables côté client).
- Conditions communes : movePointDelayMs = 0 ; context.history non injecté ; « no_match » compté comme « executed » au niveau extraction ; comparaison structurelle (type de commande, paramètres appariés par type puis contenu — filtres [type, valeur, horodatage], opérateurs AND/OR positionnels, limit, order).

## Métriques globales

| Métrique | Valeur |
|---|---|
| Exactitude du type | 35/35 = 100.0 % |
| Paramètres corrects — appariement strict | 14/39 |
| Paramètres corrects — sans horodatages | 28/39 |
| Paramètres — précision (stricte) | 35.0 % (14 VP / 26 FP / 25 FN) |
| Paramètres — rappel (strict) | 35.9 % |
| Paramètres — F-mesure (stricte) | 35.4 % |
| Paramètres — F-mesure sans horodatages (diagnostic) | 70.9 % |
| Paramètres — F-mesure sans horodatages, conjonctions non ordonnées (diagnostic) | 70.9 % |
| Clarifications légitimes | 3/3 |
| Clarifications à tort | 0 |
| Clarifications manquées | 0 |
| Exactitude de l'issue | 35/35 = 100.0 % |
| Latence médiane | 1809.8 ms |
| Latence HTTP médiane (réseau + inférence) | 1809.6 ms |

## Détail par catégorie

| Catégorie | n | Type OK | Params OK (strict) | Params OK (sans ts) | F1 stricte | F1 sans horodatages | Issue OK | Latence médiane (ms) |
|---|---|---|---|---|---|---|---|---|
| anaphorique | 5 | 5/5 | 2/7 | 2/7 | 29 % | 29 % | 5/5 | 1593.3 |
| clarification | 3 | 3/3 | 0/3 | 1/3 | 0 % | 33 % | 3/3 | 2381.6 |
| descriptif | 12 | 12/12 | 3/12 | 11/12 | 25 % | 92 % | 12/12 | 1833.6 |
| déictique | 7 | 7/7 | 8/10 | 9/10 | 76 % | 86 % | 7/7 | 1809.8 |
| no_match | 1 | 1/1 | 0/1 | 1/1 | 0 % | 100 % | 1/1 | 2722.8 |
| quantifié | 5 | 5/5 | 1/6 | 4/6 | 17 % | 67 % | 5/5 | 2213.6 |
| rejet | 2 | 2/2 | 0/0 | 0/0 | 100 % | 100 % | 2/2 | 817.9 |

## Causes d'écart les plus fréquentes

- 14 × SelectionParameter: horodatages seuls
- 8 × SelectionParameter: contenu des filtres/valeurs
- 2 × JSON produit non désérialisable
- 1 × SelectionParameter: limit + ordre des filtres
- 1 × PointParameter manquant
- 1 × PointParameter en trop
- 1 × SelectionParameter en trop
- 1 × SelectionParameter: limit (+ horodatages ?)

Détail par cas : `intent_extraction_llm_gpt-4o_trim_cases.csv` ; sorties brutes : `intent_extraction_llm_gpt-4o_trim_outputs.jsonl`.
