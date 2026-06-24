# Benchmark des interactions multimodales en environnement virtuel

Cadre **générique** pour décrire, catégoriser et **mesurer le degré de multimodalité** des
commandes d'un environnement virtuel (EV). Le but n'est pas de tester un projet précis, mais de
fournir une base réutilisable, ancrée dans la littérature HCI, que l'on étendra à d'autres EV.
**SC4VE/SVEN sert ici de première instanciation** (cas d'amorçage).

> ⚠️ Document vivant. La taxonomie (§3–4) et la grille de notation (§5) sont les contributions
> réutilisables ; le catalogue SC4VE (§4.3) est un exemple à dupliquer pour chaque nouvel EV.

## Sommaire

1. [Idée directrice](#1-idée-directrice)
2. [Fondements théoriques](#2-fondements-théoriques)
3. [Dimensions d'analyse d'une commande](#3-dimensions-danalyse-dune-commande)
4. [Typologie : patterns d'interaction et catalogue de commandes](#4-typologie--patterns-dinteraction-et-catalogue-de-commandes)
5. [Grille du « degré de multimodalité » d'un EV](#5-grille-du--degré-de-multimodalité--dun-ev)
6. [Application : profil de SC4VE](#6-application--profil-de-sc4ve)
7. [Espace d'extension — commandes non couvertes](#7-espace-dextension--commandes-non-couvertes)
8. [Protocole d'extension à un nouvel EV](#8-protocole-dextension-à-un-nouvel-ev)
9. [Limites & travaux futurs](#9-limites--travaux-futurs)
10. [Références](#10-références)

---

## 1. Idée directrice

**La multimodalité n'est pas une propriété de la *commande*, mais du couple (commande, énoncé).**

Une même commande couvre un *espace* d'interactions de richesse variable :

| Énoncé | Modalités | Degré |
|--------|-----------|-------|
| « colorie les pommes en rouge » | voix seule (description) | unimodal |
| « colorie **ça** en rouge » | voix + pointage (déixis) | bimodal synergique |
| « colorie-**les** en rouge » | voix + mémoire du dialogue (anaphore) | bimodal inter-tour |

Conséquence pour le benchmark :

- Le **degré d'un énoncé** = quel *pattern d'interaction* (§4) il instancie.
- Le **degré d'un EV** = l'**enveloppe** des patterns / classes de fusion / relations CARE que son
  jeu de commandes **et son moteur de résolution** savent réellement traiter (§5).

C'est cette enveloppe que la grille du §5 quantifie.

---

## 2. Fondements théoriques

Le cadre s'appuie sur le canon de l'interaction multimodale (école française HCI très présente :
Grenoble pour CARE/CASE, LIMSI/Paris-Saclay pour TYCOON — lignée dont SC4VE/LISN est proche).

| Référence | Apport mobilisé ici |
|-----------|---------------------|
| **Bolt (1980)** — *Put-that-there* | Archétype voix + geste déictique ; « déplace ça ici » en est la réplique directe. |
| **Nigay & Coutaz (1993)** — *CASE design space* | Échelle ordinale de fusion → sert d'axe principal du « degré ». |
| **Coutaz, Nigay, Salber et al. (1995)** — *CARE properties* | Caractérise la *relation* entre modalités pour une commande donnée. |
| **Martin (1998)** — *TYCOON* | 6 types de **coopération** entre modalités (vue complémentaire de CARE). |
| **Oviatt (1999)** — *Ten myths* | Désambiguïsation mutuelle ; les modalités ne sont pas redondantes mais complémentaires. |
| **Bernsen (2008)** — *Multimodality Theory* | Taxonomie des modalités d'entrée/sortie (inventaire de l'axe « largeur modale »). |
| **Dumas, Lalanne & Oviatt (2009)** — *Survey* | Niveaux de fusion (lexical / syntaxique / sémantique ; early vs late). |
| **Reeves et al. (2004)** | Lignes directrices de conception (cadrage « bonnes pratiques »). |

### 2.1 CASE — l'espace de conception (Nigay & Coutaz, 1993)

Deux axes :

- **Usage des modalités** : *séquentiel* vs *parallèle* (dans le temps).
- **Fusion** : *combinée* (un seul énoncé) vs *indépendante* (des énoncés disjoints).

→ quatre classes, **ordonnées par richesse** :

| Classe | Usage | Fusion | Définition |
|--------|-------|--------|------------|
| **Exclusive** | séquentiel | indépendante | une seule modalité à la fois, sans fusion. |
| **Alternate** | séquentiel | combinée | modalités utilisées l'une après l'autre pour *un même* but. |
| **Concurrent** | parallèle | indépendante | plusieurs modalités en parallèle, traitées séparément. |
| **Synergistic** | parallèle | combinée | modalités fusionnées en un sens unique (le tout > somme des parties). |

C'est l'**échelle ordinale 0→3** de notre axe CASE (§5).

### 2.2 CARE — les propriétés de combinaison (Coutaz et al., 1995)

Caractérise comment, pour passer d'un état `s` à `s'`, les modalités d'un ensemble *M* se relient
(`tw` = fenêtre temporelle de fusion) :

| Propriété | Définition (paraphrase fidèle de la source) |
|-----------|---------------------------------------------|
| **Equivalence** | n'importe quelle modalité de *M* suffit (choix offert à l'utilisateur, sans contrainte temporelle). |
| **Assignment** | une seule modalité est utilisable / est choisie pour la transition (pas d'alternative). |
| **Redundancy** | les modalités sont équivalentes **et** toutes utilisées dans la même fenêtre `tw` (même information, en double). |
| **Complementarity** | toutes les modalités doivent être utilisées dans `tw` ; aucune seule ne couvre le but. |

> *Put-that-there* = **Complementarity** typique : « déplace » (voix) ne suffit pas, « ça/ici »
> (pointage) ne suffit pas — il faut les deux, dans la même fenêtre.

### 2.3 TYCOON — types de coopération (Martin, 1998)

Six types, utiles pour nommer finement la coopération : **Équivalence, Transfert, Spécialisation,
Redondance, Complémentarité, Concurrence**. On les utilise comme étiquette secondaire des patterns
(§4) — p. ex. la coréférence inter-tour relève du **Transfert** (une modalité réutilise le résultat
d'une autre dans le temps).

### 2.4 Niveaux de fusion (Dumas, Lalanne & Oviatt, 2009)

- **Lexical** : association signal ↔ token.
- **Syntaxique** : structure / ordre (p. ex. l'enchaînement « trois fois »).
- **Sémantique** : intégration du *sens* (p. ex. fusionner « ça » + cible pointée en une référence
  d'objet). C'est le niveau visé par un EV synergique.
- *Early* vs *late fusion* : fusion des signaux avant interprétation vs fusion des résultats après
  reconnaissance par modalité (SC4VE = *late*, fusion au niveau sémantique dans le graphe RDF).

---

## 3. Dimensions d'analyse d'une commande

Les **colonnes** du catalogue (§4.3) et les **axes** de la grille (§5) dérivent de 7 dimensions :

| # | Dimension | Valeurs | Ancrage |
|---|-----------|---------|---------|
| **D1** | Modalités d'entrée mobilisées | Voix (V), Pointage/déixis (P), Regard (R), Manipulation directe (M), Mémoire du dialogue (C) | Bernsen |
| **D2** | Fonction de la modalité | Prédicat/commande (le verbe) · Référence/désignation (la cible) | linguistique (deixis) |
| **D3** | Type de référence à la cible | **Desc**riptive (catégorie/attribut) · **Déic**tique (pointage/regard) · **Ana**phorique (coréférence) · **Quant**ifiée/ordinale | deixis vs anaphore |
| **D4** | Relation entre modalités | CARE : Equivalence / Assignment / Redundancy / Complementarity | Coutaz et al. |
| **D5** | Classe de fusion | CASE : Exclusive / Alternate / Concurrent / Synergistic | Nigay & Coutaz |
| **D6** | Niveau d'intégration | lexical / syntaxique / sémantique | Dumas et al. |
| **D7** | Contrainte temporelle | aucune · fenêtre globale · fenêtre par énoncé · **alignement fin par horodatage de token** | CARE (`tw`) |

---

## 4. Typologie : patterns d'interaction et catalogue de commandes

### 4.1 Patterns d'interaction (le cœur littérature)

Chaque pattern est un point caractéristique de l'espace des dimensions. **C'est à ce niveau que vit
la catégorisation multimodale.** Les commandes (§4.3) ne font qu'*instancier* un ou plusieurs
patterns.

| Pattern | Énoncé type | Modalités (D1) | Référence (D3) | CARE (D4) | TYCOON | CASE (D5) | Fusion (D6) | Temporel (D7) |
|---------|-------------|----------------|----------------|-----------|--------|-----------|-------------|---------------|
| **P0** Vocal descriptif | « colorie les pommes rouges » | V | Desc | Assignment | — | Exclusive | sémantique (intra-voix) | aucune |
| **P1** Vocal quantifié/ordinal | « les 3 plus petites voitures » | V | Quant | Assignment | Spécialisation | Exclusive | sémantique | aucune |
| **P2** Vocal + déixis *(Put-that-there)* | « déplace **ça** **ici** » | V + P | Déic | **Complementarity** | Complémentarité | **Synergistic** | sémantique | **token ↔ pointeur** |
| **P3** Déixis multiple | « mesure entre **ça** et **ça** » | V + P×2 | Déic×2 | Complementarity | Complémentarité | Synergistic | sémantique | token×2 |
| **P4** Vocal + regard | « colorie **ce que je vois** » | V + R | Déic (gaze) | Complementarity | Complémentarité | Synergistic | sémantique | frustum @ token |
| **P5** Vocal + coréférence | « colorie-**les** » | V + C | Ana | Complementarity (inter-tour) | **Transfert** | Synergistic (inter-tour) | sémantique/pragmatique | mémoire du dialogue |
| **P6** Ambiguïté déixis/anaphore | « ça » (pointé ? ou repris ?) | V + P/C | Déic \| Ana | — | désambiguïsation mutuelle | Synergistic | token + contexte |
| **P7** Enchaînement temporel | « **trois fois** assombris… » | V | — | — | Concurrence | Exclusive (×N) | syntaxique | séquence |
| **P8** Clarification (fission sortie) | EV : « Sur quels objets ? » | V (sortie) | — | — | — (fission) | — | tour de dialogue | dialogue |

> **P6** est le cas dur, et le marqueur d'un EV mature : « ça » + destination → déixis ; « ça » seul
> après une action → anaphore. La résolution exige un raisonnement contextuel (cf. Oviatt,
> désambiguïsation mutuelle).

### 4.2 Légende des colonnes du catalogue

- **Modalités** : V P R M C (cf. D1).
- **Réf.** : Desc / Déic / Ana / Quant (cf. D3) — modes de désignation *acceptés* par la commande.
- **CASE max** : classe de fusion la plus riche atteignable par la commande (selon l'énoncé).
- **Params** : paramètres requis (déclenchent une clarification P8 si absents).

### 4.3 Catalogue SC4VE (35 commandes vocales + 1 système)

> Toutes les commandes à sélection acceptent les modes Desc/Déic/Regard/Ana/Quant ; leur **CASE max
> = Synergistic** car « <verbe> ça » est toujours possible. Les commandes *sans cible* restent
> **Exclusive** (pure commande vocale, aucune fusion). C'est précisément ce contraste qui peuple la
> grille du §5.

#### A. Sélection & dialogue

| Commande | Exemple | Modalités | Réf. | CASE max | Params |
|----------|---------|-----------|------|----------|--------|
| `SelectCommand` | « sélectionne les pommes rouges » | V P R C | Desc Déic Ana Quant | Synergistic | Selection |
| `UnselectCommand` | « désélectionne tout » | V P C | Desc Déic Ana | Synergistic | Selection *(opt.)* |
| `SelectAllCommand` | « sélectionne tout » | V | — | Exclusive | — |
| `InvertSelectionCommand` | « inverse la sélection » | V | — | Exclusive | — |

#### B. Transformation spatiale

| Commande | Exemple | Modalités | Réf. | CASE max | Params |
|----------|---------|-----------|------|----------|--------|
| `MoveCommand` | « déplace ça ici » | V P R C | Déic Desc Ana | **Synergistic** (P2/P3) | Selection(src) + Point/Selection(dest) |
| `RotateRightCommand` | « tourne la voiture à droite » | V P R C | Desc Déic Ana | Synergistic | Selection |
| `RotateLeftCommand` | « tourne les pommes à gauche » | V P R C | Desc Déic Ana | Synergistic | Selection |
| `FlipCommand` | « retourne la citrouille » | V P R C | Desc Déic Ana | Synergistic | Selection |
| `ScaleUpCommand` | « agrandis les légumes » | V P R C | Desc Déic Ana Quant | Synergistic | Selection |
| `ScaleDownCommand` | « réduis ça » | V P R C | Déic Desc Ana | Synergistic | Selection |
| `AlignCommand` | « aligne les pommes » | V P R C | Desc Déic Ana | Synergistic | Selection |
| `SnapToGroundCommand` | « pose ça au sol » | V P R C | Déic Desc Ana | Synergistic | Selection |

#### C. Couleur

| Commande | Exemple | Modalités | Réf. | CASE max | Params |
|----------|---------|-----------|------|----------|--------|
| `ColorizeCommand` | « colorie en rouge cette pomme verte » | V P R C | Desc Déic Ana | Synergistic | **Color** + Selection |
| `ColorizeCopyCommand` | « colorie la pomme comme la citrouille » | V P R C | Desc Déic Ana | Synergistic (2 réf.) | Selection(cible) + Selection(source) |
| `ColorizeDarkerCommand` | « assombris ça » | V P R C | Déic Desc Ana | Synergistic | Selection |
| `ColorizeLighterCommand` | « éclaircis les voitures bleues » | V P R C | Desc Déic Ana | Synergistic | Selection |
| `ResetColorCommand` | « remets la couleur d'origine » | V P R C | Desc Déic Ana | Synergistic | Selection |

#### D. Visibilité & apparence

| Commande | Exemple | Modalités | Réf. | CASE max | Params |
|----------|---------|-----------|------|----------|--------|
| `HideCommand` | « masque la voiture rouge » | V P R C | Desc Déic Ana | Synergistic | Selection |
| `ShowCommand` | « montre les citrouilles » | V P R C | Desc Déic Ana | Synergistic | Selection |
| `SetTransparentCommand` | « rends ça transparent » | V P R C | Déic Desc Ana | Synergistic | Selection |
| `SetOpaqueCommand` | « rends les pommes opaques » | V P R C | Desc Déic Ana | Synergistic | Selection |
| `HighlightCommand` | « surligne les légumes » | V P R C | Desc Déic Ana | Synergistic | Selection |
| `FocusCommand` | « focus sur la voiture » | V P R C | Desc Déic Ana | Synergistic | Selection |

#### E. Manipulation directe

| Commande | Exemple | Modalités | Réf. | CASE max | Params |
|----------|---------|-----------|------|----------|--------|
| `GrabCommand` | « attrape ça » | V P R C M | Déic Desc Ana | Synergistic | Selection (+ point de saisie) |
| `ReleaseCommand` | « lâche » / « pose » | V C M | Ana | Synergistic | Selection |

#### F. Cycle de vie

| Commande | Exemple | Modalités | Réf. | CASE max | Params |
|----------|---------|-----------|------|----------|--------|
| `DuplicateCommand` | « duplique ça ici » | V P R C | Déic Desc Ana | Synergistic | Selection + Point(dest, opt.) |
| `DeleteCommand` | « supprime la pomme rouge » | V P R C | Desc Déic Ana | Synergistic | Selection |

#### G. Réinitialisation

| Commande | Exemple | Modalités | Réf. | CASE max | Params |
|----------|---------|-----------|------|----------|--------|
| `ResetTransformCommand` | « remets ça en place » | V P R C | Déic Desc Ana | Synergistic | Selection |
| `ResetScaleCommand` | « taille normale pour les pommes » | V P R C | Desc Déic Ana | Synergistic | Selection |
| `ResetSceneCommand` | « réinitialise la scène » | V | — | Exclusive | — |

#### H. Requête / information *(sortie console ou voix)*

| Commande | Exemple | Modalités | Réf. | CASE max | Params |
|----------|---------|-----------|------|----------|--------|
| `CountCommand` | « combien de citrouilles ? » | V P R C | Desc Quant Ana | Synergistic | Selection |
| `MeasureCommand` | « mesure la distance entre ça et ça » | V P R | Déic | **Synergistic** (P3) | 2 Points |
| `DescribeCommand` | « c'est quoi ça ? » / « décris la pomme rouge » | V P R C | Déic Desc Ana | Synergistic | Selection |

#### I. Historique

| Commande | Exemple | Modalités | Réf. | CASE max | Params |
|----------|---------|-----------|------|----------|--------|
| `UndoCommand` | « annule » | V | — | Exclusive | — |
| `RedoCommand` | « rétablis » | V | — | Exclusive | — |

#### J. Système *(non déclenchable à la voix)*

| Commande | Rôle | Modalités | Params |
|----------|------|-----------|--------|
| `SpeechCommand` | pose une clarification (TTS) → pattern **P8** | V (sortie) | SentenceParameter |

---

## 5. Grille du « degré de multimodalité » d'un EV

Objectif : produire, pour tout EV, **(a)** un *profil* à 8 axes (radar) et **(b)** un *niveau de
maturité* synthétique (N0–N4). On note ce que l'EV **sait réellement traiter** (commandes ×
moteur de résolution), pas ce qu'il déclare.

### 5.1 Les 8 axes (chacun noté 0–3)

| Axe | 0 | 1 | 2 | 3 |
|-----|---|---|---|---|
| **MOD** — largeur modale (D1) | 1 modalité | 2 | 3 | ≥ 4 |
| **CASE** — fusion max (D5) | Exclusive | Alternate | Concurrent | Synergistic |
| **FUS** — niveau d'intégration (D6) | aucune fusion | lexical | syntaxique | sémantique |
| **TMP** — granularité temporelle (D7) | aucune | fenêtre globale | fenêtre par énoncé | alignement par horodatage de token |
| **REF** — richesse référentielle (D3) | 1 type | 2 types | 3 types | 4 types (Desc+Déic+Ana+Quant) |
| **CARE** — souplesse combinatoire (D4) | Assignment seul | + Equivalence | + Complementarity | + Redundancy |
| **OUT** — bidirectionnalité / fission | aucun retour | retour visuel | + retour vocal (TTS) | + dialogue de réparation/clarification |
| **PAR** — modulation paramétrique de l'action (§7) | aucune (incréments fixes) | grandeur discrète (« de 90° », « ×2 ») | + graduable (« un peu », « beaucoup ») | + relative à un référent (« deux fois plus que ça ») |

**Index agrégé** (optionnel) : `DMI = Σ(axes) / 24` ∈ [0, 1].

### 5.2 Niveaux de maturité (synthèse ordinale)

Déterminés en priorité par CASE + FUS + TMP (les axes qui « font » la fusion) :

| Niveau | Nom | Critère | CASE |
|--------|-----|---------|------|
| **N0** | Unimodal | une seule modalité | — |
| **N1** | Multimodal séquentiel | modalités disponibles mais non fusionnées | Exclusive / Alternate |
| **N2** | Multimodal concurrent | parallèle, traitement indépendant | Concurrent |
| **N3** | Multimodal synergique | fusion **sémantique** dans une fenêtre temporelle | Synergistic |
| **N4** | Synergique avancé | N3 **+** référence anaphorique inter-tour **+** désambiguïsation déixis/anaphore (P6) **+** fission de sortie (P8) | Synergistic |

---

## 6. Application : profil de SC4VE

| Axe | Score | Justification |
|-----|:-----:|---------------|
| **MOD** | **3** | Voix + pointage (raycast) + regard (caméra) + manipulation (grab) + mémoire du dialogue → ≥ 4 modalités. |
| **CASE** | **3** | « déplace ça ici » fusionne voix + pointage en un sens unique → Synergistic. |
| **FUS** | **3** | Fusion *sémantique* (late fusion) : chaque modalité produit des triplets, fusionnés dans le graphe RDF SVEN. |
| **TMP** | **3** | Alignement **fin** : le timestamp de chaque mot sélectionne l'objet pointé à cet instant (`StartedAt` de « ça » pour la source d'un déplacement, `EndedAt` pour la destination). |
| **REF** | **3** | Descriptive (annotation/couleur) + déictique (pointeur/regard) + anaphorique (coréférence) + quantifiée/ordinale (`limit`, « les 3 plus petites »). |
| **CARE** | **2** | Equivalence (plusieurs façons de désigner une cible) + Complementarity (voix+pointage). Redundancy non exploitée. |
| **OUT** | **3** | Retour visuel (contour de sélection) + vocal (TTS) + **dialogue de clarification** piloté par l'ontologie (« Sur quels objets ? »). |
| **PAR** | **0** | Transforms à incréments fixes (rotation 45°, facteur d'échelle constant) ; la quantité n'agit que sur la sélection (`limit`), jamais sur l'amplitude de l'action. |

**DMI = 20 / 24 ≈ 0.83** · **Niveau = N4** (synergique avancé : P6 désambiguïsation + P8 clarification présents).

> Lecture : SC4VE est un EV multimodal *mûr*. Les axes non saturés sont **CARE** (2/3 — pas de
> redondance/confirmation mutuelle : on ne combine pas « la pomme rouge » **et** un pointage
> simultané) et **PAR** (0/3 — transforms à incréments fixes). Ce sont les deux pistes d'extension
> les plus concrètes (cf. §7, manques #10 et #4) et des points de comparaison utiles entre EV.

---

## 7. Espace d'extension — commandes non couvertes

Cartographier ce qu'un EV *pourrait* faire situe sa couverture réelle. En projetant SC4VE sur les
**cinq tâches universelles de l'interaction 3D** (Bowman et al.), deux tâches entières manquent et
plusieurs **modes de référence** restent inexploités.

### 7.1 Couverture des tâches universelles (Bowman et al.)

| Tâche universelle | Couverture SC4VE |
|-------------------|------------------|
| Sélection | ✓✓ riche (Desc, Déic, Ana, Quant) |
| Manipulation | ✓ position/rotation/échelle — **incréments fixes**, pas de composite |
| Contrôle système | ✓ undo/redo/reset/select-all |
| **Navigation / déplacement du point de vue** | ✗ quasi absent (seul `FocusCommand` oriente la caméra) |
| **Entrée symbolique / création** | ✗ absent (`DuplicateCommand` copie l'existant, n'instancie pas) |

### 7.2 Catalogue des manques

Chaque manque indique l'**axe de la grille (§5)** qu'il ferait progresser et son ancrage.

| # | Manque | Énoncé exemple | Axe impacté | Ancrage |
|---|--------|----------------|-------------|---------|
| 1 | Navigation / téléportation déictique | « emmène-moi **ici** », « tourne autour de **ça** », « zoome », « vue de dessus » | MOD (cible : point de vue égocentrique) | Bowman |
| 2 | Référence relationnelle / topologique | « la pomme **à côté de** la voiture », « celle **entre** les deux », « mets-le **derrière** la citrouille » | REF (5ᵉ type → relève le plafond) | Levinson |
| 3 | Cadre spatial relatif / égocentrique | « pousse-le **un peu à gauche** », « **vers moi** », « tourne-le **face à la caméra** » | REF (cadre de référence) | Levinson, Bowman |
| 4 | Modulation paramétrique vocale | « tourne-le **de 90°** », « **deux fois** plus grand », « un **tout petit peu** » | **PAR (nouvel axe, §5.1)** | Cohen et al. (QuickSet) |
| 5 | Désignation par région (geste continu) | « tout **dans cette zone** » ⟨lasso⟩, « **d'ici à là** » | MOD/REF (geste surfacique) | Bowman |
| 6 | Geste iconique / mimétique | « tourne-le **comme ça** » ⟨geste⟩, « grand **comme ça** » ⟨écart des mains⟩ | MOD (geste dépictif ≠ déictique) | McNeill |
| 7 | Création / authoring & étiquetage | « **crée un cube ici** », « ajoute une pomme là », « **appelle ça** une chaise » | CASE/FUS (instanciation synergique, écriture du graphe) | Cohen et al., Bolt |
| 8 | Requêtes spatiales / déixis inverse | « **où est** la pomme rouge ? » (le système la désigne), « lequel est le plus grand, **ça** ou **ça** ? » | OUT (le système désigne en retour) | Dumas et al. (fission) |
| 9 | Groupement & agencement | « **groupe** ceux-là », « aligne-les **en cercle** », « **empile**-les », « mets ça **sur** ça » | — (manipulation composite, niveau commande) | Bowman |
| 10 | Désignation redondante (robustesse) | « la pomme rouge » ⟨**+ pointe la même**⟩ | **CARE** (Redundancy → 3/3) | Oviatt ; Kaiser et al. |

### 7.3 Patterns d'extension (prolongent §4.1)

| Pattern | Énoncé type | Modalités | Référence | CARE | CASE | Fusion | Manque |
|---------|-------------|-----------|-----------|------|------|--------|--------|
| **P9** Navigation déictique | « emmène-moi ici » | V + P/R | Déic | Complementarity | Synergistic | sémantique | 1 |
| **P10** Référence relationnelle | « celle entre les deux » | V (+P) | Relationnelle | Complementarity / Assignment | Exclusive→Synergistic | sémantique | 2, 3 |
| **P11** Modulation paramétrique | « tourne-le de 90° » | V | — | Assignment | Exclusive | sémantique | 4 |
| **P12** Désignation surfacique | « tout dans cette zone » ⟨lasso⟩ | V + geste | Déic (région) | Complementarity | Synergistic | sémantique | 5 |
| **P13** Geste iconique | « comme ça » ⟨geste⟩ | V + geste | dépictive | Complementarity | Synergistic | sémantique | 6 |
| **P14** Création / authoring | « crée un cube ici » | V + P | Déic + contenu | Complementarity | Synergistic | sémantique | 7 |
| **P15** Déixis inverse (sortie) | « où est X ? » → le système pointe | V → sortie | — | — | — | — (fission) | 8 |
| **P16** Désignation redondante | « la pomme rouge » + pointage | V + P | Desc ⊕ Déic | **Redundancy** | Synergistic | sémantique | 10 |

### 7.4 Les trois manques les plus structurants

1. **Navigation (P9)** — une des cinq tâches universelles, absente. « Emmène-moi ici » est le
   pendant *navigation* de « déplace ça ici » (même fusion voix + déixis, appliquée au point de
   vue). Ajoute une cible modale entière.
2. **Création / authoring (P14)** — l'archétype de QuickSet (« crée un X ici »), avec un gain
   mesuré ×9 sur le temps de création vs GUI. SC4VE copie et supprime, mais n'instancie ni ne nomme.
3. **Référence relationnelle (P10)** — « à côté de / entre / derrière » : le mode de référence
   manquant face aux quatre gérés, et le plus exigeant en raisonnement spatial qualitatif.

> **Impact sur la grille (§5).** Ces manques ne sont pas tous capturés par les 7 axes initiaux :
> le #4 (magnitude) a justifié l'ajout de l'axe **PAR** ; le #2 étend le plafond de **REF** (5ᵉ
> type) ; le #1 ajoute une cible à **MOD** ; le #8 enrichit **OUT** ; le #10 sature **CARE**.
> L'analyse des manques sert donc aussi de test de robustesse au rubric lui-même.

---

## 8. Protocole d'extension à un nouvel EV

1. **Inventorier les commandes** → remplir un catalogue au format §4.3 (une ligne par commande :
   modalités, type de référence, CASE max, paramètres requis).
2. **Mapper chaque commande aux patterns** P0–P16 (§4.1, §7.3).
3. **Noter les 8 axes** (§5.1) d'après ce que le moteur traite *réellement* (tester un énoncé par
   pattern suffit à prouver la capacité).
4. **Calculer** le profil radar, le DMI et le niveau N0–N4.
5. **Comparer** les EV sur le profil à 8 axes (plus informatif que le seul scalaire DMI).

Un EV = un fichier `profil-<nom>.md` réutilisant ces sections. La taxonomie (§3–4) et la grille
(§5) restent inchangées : c'est ce qui rend les EV comparables.

---

## 9. Limites & travaux futurs

- **Validité de la grille** : les pondérations (axes égaux) sont un choix par défaut ; à valider
  empiriquement (corrélation avec l'efficacité/satisfaction utilisateur).
- **Évaluation empirique différée** : pour mesurer la *performance* (et non la seule *capacité*) du
  moteur, prévoir un jeu de cas de test `entrée (texte + horodatages + trajectoire pointeur/regard +
  historique) → sortie attendue (JSON de commande)`, rejouable et annoté par pattern. Métriques :
  exactitude du type de commande, F1 des paramètres, ancrage référentiel, précision/rappel des
  clarifications. *(Hors périmètre de cette première version, centrée sur la capacité.)*
- **Modalités non couvertes** : audio non verbal, haptique, expressions faciales (Bernsen) — à
  ajouter à l'axe MOD si pertinents.
- **CARE Redundancy & confirmation mutuelle** : non modélisée comme dimension de robustesse.

---

## 10. Références

- Bolt, R. A. (1980). *Put-that-there: Voice and gesture at the graphics interface.* ACM SIGGRAPH Computer Graphics, 14(3), 262–270. <https://doi.org/10.1145/965105.807503>
- Nigay, L., & Coutaz, J. (1993). *A design space for multimodal systems: concurrent processing and data fusion.* Proc. INTERCHI '93, 172–178. <https://doi.org/10.1145/169059.169143>
- Coutaz, J., Nigay, L., Salber, D., Blandford, A., May, J., & Young, R. M. (1995). *Four easy pieces for assessing the usability of multimodal interaction: the CARE properties.* Proc. INTERACT '95, 115–120. <http://iihm.imag.fr/publs/1995/Interact95_CARE.pdf>
- Martin, J.-C. (1998). *TYCOON: Theoretical framework and software tools for multimodal interfaces.* In J. Lee (Ed.), Intelligence and Multimodality in Multimedia Interfaces. AAAI Press.
- Oviatt, S. (1999). *Ten myths of multimodal interaction.* Communications of the ACM, 42(11), 74–81. <https://doi.org/10.1145/319382.319398>
- Bernsen, N. O. (2008). *Multimodality theory.* In D. Tzovaras (Ed.), Multimodal User Interfaces: From Signals to Interaction, 5–29. Springer. <https://doi.org/10.1007/978-3-540-78345-9_2>
- Reeves, L. M., Lai, J., Larson, J. A., Oviatt, S., Balaji, T. S., Buisine, S., et al. (2004). *Guidelines for multimodal user interface design.* Communications of the ACM, 47(1), 57–59. <https://doi.org/10.1145/962081.962106>
- Dumas, B., Lalanne, D., & Oviatt, S. (2009). *Multimodal interfaces: A survey of principles, models and frameworks.* In Human Machine Interaction, LNCS 5440, 3–26. Springer. <https://doi.org/10.1007/978-3-642-00437-7_1>
- Lalanne, D., Nigay, L., Palanque, P., Robinson, P., Vanderdonckt, J., & Ladry, J.-F. (2009). *Fusion engines for multimodal input: a survey.* Proc. ICMI-MLMI '09, 153–160. <https://doi.org/10.1145/1647314.1647343> · PDF libre : <http://iihm.imag.fr/publs/2009/FinalSurvey.pdf>

### Espace d'extension (§7)

- Bowman, D. A., Kruijff, E., LaViola, J. J., & Poupyrev, I. (2004). *3D User Interfaces: Theory and Practice.* Addison-Wesley. ISBN 978-0201758672. (2ᵉ éd. 2017 : LaViola, Kruijff, McMahan, Bowman, Poupyrev.)
- Cohen, P. R., Johnston, M., McGee, D., Oviatt, S., Pittman, J., Smith, I., Chen, L., & Clow, J. (1997). *QuickSet: Multimodal interaction for distributed applications.* Proc. 5th ACM Int. Conf. on Multimedia, 31–40. <https://doi.org/10.1145/266180.266328>
- Kaiser, E., Olwal, A., McGee, D., Benko, H., Corradini, A., Li, X., Cohen, P., & Feiner, S. (2003). *Mutual disambiguation of 3D multimodal interaction in augmented and virtual reality.* Proc. ICMI '03, 12–19. <https://doi.org/10.1145/958432.958438>
- McNeill, D. (1992). *Hand and Mind: What Gestures Reveal about Thought.* University of Chicago Press.
- Levinson, S. C. (2003). *Space in Language and Cognition: Explorations in Cognitive Diversity.* Cambridge University Press. ISBN 978-0521011969. <https://www.cambridge.org/core/books/space-in-language-and-cognition/D07AD2885A025E00B1C94ED722071D80>
