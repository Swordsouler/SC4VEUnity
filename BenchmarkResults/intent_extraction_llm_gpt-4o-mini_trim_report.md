# Extraction d'intention — configuration « llm_gpt-4o-mini_trim »

- Date : 2026-09-01 15:00
- Conditions : mode LLM, modèle « gpt-4o-mini » via API OpenAI ; prompt ALLÉGÉ (TrimExamplesSection : les exemples sont retirés, ~6 500 → ~3 200 tokens — condition expérimentale différente des modèles évalués avec le prompt complet) ; un seul appel par cas (pas de cascade fast→precise) ; latence totale = appel HTTP + post-traitement, colonne http_ms = aller-retour HTTP seul (réseau + inférence, non séparables côté client).
- Conditions communes : movePointDelayMs = 0 ; context.history non injecté ; « no_match » compté comme « executed » au niveau extraction ; comparaison structurelle (type de commande, paramètres appariés par type puis contenu — filtres [type, valeur, horodatage], opérateurs AND/OR positionnels, limit, order).

## Métriques globales

| Métrique | Valeur |
|---|---|
| Exactitude du type | 32/35 = 91.4 % |
| Paramètres corrects — appariement strict | 15/39 |
| Paramètres corrects — sans horodatages | 25/39 |
| Paramètres — précision (stricte) | 35.7 % (15 VP / 27 FP / 24 FN) |
| Paramètres — rappel (strict) | 38.5 % |
| Paramètres — F-mesure (stricte) | 37.0 % |
| Paramètres — F-mesure sans horodatages (diagnostic) | 61.7 % |
| Paramètres — F-mesure sans horodatages, conjonctions non ordonnées (diagnostic) | 61.7 % |
| Clarifications légitimes | 0/3 |
| Clarifications à tort | 0 |
| Clarifications manquées | 3 |
| Exactitude de l'issue | 30/35 = 85.7 % |
| Latence médiane | 1538.2 ms |
| Latence HTTP médiane (réseau + inférence) | 1537.9 ms |

## Détail par catégorie

| Catégorie | n | Type OK | Params OK (strict) | Params OK (sans ts) | F1 stricte | F1 sans horodatages | Issue OK | Latence médiane (ms) |
|---|---|---|---|---|---|---|---|---|
| anaphorique | 5 | 4/5 | 2/7 | 3/7 | 31 % | 46 % | 4/5 | 1171.4 |
| clarification | 3 | 3/3 | 0/3 | 1/3 | 0 % | 22 % | 0/3 | 1538.2 |
| descriptif | 12 | 11/12 | 4/12 | 8/12 | 32 % | 64 % | 12/12 | 1790.6 |
| déictique | 7 | 6/7 | 8/10 | 8/10 | 80 % | 80 % | 6/7 | 1624.0 |
| no_match | 1 | 1/1 | 0/1 | 1/1 | 0 % | 100 % | 1/1 | 1594.5 |
| quantifié | 5 | 5/5 | 1/6 | 4/6 | 17 % | 67 % | 5/5 | 1508.9 |
| rejet | 2 | 2/2 | 0/0 | 0/0 | 100 % | 100 % | 2/2 | 484.9 |

## Causes d'écart les plus fréquentes

- 12 × SelectionParameter: contenu des filtres/valeurs
- 10 × SelectionParameter: horodatages seuls
- 2 × ColorParameter en trop
- 2 × PointParameter en trop
- 2 × JSON produit non désérialisable
- 1 × SelectionParameter en trop
- 1 × PointParameter manquant
- 1 × SelectionParameter manquant

Détail par cas : `intent_extraction_llm_gpt-4o-mini_trim_cases.csv` ; sorties brutes : `intent_extraction_llm_gpt-4o-mini_trim_outputs.jsonl`.
