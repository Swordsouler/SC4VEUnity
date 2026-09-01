# Extraction d'intention — configuration « llm_Ministral-3-8B-Instruct_trim »

- Date : 2026-09-01 15:17
- Conditions : mode LLM, modèle « ministral-3-8b-instruct-2512 » via http://localhost:1234/v1 ; prompt ALLÉGÉ (TrimExamplesSection : les exemples sont retirés, ~6 500 → ~3 200 tokens — condition expérimentale différente des modèles évalués avec le prompt complet) ; un seul appel par cas (pas de cascade fast→precise) ; latence totale = appel HTTP + post-traitement, colonne http_ms = aller-retour HTTP seul (réseau + inférence, non séparables côté client).
- Conditions communes : movePointDelayMs = 0 ; context.history non injecté ; « no_match » compté comme « executed » au niveau extraction ; comparaison structurelle (type de commande, paramètres appariés par type puis contenu — filtres [type, valeur, horodatage], opérateurs AND/OR positionnels, limit, order).

## Métriques globales

| Métrique | Valeur |
|---|---|
| Exactitude du type | 33/35 = 94.3 % |
| Paramètres corrects — appariement strict | 11/39 |
| Paramètres corrects — sans horodatages | 17/39 |
| Paramètres — précision (stricte) | 25.6 % (11 VP / 32 FP / 28 FN) |
| Paramètres — rappel (strict) | 28.2 % |
| Paramètres — F-mesure (stricte) | 26.8 % |
| Paramètres — F-mesure sans horodatages (diagnostic) | 41.5 % |
| Paramètres — F-mesure sans horodatages, conjonctions non ordonnées (diagnostic) | 48.8 % |
| Clarifications légitimes | 0/3 |
| Clarifications à tort | 1 |
| Clarifications manquées | 3 |
| Exactitude de l'issue | 31/35 = 88.6 % |
| Latence médiane | 3298.9 ms |
| Latence HTTP médiane (réseau + inférence) | 3298.6 ms |

## Détail par catégorie

| Catégorie | n | Type OK | Params OK (strict) | Params OK (sans ts) | F1 stricte | F1 sans horodatages | Issue OK | Latence médiane (ms) |
|---|---|---|---|---|---|---|---|---|
| anaphorique | 5 | 5/5 | 2/7 | 2/7 | 29 % | 29 % | 5/5 | 2416.5 |
| clarification | 3 | 2/3 | 0/3 | 0/3 | 0 % | 0 % | 0/3 | 3831.6 |
| descriptif | 12 | 12/12 | 3/12 | 7/12 | 25 % | 58 % | 12/12 | 3216.6 |
| déictique | 7 | 6/7 | 5/10 | 5/10 | 45 % | 45 % | 6/7 | 3151.8 |
| no_match | 1 | 1/1 | 0/1 | 1/1 | 0 % | 100 % | 1/1 | 3469.0 |
| quantifié | 5 | 5/5 | 1/6 | 2/6 | 17 % | 33 % | 5/5 | 3747.0 |
| rejet | 2 | 2/2 | 0/0 | 0/0 | 100 % | 100 % | 2/2 | 439.3 |

## Causes d'écart les plus fréquentes

- 17 × SelectionParameter: contenu des filtres/valeurs
- 6 × SelectionParameter: horodatages seuls
- 3 × SelectionParameter: ordre des filtres (+ horodatages)
- 2 × ColorParameter en trop
- 2 × SelectionParameter en trop
- 2 × JSON produit non désérialisable
- 1 × PointParameter manquant
- 1 × PointParameter en trop
- 1 × SelectionParameter manquant
- 1 × SentenceParameter en trop

Détail par cas : `intent_extraction_llm_Ministral-3-8B-Instruct_trim_cases.csv` ; sorties brutes : `intent_extraction_llm_Ministral-3-8B-Instruct_trim_outputs.jsonl`.
