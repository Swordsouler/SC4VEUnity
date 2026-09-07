# Démonstration — Mini-jeu de service en VR

Document de chantier. Décrit **quoi** construire et **dans quel ordre**, pas encore **comment**
ligne à ligne. À lire avec `../../COMMANDS.md` (ajouter une commande) et `../../README.md`
(architecture).

---

## Organisation du dossier

`Assets/Demonstration/` est le répertoire de travail de la démonstration. Y vivent :

| | |
|---|---|
| `Editor/` | outils d'éditeur — **`DemoSceneBuilder.cs` construit la scène par code** |
| `Scenes/` | les scènes du mini-jeu, produites par l'outil ci-dessus |
| `Prefabs/` | ingrédients, assiettes, stations, tables, serveurs, clients |
| `Ontologies/` | brouillon de `sven-restaurant.ttl` avant copie dans `StreamingAssets` |
| `Scripts/` | code **spécifique au jeu** : machine à états des serveurs, spawn des clients, score, tableau des commandes |
| `Materials/`, `Meshes/` | les 8 nouveaux assets du §6.1 |

Les dossiers sont à créer au fur et à mesure — un dossier vide n'est pas suivi par Git et
Unity y génère des `.meta` pour rien.

### La scène se construit par code

Menu **SC4VE > Démonstration** :

| | |
|---|---|
| **1 — (Re)construire le contenu du mini-jeu** | remplit `Scenes/Demo Mini Game.unity` : sol, cuisine (plan de travail, étagère, 11 ingrédients ×2, 2 stations, 6 assiettes, poubelle, passe), salle (4 tables non numérotées, 2 serveurs identiques), et le rig XR s'il manque |
| **2 — Corriger les prefabs existants** | passe le `SemanticAnnotator` des prefabs de fruits en `Dynamic` et leur ajoute un `XRGrabInteractable`. Idempotent. L'outil 1 l'exécute d'abord. |
| **3 — Générer les meshes des ingrédients** | fabrique les meshes low-poly des 8 ingrédients sans modèle, dans `Meshes/`. L'outil 1 l'exécute aussi. |
| **4 — Peupler la scène d'exposition** | aligne un exemplaire de chaque objet manipulable dans `Scenes/Demo Exposition.unity`, à sa taille réelle et étiqueté |

**La scène d'exposition est une planche de contact**, pas une scène de jeu : elle sert à
repérer d'un coup d'œil un modèle raté, une taille incohérente ou une pièce décollée. Les
objets y sont les **mêmes prefabs** que dans le jeu — corriger un prefab depuis l'exposition
corrige le jeu. Inutile d'y entrer en mode Play : sans `GraphController`, la sémantisation ne
ferait qu'y produire des erreurs, et il n'y a rien à y exécuter.

**Construire par code plutôt qu'à la main** est un choix, pas un pis-aller : le contenu est
relisible en diff, reproductible, et se reconstruit quand le vocabulaire change.

**L'outil ne possède qu'un seul objet : la racine `Mini-jeu (généré)`.** Il la détruit et la
reconstruit à chaque exécution, et ne touche à rien d'autre dans la scène. Tout ce qui vit en
dehors — rig XR, `MultimodalityController`, pipeline vocal, éclairage, réglages de NavMesh —
survit aux reconstructions et peut être réglé à la main librement. **La seule règle : ne rien
placer soi-même sous cette racine.**

Le rig XR suit la même logique : créé s'il manque, laissé intact s'il existe, et toujours hors
de la racine générée.

Ce que l'outil ne fait pas, et qui reste manuel : cuire le NavMesh, poser le
`MultimodalityController` et le pipeline vocal, remplacer les 8 primitives colorées par de
vrais modèles (§6.1).

### Ce qui ne doit PAS venir ici

**Les nouvelles commandes restent dans `Assets/Scripts/SC4VE/Intent/Command/`.** Ce n'est pas
une préférence de rangement, c'est une contrainte technique vérifiée dans le code :

| Mécanisme | Portée de recherche |
|---|---|
| `RuleBasedTriggersAttribute.GetAllMappings()` | `AppDomain.CurrentDomain.GetAssemblies()` — **toutes** les assemblies |
| `CommandDescriptionAttribute` (liste LLM, `CreateCommandInstance`) | `Assembly.GetAssembly(typeof(Command))` — **la seule** assembly SC4VE |

Une commande placée dans `Assets/Demonstration/Scripts/` compilerait dans `Assembly-CSharp` et
serait donc **reconnue en mode RuleBased mais invisible au LLM**, et impossible à instancier à
la désérialisation (`CommandConverter` passe par `CreateCommandInstance`). Le symptôme serait
déroutant : la commande marche dans un mode et échoue en silence dans l'autre.

Deux issues si l'on tient un jour à déplacer les commandes : donner une asmdef au dossier
`Demonstration` **et** élargir `CommandDescriptionAttribute` à toutes les assemblies. Tant que
ce n'est pas fait, les commandes restent où elles sont.

Le reste du code de jeu n'a aucune contrainte : `SC4VE.asmdef` et `com.nsaintl.sven.asmdef`
sont tous deux `autoReferenced`, donc un `MonoBehaviour` placé dans `Demonstration/Scripts/`
accède librement aux types SC4VE et SVEN sans asmdef ni configuration.

### Fichiers déjà écrits pour ce chantier, hors de ce dossier

| Fichier | Rôle |
|---|---|
| `Assets/Scripts/SC4VE/ListeningTimeScale.cs` | ralenti pendant la parole (§2) |
| `Assets/Scripts/SC4VE/Intent/Command/XRGrabSupport.cs` | accès XRI pour `GrabCommand` / `ReleaseCommand` |
| `Assets/Scripts/SC4VE/Voice/VoiceProcessor.cs` | ajout de l'événement `OnSpeechStart` |

Aucun de ces fichiers n'a été compilé ni testé.

---

## Sommaire

0. [Organisation du dossier](#organisation-du-dossier)
1. [Intention](#1-intention)
2. [Décisions actées](#2-décisions-actées)
3. [Principe directeur : aucune modalité ne gagne toujours](#3-principe-directeur--aucune-modalité-ne-gagne-toujours)
4. [Boucle de jeu](#4-boucle-de-jeu)
5. [Anatomie de la scène](#5-anatomie-de-la-scène)
6. [Modèle sémantique](#6-modèle-sémantique)
7. [Nouvelles commandes](#7-nouvelles-commandes)
8. [Le registre de délégation](#8-le-registre-de-délégation)
9. [Lots de chantier](#9-lots-de-chantier)
10. [Instrumentation](#10-instrumentation)
11. [Hors périmètre](#11-hors-périmètre)
12. [Risques identifiés](#12-risques-identifiés)
13. [Questions ouvertes](#13-questions-ouvertes)

---

## 1. Intention

Remplacer le bac à sable actuel (`New Demo`, manipulation d'objets sans but) par une **tâche
avec un début, une fin et un enjeu**, jouable en VR, où l'utilisateur est le cuisinier d'un
petit restaurant et pilote des serveurs autonomes.

Ce que ça apporte par rapport à la démo actuelle :

- un **second registre de commande** : la délégation à un agent, à côté de la manipulation
  directe. La cible d'une commande n'est plus seulement un objet inerte mais un acteur avec
  un état de tâche ;
- une **raison d'être au TTS** : aujourd'hui Piper ne sert qu'à la clarification. Ici les
  clients commandent à voix haute et les serveurs répondent ;
- la **clarification devient un trait de personnage**. « Laquelle ? » n'est plus une limite
  technique exposée au public, c'est le serveur qui ne comprend pas ;
- une **contrainte sémantique authentique** : « je suis allergique aux fruits à coque »,
  « sans banane », « quelque chose de végétarien ». C'est le seul endroit où l'inférence
  taxonomique RDF fait quelque chose qu'une interface graphique ne saurait pas faire.

### Destination

Le jeu est un **livrable de présentation** : démonstrations en conférence et soutenance de
thèse. Ça n'est pas un détail de communication, ça contraint la conception :

- **une session dure quelques minutes** et se réinitialise en un geste — on enchaîne les
  visiteurs sur un stand ;
- **la scène doit être compréhensible sans explication préalable** ; l'opérateur ne doit pas
  avoir à commenter pendant que quelqu'un joue ;
- **l'environnement sonore d'une conférence est mauvais.** C'est la contrainte la plus
  sous-estimée : le VAD déclenchera sur le bruit ambiant. Prévoir le **push-to-talk** comme
  mode par défaut en salon (`WhisperSpeechToText.PushToTalk` existe déjà), le VAD étant
  réservé aux conditions calmes ;
- **rien ne doit dépendre du réseau** le jour J — donc mode LLM local ou RuleBased, jamais
  l'API OpenAI en démonstration publique.

---

## 2. Décisions actées

| Décision | Statut |
|---|---|
| Plateforme **VR** (OpenXR + XR Interaction Toolkit) | acté |
| Les agents (serveurs) sont des `SemantizationCore` ordinaires, avec un état de tâche | acté |
| **Aucune action réflexe** : toute commande est un plan, jamais une réaction | acté |
| **La pression vient du nombre, pas de la vitesse** : timers clients 45–90 s, montée en difficulté par le nombre de tables | acté |
| **Le trajet des serveurs absorbe la latence** du pipeline STT+LLM (~1–3 s) | acté |
| **Ralenti à 30 % du temps de jeu pendant que le joueur parle** | acté, **implémenté** — réglable dans l'Inspector, `1` désactive l'effet |
| **Un plat est un contenant rempli**, pas un objet créé de toutes pièces | acté |
| **Les serveurs portent réellement les plats**, un seul à la fois | acté |
| **La commande client se prend** avec `TakeOrderCommand` ; le client ne parle pas spontanément | acté |
| **Le joueur ne se déplace pas** : pas de téléportation, pas de locomotion au joystick. Il peut marcher physiquement pour mieux voir | acté |
| **Le mode de reconnaissance est choisi par le joueur au lancement** (RuleBased / LLM) | acté |

### Le ralenti pendant la parole — implémenté

`ListeningTimeScale` (`Assets/Scripts/SC4VE/ListeningTimeScale.cs`) ralentit `Time.timeScale`
pendant que le joueur parle, et le remet à 1 quand il se tait. Deux champs d'Inspector :
`ListeningTimeScale` (0,3 par défaut ; **mettre 1 désactive l'effet**) et `TransitionDuration`
(0,15 s ; la transition douce est ce qui fait lire l'effet comme une pause tactique et non
comme un ralentissement subi).

**Le ralenti ralentit aussi la patience des clients** — la jauge tourne en `Time.deltaTime`,
pas en temps réel. Cela ouvre un exploit : parler pour ne rien dire fait gagner du temps.
L'exploit est réel et assumé, parce que l'alternative est pire. Faire courir la patience en
temps réel reviendrait à **pénaliser le joueur pour avoir utilisé la modalité que la démo
existe pour montrer** : celui qui triche en marmonnant gagne quelques secondes, celui qui est
puni d'avoir parlé apprend à ne plus parler. Si le déséquilibre gêne un jour, la correction
propre est de plafonner le ralenti cumulé par client, pas de le retirer.

**Ce que l'écriture a révélé** : `VoiceProcessor` n'avait **aucun événement de début de
parole**. `OnRecordingStart` signale le démarrage de la *capture micro* — donc, en mode VAD
avec `AutoStart`, une seule fois au lancement. S'en servir aurait ralenti la partie en
permanence. Un événement `OnSpeechStart` a donc été ajouté, symétrique du `OnRecordingStop`
existant, déclenché sur la transition `_didDetect` faux → vrai : franchissement du seuil de
volume en VAD, première trame après l'appui en push-to-talk. Une seule ligne, mais sans elle
le mécanisme ne pouvait pas exister.

`Time.fixedDeltaTime` suit `Time.timeScale`, sans quoi la physique saccade au ralenti. Le
suivi de tête n'est **pas** affecté (piloté par le runtime XR), donc pas de risque de
cinétose — c'est ce qui rend le procédé acceptable en VR.

### Le choix du mode, écran de départ

`MultimodalityController.Mode` est déjà une énumération d'Inspector (`LLM` / `RuleBased`) : la
rendre choisissable au lancement ne coûte qu'un écran de départ qui écrit ce champ avant le
démarrage de la partie. Formulation à l'écran, sans jargon :

| | Annoncé au joueur |
|---|---|
| **RuleBased** | *Rapide* — réponse immédiate, mais comprend moins bien les phrases inhabituelles |
| **LLM** | *Plus lent* — une à trois secondes de réflexion, mais comprend beaucoup mieux |

Ce choix a trois effets, dans l'ordre d'importance :

1. **Il transforme la limite du mode RuleBased en contenu.** Le risque n° 1 du §12 (deux
   déictiques dans un même énoncé) cesse d'être un défaut à cacher : c'est le compromis
   que le joueur a accepté en choisissant le mode rapide. Le mode RuleBased couvre le
   sous-ensemble à un seul déictique, et « toi 👆 va servir cette table-là 👆 » devient une
   raison de passer en mode LLM.
2. **Il fait du jeu un instrument de comparaison.** Deux populations de joueurs, deux modes,
   les mêmes métriques du §10 — la comparaison latence/qualité d'interprétation se collecte
   toute seule. À condition de journaliser le mode choisi avec chaque énoncé.
3. **Il rend la démonstration robuste.** Si le serveur LLM local ne répond pas le jour de la
   soutenance, le mode RuleBased tourne sans réseau ni GPU.

Une réserve à assumer : les deux modes ne couvriront pas exactement le même ensemble
d'énoncés. Il faut donc que **l'écran de départ le dise** (« comprend moins bien ») plutôt que
de laisser le joueur croire à un simple réglage de vitesse, et que l'échec de compréhension en
RuleBased produise une clarification parlée, jamais un silence.

Conséquence majeure du point « agents = `SemantizationCore` » : **aucun nouveau type de
paramètre n'est nécessaire pour désigner un serveur.** Le `SelectionParameter` existant sait
déjà filtrer par annotation, résoudre un déictique et gérer la coréférence — il désignera un
serveur exactement comme il désigne une pomme. C'est ce qui rend ce chantier raisonnable.

---

## 3. Principe directeur : aucune modalité ne gagne toujours

Le piège serait une scène où le pointage suffit (ce serait du drag & drop) ou où la parole
suffit (ce serait un jeu textuel). La scène doit alterner délibérément entre deux familles
de situations.

**Où la parole écrase le geste** — l'ensemble, la classe, la contrainte :

- « mets tous les fruits rouges dans le saladier » → 6 objets en un énoncé
- « tout sauf les bananes » → négation, impossible à pointer
- « prépare une salade de fruits » → abstraction nommée, aucun référent visible
- « trois carottes » → quantité

**Où le geste écrase la parole** — l'individu, le lieu, l'inconnu :

- « pose-le **ici** 👆 » → un emplacement vide n'a pas de nom
- deux pommes strictement identiques → seul le pointage tranche
- « **cette** table-**là** 👆 » → **les tables ne sont pas numérotées** (décision de design
  volontaire : numéroter les tables tuerait la deixis)

**Les énoncés hybrides** sont le cœur de la démonstration — ils exigent les deux modalités
dans le même énoncé :

> « Toi 👆, va servir cette table-là 👆. »
> « Prends les deux pommes rouges et mets-les ici 👆. »
> « Apporte ça 👆 à ce client-là 👆. »

**Critère d'acceptation de la scène** : si un énoncé hybride ne peut pas être exécuté, la
scène a échoué, quel que soit le reste.

Corollaire à garder en tête pour peupler la scène : il faut **délibérément** deux serveurs
indiscernables et deux ingrédients identiques, sinon la clarification ne se déclenchera
jamais et le meilleur moment de la démo n'aura pas lieu.

---

## 4. Boucle de jeu

La boucle a **deux moments de délégation**, un au début et un à la fin :

```
1. un client s'installe                                    (le joueur voit, ne sait rien)
2. « toi 👆 va prendre la commande de cette table-là 👆 »   → TakeOrderCommand
3. le serveur s'y rend, le client énonce sa commande        (TTS — le joueur écoute)
4. la commande s'inscrit au tableau                         (le joueur peut réécouter en la lisant)
5. le joueur prépare : ingrédients → stations → contenant   (manipulation directe)
6. le contenant rempli est posé à la passe
7. « va servir cette table-là 👆 »                          → ServeCommand
8. le serveur porte le plat jusqu'à la table
9. le client accepte ou refuse selon la conformité
```

```
        SALLE                      PASSE                    CUISINE
   tables + clients   ←───  contenants remplis   ←───  plan de travail + stations
        ▲                                                      ▲
        │  le serveur va chercher la commande, puis sert       │
        └──────────────────────────────────────────────────────┘

   Serveurs : SALLE ⇄ PASSE, un plat à la fois    Joueur : reste en CUISINE
```

- **Le joueur** est en cuisine. Il ne se déplace pas, ne porte rien vers la salle, n'a aucune
  action urgente. Manipulation directe des ingrédients et des contenants.
- **Les serveurs** sont en salle. Ils exécutent un ordre puis reviennent en attente.
  Délégation.
- **Les clients** s'installent et attendent qu'on vienne les voir. Ils ne parlent **qu'une
  fois** qu'un serveur est arrivé à leur table.

Le joueur n'incarne pas un cuisinier qui court : il **orchestre**. C'est ce qui rend le jeu
compatible avec la latence du pipeline vocal.

### Pourquoi prendre la commande change la boucle

`TakeOrderCommand` n'est pas une étape de plus pour le plaisir : elle apporte trois choses.

1. **Le joueur a quelque chose à faire dès la première seconde.** Sans elle, le début de
   partie est une attente passive pendant que les clients parlent.
2. **Le joueur choisit le moment où il doit écouter.** C'est déterminant sur un stand bruyant
   (§1) : avec une commande spontanée, le client parle quand il veut, souvent dans le
   brouhaha. Ici, le joueur déclenche la prise de commande et se tient prêt.
3. **Elle double le temps masqué.** Deux allers-retours de serveur par client au lieu d'un,
   donc deux fenêtres pendant lesquelles la latence du pipeline ne coûte rien.

**Une seule jauge de patience**, démarrée à l'installation du client — oublier de prendre une
commande se paie donc, exactement comme oublier de servir.

---

## 5. Anatomie de la scène

### Cuisine (zone de travail du joueur — tout atteignable sans marcher, aire ~2 × 2 m)

| Élément | Rôle |
|---|---|
| Cagettes d'ingrédients | réserve en vrac, les 11 ingrédients du §6.1 |
| Plan de travail | surface libre — c'est là que « ici 👆 » a du sens |
| Stations | planche à découper et plaque de cuisson (§6.2) |
| Assiettes | **6, toutes identiques** — un seul type de contenant, quel que soit le plat |
| Poubelle | contenant comme un autre : c'est là qu'on vide un plat raté ou refusé |
| La passe | étagère de dépôt ; un plat posé là devient récupérable par un serveur |

#### L'économie des contenants

**Un seul type de contenant : l'assiette.** Pas de bol, pas de barquette. Six assiettes
interchangeables, pour quatre tables au maximum — quatre en circulation, deux d'avance.

Cette simplification supprime une règle entière : il n'y a plus besoin de contraindre le
générateur de commandes à ne pas émettre trop de plats de la même famille, puisqu'aucun plat
ne réclame un contenant particulier. Une règle en moins à écrire, un mode d'échec en moins à
diagnostiquer.

La seule variable qui reste est la **durée d'immobilisation** : une assiette sort de
circulation dès le premier ingrédient et n'y revient qu'une fois vidée. Un plat refusé
l'immobilise bien plus longtemps (aller, retour, vidage). Si le playtest du lot 2 montre des
blocages, c'est cette durée qu'il faut regarder avant d'ajouter une septième assiette.

Compromis assumé : une soupe servie dans une assiette n'est pas vraisemblable. La lisibilité
du jeu et la simplicité du modèle passent avant.

**La poubelle est un `Container` comme les autres**, ce qui évite encore une commande : vider,
c'est « mets tout ça dans la poubelle » — `PutInCommand`, comme remplir une assiette et comme
poser sur une station. Trois mécaniques différentes, une seule commande.

### Salle

| Élément | Rôle |
|---|---|
| 2 à 4 tables **non numérotées** | désignables uniquement par pointage |
| Clients | un par table ; commande + jauge de patience |
| 1 à 2 serveurs | agents autonomes ; au moins deux visuellement identiques au niveau 4 |

### Affichage

Un panneau de bons de commande visible depuis la cuisine, listant ce que chaque table attend.
C'est ce qui rend l'objectif lisible pour un spectateur en 3 secondes. Non interactif.

---

## 6. Modèle sémantique

Deux ontologies distinctes, à ne pas mélanger :

- `sven-*.ttl` — le **domaine** (ce qu'il y a dans le monde). Alimente le filtrage des
  sélections et le vocabulaire de reconnaissance.
- `sc4ve.ttl` — les **commandes** (leurs paramètres requis et leurs messages de
  clarification). Source de vérité lue par `ClarificationVocabulary`.

### 6.1 Les ingrédients

Existant, réutilisable tel quel (`Assets/Samples/SVEN/1.0.0/.../Fruits/Resources/`) —
✅ = classe C# **et** mesh déjà présents, ⬜ = à créer :

```
Food ─┬─ Fruit ─────┬─ Apple    ✅   pomme
      │             └─ Banana   ✅   banane
      ├─ Vegetable ─┬─ Carrot   ✅   carotte
      │             ├─ Pumpkin  ✅   citrouille
      │             ├─ Potato   ⬜   pomme de terre
      │             ├─ Lettuce  ⬜   laitue
      │             └─ Tomato   ⬜   tomate
      ├─ Meat ──────┬─ Beef     ⬜   bœuf
      │             └─ Chicken  ⬜   poulet
      ├─ Fish ────────ᐧ Salmon  ⬜   saumon
      ├─ Dairy ───────ᐧ Cheese  ⬜   fromage
      └─ Bakery ──────ᐧ Bread   ⬜   pain
```

**Chaque branche gagne son existence par une contrainte alimentaire** (§6.5) : `Meat` et
`Fish` portent le végétarisme, `Dairy` le sans-lactose, `Bakery` le sans-gluten. Ne pas
ajouter de branche qui ne serve à aucune contrainte et à aucune recette.

**`Lettuce` se dit « laitue », pas « salade ».** Le label français doit éviter la collision
avec le nom de plat « salade » (« mets la salade dans l'assiette » serait ambigu entre
l'ingrédient et le plat). Second label `@fr` « salade verte » acceptable, « salade » seul :
non.

### 6.2 Les états d'aliment

Un ingrédient n'est pas seulement d'un type, il est dans un **état** : entier ou coupé, cru ou
cuit. Un steak coupé et un steak cuit sont des sous-classes de steak.

#### Ne pas écrire le produit croisé à la main

La tentation serait de déclarer `SlicedBeef` et `CookedBeef` comme sous-classes **asserties**
de `Beef`. C'est intenable : deux états binaires font quatre classes par ingrédient, onze
ingrédients font quarante-quatre classes à écrire et à maintenir, et ajouter un troisième état
double encore le tout.

La solution tient dans le mécanisme d'annotation existant : **un objet porte plusieurs
annotations** (`SemanticAnnotator.Annotations` est une liste). L'état est donc **orthogonal**
au type : un steak cuit porte `sven:Beef` **et** `sven:Cooked`.

> **Corrigé à l'implémentation.** Ce paragraphe prévoyait initialement des classes qualifiées
> *définies* (`sven:CookedBeef ≡ ∃component.BeefComponent ⊓ ∃component.CookedComponent`), en
> comptant sur l'inférence pour en déduire `CookedBeef ⊑ Beef`. La lecture du code a montré
> que **c'est inutile**, pour deux raisons vérifiées :
>
> 1. le filtre de sélection ne fait **aucune inférence** — il compare un label :
>    `?annotation sven:value ?componentType . ?componentType rdfs:label "pomme"@fr` ;
> 2. `SemanticAnnotatorEditor` **matérialise les parents** au moment de l'annotation : cocher
>    « Apple » écrit `sven:Apple`, `sven:Fruit`, `sven:Food` dans la liste. La hiérarchie est
>    résolue à l'annotation, pas à la requête.
>
> Conséquence : **aucune classe croisée n'est nécessaire**, et il n'y en a aucune dans
> `sven-restaurant.ttl` — un avertissement en tête de fichier demande de ne pas en ajouter.
> « Le bœuf cuit » est la **conjonction de deux filtres d'annotation**, ce que `FilterElement`
> sait déjà faire, et « pas de viande » rejette le steak cuit comme le cru puisque l'objet
> porte de toute façon `sven:Meat` parmi ses annotations matérialisées.

Les exigences des recettes n'ont donc pas besoin de classes qualifiées non plus : chacune est
un nœud portant l'ingrédient et ses états.

```turtle
sven:SteakFrites sven:requires [ sven:ingredient sven:Beef ; sven:state sven:Cooked ] ,
                               [ sven:ingredient sven:Potato ; sven:state sven:Sliced , sven:Cooked ] .
```

`sven:forbidsState` exprime l'exigence inverse — le saumon cru du sandwich au saumon, les
légumes non cuits du sandwich crudités.

#### Les états retenus

```
State ─┬─ Sliced   coupé     ← planche à découper
       └─ Cooked   cuit      ← plaque de cuisson
```

Deux états seulement, tous deux **positifs** : leur absence signifie « entier » et « cru ». Pas
de classe `Raw` ni `Whole` — une recette qui exige du cru s'exprime par une exclusion
(`FILTER NOT EXISTS`), exactement comme une contrainte alimentaire.

**Les états ne font que s'ajouter, jamais se retirer.** On ne « décuit » pas un steak. Cette
monotonie n'est pas un détail de confort : elle évite d'avoir à retirer une annotation en
cours de partie, opération dont le support à l'exécution n'est pas acquis. Le seul retour en
arrière possible est de jeter l'aliment et d'en reprendre un.

#### La condition technique à respecter

Le `SemanticAnnotator` des aliments **doit être en `SemanticProcessingMode.Dynamic`**, pas
`Static`. `SemantizationCore` surveille les propriétés dans une coroutine
(`LoopCheckForChanges` → `Property.CheckForChanges()`), mais ignore explicitement les
composants marqués `Static` une fois sémantisés :

```csharp
if (componentProperties.Value.IsSemantized &&
    componentProperties.Value.ProcessingMode == SemanticProcessingMode.Static) continue;
```

En `Static`, l'annotation est figée au démarrage et « cuire » n'atteindrait jamais le graphe —
le plat serait jugé non conforme sans raison visible.

> **Vérifié : les prefabs existants sont en `Static`.** Sur `Interactable Apple.prefab`,
> l'entrée `SemanticAnnotator` de `componentsToSemanticize` porte `ProcessingMode: 1`, et
> l'énumération est `Dynamic = 0, Static = 1`. Le risque n'était donc pas théorique.
> L'outil d'éditeur **SC4VE > Démonstration > 2** corrige les dix prefabs concernés, et la
> construction de scène l'exécute d'elle-même avant d'en instancier.

En contrepartie, `Dynamic` offre gratuitement la dimension temporelle : le graphe enregistre
un intervalle, donc *à quel moment* le steak est devenu cuit. Rien à écrire pour l'obtenir.

#### Les stations

Transformer un aliment, c'est le **poser à un endroit** — aucune nouvelle commande n'est
nécessaire, `PutInCommand` suffit :

| Station | Effet | Énoncé |
|---|---|---|
| Planche à découper | ajoute `Sliced` | « mets la pomme sur la planche » |
| Plaque de cuisson | ajoute `Cooked` | « mets le steak sur la plaque » |

Deux décisions de simplification, à assumer :

- **L'ordre des états est sans importance.** Couper puis cuire une pomme de terre donne le
  même résultat que l'inverse : seul compte l'ensemble final des états. Cela évite tout
  système de séquencement.
- **La transformation prend du temps mais ne rate pas.** Un délai court (2–4 s) rend l'action
  visible et occupe la station, sans introduire d'état d'échec. Pas d'aliment brûlé : ce serait
  une action réflexe, contraire au §2. Si l'on veut plus tard un état `Burnt`, il faudra une
  fenêtre large et un avertissement vocal — à traiter comme une extension, pas comme un
  acquis.

**Coût visuel** : « cuit » se rend par un simple changement de matériau (assombrissement), déjà
faisable. « Coupé » demande un mesh de remplacement par ingrédient concerné — c'est le vrai
coût, à mutualiser (un mesh générique « en morceaux » teinté par ingrédient est acceptable
pour la démo).

### 6.3 Les autres classes du domaine

À ajouter dans le même `sven-restaurant.ttl` :

```
Container ─┬─ Plate    (assiette — seul contenant de service)
           ├─ Bin      (poubelle)
           └─ Station ─┬─ CuttingBoard  (planche à découper)
                       └─ Stove         (plaque de cuisson)
Furniture ─── Table
Person ────┬─ Waiter
           └─ Customer
Dish ──────┬─ Salad ──────┬─ FruitSalad     salade de fruits
           │              ├─ CaesarSalad    salade César
           │              └─ SteakFrites    steak frites
           ├─ Soup ───────┬─ FishSoup       soupe de poisson
           │              ├─ PumpkinSoup    soupe de citrouille
           │              └─ CarrotSoup     soupe de carottes
           └─ Sandwich ───┬─ SalmonSandwich    sandwich au saumon
                          ├─ CruditesSandwich  sandwich crudités
                          └─ BeefSandwich      sandwich au bœuf
```

> **Les stations sont des contenants.** `Station ⊑ Container` n'est pas une commodité de
> rangement : c'est ce qui permet à `PutInCommand` de viser une planche ou une plaque sans une
> ligne de code de plus. « Mets les fruits dans l'assiette » et « mets le steak sur la
> plaque » sont la même commande.

> **Définition de `Salad` dans cette ontologie** : *un mélange d'ingrédients servi en
> assiette*. C'est un mode de composition, pas le sens culinaire courant — « steak frites »
> y appartient donc à juste titre, au même titre que la salade de fruits. Les trois familles
> se distinguent par la façon dont les ingrédients sont assemblés et servis : mélangés en
> assiette (`Salad`), sous forme liquide (`Soup`), dans du pain (`Sandwich`). L'`rdfs:comment`
> de la classe doit énoncer cette définition, sinon un relecteur la lira au sens culinaire.

#### Un plat est un contenant rempli, pas un objet créé

Décision actée. « Salade de fruits » n'est **pas** un nouvel objet fabriqué à la préparation :
c'est une assiette dont on inspecte le contenu. Conséquences, dans l'ordre :

- **Le joueur manipule jusqu'au bout.** Un plat à moitié fait est un état visible et valide,
  pas un « en cours » abstrait. On peut retirer un ingrédient de trop.
- **`Dish` n'est pas une classe d'objets, c'est une spécification de recette.** Les neuf
  classes ci-dessus nomment les recettes — c'est à elles que renvoie le `RecipeParameter`, et
  ce sont elles qui portent les ingrédients et états requis. Elles ne classent aucune instance.
- **La conformité reste une requête SPARQL, jamais une classification OWL.** La tentation
  serait d'écrire `FruitSalad ≡ Plate ⊓ ∃contains.SlicedApple ⊓ …`, mais OWL est en monde
  ouvert : on ne peut pas y exprimer « et rien d'autre ». Or une assiette contenant en plus
  un steak n'est pas une salade de fruits. La clôture s'exprime en SPARQL
  (`FILTER NOT EXISTS`), pas en OWL. C'est la limite à connaître avant de perdre une journée
  à faire raisonner le raisonneur.

#### Le coût réel de cette décision : la relation de contenance

**SVEN ne modélise pas la contenance.** `sven:Transform` porte `position`, `rotation`, `scale`
— et rien qui relie un objet à son parent ou à son contenant. Sans cette relation, aucune
requête ne peut dire ce qu'il y a dans l'assiette, et toute la conformité s'effondre.

Il faut donc **ajouter une relation `contains`**, sur le modèle exact de `SemanticAnnotator` :
un composant `Container` exposant son contenu comme une `ComponentProperty`, dont le délégué
d'assertion émet un triplet par objet contenu — c'est déjà ce que fait `SemanticAnnotator`
pour sa liste d'annotations, il n'y a qu'à recopier le motif.

Ce composant doit être en **`SemanticProcessingMode.Dynamic`**, pour la même raison que les
états (§6.2) : le contenu change en cours de partie.

C'est le premier vrai développement du lot 2, et le seul point où la décision « plat =
contenant » coûte plus cher que « plat = objet ». Elle le vaut : c'est elle qui rend la
manipulation ingrédient par ingrédient possible, donc tout le §3.

**Rappel du mécanisme d'annotation** (à respecter à la lettre, sinon la sélection ne trouvera
rien) :

1. Classe C# héritant d'une classe parente, implémentant `ISemanticAnnotation` et
   `IComponentMapping`, exposant `public static new string SemanticTypeName => "sven:Waiter";`
   et une `ComponentMapping()` nommée `"WaiterComponent"`.
2. Classe OWL dans le `.ttl`, avec l'`owl:equivalentClass` par restriction sur
   `sven:component` `owl:someValuesFrom sven:WaiterComponent` — c'est **cette restriction**
   qui permet à l'inférence de classer l'objet.
3. Labels `@fr` **et** `@en` obligatoires (ils alimentent le vocabulaire de reconnaissance).
4. Déclarer le fichier dans `Assets/StreamingAssets/Ontologies/ontologies_index.json`.

Le modèle à copier est `sven-fruits.ttl` + `Apple.cs`.

**Coût en assets** : 8 nouveaux meshes (`Potato`, `Lettuce`, `Tomato`, `Beef`, `Chicken`,
`Salmon`, `Cheese`, `Bread`) contre 4 existants. La sémantique ne dépend pas du mesh — un
primitif coloré suffit pour valider le lot 1 — mais la lisibilité de la démo, si. Traiter
l'acquisition des modèles comme une tâche à part, non bloquante.

### 6.4 Les neuf recettes

Un plat est **un ensemble d'ingrédients dans une assiette**, jamais un procédé : pas de
cuisson, pas de découpe (§11). « Frites » = `Potato` tel quel.

**Un seul contenant pour les neuf recettes** (§5). Ce que la famille du plat déterminait
auparavant — assiette, bol ou barquette — n'existe plus : le rôle fonctionnel que jouait la
taxonomie est bel et bien perdu, et c'est le prix de la simplification.

Elle garde toutefois un rôle, et un meilleur : **elle permet les commandes sous-spécifiées.**
Un client qui dit « je voudrais une soupe » ne nomme aucune recette précise mais désigne trois
candidates — ce qui déclenche exactement le mécanisme que le système sait le mieux montrer :
la clarification. La famille passe donc du rôle de contrainte matérielle à celui de niveau
d'abstraction dans le dialogue.

Notation des états : ✂ = `Sliced`, 🔥 = `Cooked`, rien = brut et entier.

| Plat | Famille | Ingrédients (avec état requis) |
|---|---|---|
| Salade de fruits | `Salad` | Apple ✂ + Banana ✂ |
| Salade César | `Salad` | Lettuce ✂ + Chicken 🔥 + Cheese + Bread ✂ |
| Steak frites | `Salad` | Beef 🔥 + Potato ✂🔥 |
| Soupe de poisson | `Soup` | Salmon 🔥 + Potato ✂🔥 + Carrot ✂ |
| Soupe de citrouille | `Soup` | Pumpkin ✂🔥 + Potato ✂🔥 |
| Soupe de carottes | `Soup` | Carrot ✂🔥 + Potato ✂🔥 |
| Sandwich au saumon | `Sandwich` | Bread ✂ + Salmon **cru** + Lettuce |
| Sandwich crudités | `Sandwich` | Bread ✂ + Lettuce ✂ + Tomato ✂ + Carrot ✂ + Cheese — **tout cru** |
| Sandwich au bœuf | `Sandwich` | Bread ✂ + Beef 🔥 + Tomato ✂ + Cheese |

Cinq propriétés de ce jeu de recettes sont **voulues** et ne doivent pas être « simplifiées » :

- **Les ingrédients sont partagés** — Potato dans 4 plats, Bread dans 4, Lettuce et Carrot
  dans 3. Aucun ingrédient n'est le seul objet d'une couleur ou d'une forme sur le plan de
  travail : le joueur est obligé de désigner précisément au lieu d'attraper le seul truc
  orange.
- **Soupe de citrouille et soupe de carottes ne diffèrent que par un ingrédient.** C'est le
  cas d'erreur le plus intéressant du jeu, et le meilleur test de la résolution référentielle.
- **Deux paires quasi jumelles côté sandwich** (crudités / bœuf partagent pain, tomate,
  fromage) forcent l'écoute de la commande client jusqu'au bout.
- **Le même ingrédient est demandé dans des états différents selon le plat** : la carotte est
  crue et coupée dans le sandwich crudités, coupée et cuite dans la soupe. Une carotte posée
  sur le plan de travail n'est donc pas interchangeable avec une autre — c'est ce qui
  transforme « mets les carottes dans l'assiette » en énoncé qui demande de regarder.
- **Le sandwich crudités interdit un état.** « Crudités » veut dire cru : cuire la carotte
  rend le plat non conforme. C'est la contrainte **négative** sur l'état, symétrique des
  contraintes alimentaires du §6.5, et le meilleur cas de démonstration du modèle d'états —
  le plat échoue non pas parce qu'il manque un ingrédient, mais parce qu'un ingrédient est de
  la mauvaise classe inférée.

Le steak frites est le cas vitrine dans l'autre sens : la pomme de terre y est demandée
**coupée *et* cuite**, donc deux passages en station. C'est la recette la plus longue à
produire, et celle qui justifie à elle seule que les stations existent.

#### Règle de désambiguïsation recette / aliment

Les noms de recettes recouvrent lexicalement des noms d'ingrédients : « soupe de **carottes** »,
« salade de **fruits** », « sandwich au **bœuf** ». Sans règle, « prépare une soupe de
carottes » produirait à la fois un `RecipeParameter` **et** une sélection de carottes.

**La règle, en trois temps :**

1. **Les recettes d'abord, de la plus longue à la plus courte.** Première correspondance →
   `RecipeParameter`.
2. **Le segment reconnu est consommé.** Les mots absorbés par le nom de recette ne peuvent
   plus être réinterprétés comme un ingrédient.
3. **Les ingrédients ensuite, sur le texte restant, du plus long au plus court** —
   « pomme de terre » avant « pomme ».

« Le plus précis » se lit donc « le plus long », ce qui est déjà la convention du moteur :
`RuleBasedTriggersAttribute` trie tous les déclencheurs par longueur décroissante avant
comparaison. La règle ci-dessus ne fait qu'étendre cette convention aux recettes, en les
plaçant devant les annotations.

**Vérification sur les cas qui font mal :**

| Énoncé | Recette consommée | Ingrédients restants | Résultat |
|---|---|---|---|
| « prépare une soupe de carottes » | soupe de carottes | — | recette seule ✔ |
| « coupe les carottes pour la soupe de carottes » | soupe de carottes | carottes | découpe des carottes ✔ |
| « mets une carotte dans la soupe de carottes » | soupe de carottes | carotte | ajout ciblé ✔ |
| « prends la pomme de terre » | — | pomme de terre (pas « pomme ») | ✔ |

**En mode LLM**, la même priorité doit être énoncée dans la description du `RecipeParameter` :
« si un segment correspond à un nom de recette connu, il désigne la recette et non ses
ingrédients ». Le LLM ne consomme pas de segments, mais il suit une consigne explicite.

**Cas non couvert, et assumé :** une phrase qui parle d'un plat *déjà préparé* comme d'un
référent (« apporte la soupe de carottes à cette table 👆 ») produira un `RecipeParameter`
alors qu'il faudrait une sélection d'objet. À traiter au lot 4, quand `BringCommand` existera —
la distinction se fera sur le verbe, pas sur le nom.

### 6.5 Contraintes alimentaires — le cœur de la démonstration

C'est là que se trouve la valeur scientifique : la **vérification de conformité est une
requête SPARQL sur les classes d'ingrédients**, pas du code métier.

| Contrainte du client | Exclut la classe | Plats conformes |
|---|---|---|
| « végétarien » | `Meat` ∪ `Fish` | salade de fruits, soupe de citrouille, soupe de carottes, sandwich crudités |
| « végétalien » | `Meat` ∪ `Fish` ∪ `Dairy` | salade de fruits, soupe de citrouille, soupe de carottes |
| « allergique au poisson » | `Fish` | tout sauf soupe de poisson et sandwich au saumon |
| « sans gluten » | `Bakery` | salade de fruits, steak frites, les trois soupes |
| « sans lactose » | `Dairy` | salade de fruits, steak frites, les trois soupes, sandwich au saumon |
| « sans banane » | `Banana` | tout sauf salade de fruits |

Chaque contrainte découpe un sous-ensemble **différent et non trivial** — c'est ce qui rend
la mécanique intéressante à jouer plutôt que décorative.

> **Règle absolue : ne jamais ajouter une propriété booléenne `isVegetarian` sur un plat.**
> Ce serait défaire tout l'intérêt de la démo. La conformité doit être **déduite** de la
> classe des ingrédients. Le point à montrer : une contrainte formulée sur une classe haute
> (« pas de viande ») rejette un plat contenant une instance de sous-classe (`Chicken`), sans
> que personne n'ait écrit la règle pour ce cas — et le jour où l'on ajoute `Pork`, la règle
> continue de marcher toute seule.

### 6.6 Déclaration des commandes dans `sc4ve.ttl`

Chaque nouvelle commande doit y figurer avec ses restrictions de paramètres, sinon **elle ne
déclenchera jamais de clarification** et s'exécutera silencieusement sur 0 objet. Modèle
existant à copier : `sc4ve:MoveCommand`. Un paramètre optionnel ne doit **pas** avoir de
restriction.

Tout nouveau type de paramètre (cf. `RecipeParameter` ci-dessous) doit aussi recevoir sa
propre `sc4ve:clarification` bilingue.

---

## 7. Nouvelles commandes

| Commande | Énoncé type | Paramètres | Difficulté |
|---|---|---|---|
| `PutInCommand` | « mets tous les fruits rouges dans le saladier » | `SelectionParameter` (objets) + `SelectionParameter` (contenant) | faible |
| `PrepareCommand` | « prépare une salade de fruits » | `RecipeParameter` (nouveau) | faible |
| `GoToCommand` | « toi 👆 va là-bas 👆 » | `SelectionParameter` (agent) + `PointParameter` | faible |
| `TakeOrderCommand` | « va prendre la commande de cette table-là 👆 » | `SelectionParameter` (agent) + `SelectionParameter` (table) | moyenne |
| `ServeCommand` | « va servir cette table-là 👆 » | `SelectionParameter` (agent) + `SelectionParameter` (table) | moyenne |
| `BringCommand` | « apporte ça 👆 à ce client-là 👆 » | agent + objet + destinataire | **élevée** |
| `StopCommand` | « stop », « attends » | `SelectionParameter` (agent) | faible |
| `RepeatOrderCommand` | « répète la commande de cette table 👆 » | `SelectionParameter` (table) | faible |
| `CutCommand` *(option)* | « coupe la pomme » | `SelectionParameter` (objets) | faible |
| `CookCommand` *(option)* | « fais cuire le steak » | `SelectionParameter` (objets) | faible |
| `EmptyCommand` *(option)* | « vide l'assiette » | `SelectionParameter` (contenant) | faible |

**Les stations ne coûtent aucune commande.** Découper et cuire, c'est poser un aliment
quelque part : `PutInCommand` visant une `Station` suffit (§6.2). `CutCommand` et
`CookCommand` ne sont que des **formulations alternatives** de la même action — le verbe
d'action au lieu du locatif. Elles ne sont pas nécessaires au jeu, mais elles démontrent
bien quelque chose : que « coupe la pomme » et « mets la pomme sur la planche » convergent
vers la même intention. À traiter comme un bonus de fin de lot 2, pas comme un prérequis.

### Notes d'implémentation

**`GoToCommand` est un calque de `MoveCommand`.** Même forme de paramètres
(`BuildSelectionParameter` + `BuildDestinationParameter`), seul `Execute()` change : au lieu
d'écrire `transform.position`, il pose une destination sur le `NavMeshAgent`. Le fallback
`Pointer.PointerHitPosition` de `MoveCommand` (quand la position n'est pas encore dans le
graphe RDF) est à reprendre tel quel.

**`PutInCommand` — attention à un piège existant.** La description LLM de `MoveCommand`
annonce déjà « soit `PointParameter` (destination) soit `SelectionParameter` (destination) »,
mais `MoveCommand.Execute()` ne lit que le `PointParameter` : un second `SelectionParameter`
émis par le LLM est ignoré en silence. Deux options — étendre `MoveCommand`, ou créer
`PutInCommand`. Recommandation : **créer `PutInCommand`**, parce que l'effet diffère
réellement (parentage dans le contenant + rangement, pas une téléportation à un point). Et
corriger la description de `MoveCommand` pour qu'elle cesse de promettre ce qu'elle ne fait
pas — c'est une correction séparée, à ne pas mélanger au chantier.

**Deux `SelectionParameter` dans la même commande.** `GetParameter<T>()` renvoie le premier,
`GetParameter<T>(2)` le second : la mécanique existe déjà. Ce qui compte est que **l'ordre
soit stable** et que la description `CommandDescription` l'énonce sans ambiguïté pour le LLM
(« le premier `SelectionParameter` est l'agent, le second est la destination »).

**`TakeOrderCommand` et `ServeCommand` ont la même signature** (agent + table) et ne diffèrent
que par la macro exécutée. Les écrire ensemble ; si l'une marche, l'autre marche.

**`RepeatOrderCommand` ne délègue rien et ne change rien au monde** : elle réénonce la commande
d'une table en TTS, immédiatement. C'est la seule commande de ce type avec `SpeechCommand`,
dont elle réutilise la machinerie.

Deux choix à assumer sur elle :

- **Elle est instantanée, pas déléguée.** On pourrait la modéliser en renvoyant un serveur
  demander au client — plus cohérent avec la fiction, mais ce serait punir un besoin légitime.
  Un visiteur qui n'a pas compris n'a pas commis d'erreur de jeu.
- **C'est une commande d'accessibilité autant que de confort.** En conférence internationale,
  une partie du public n'aura pas l'oreille française ; entre le tableau (§5) et cette
  commande, la compréhension de la commande client ne dépend plus d'avoir bien entendu du
  premier coup. À ce titre, elle n'est **pas** optionnelle.

**`EmptyCommand` est un raccourci, pas un mécanisme.** Vider se fait déjà par
« mets tout ça dans la poubelle » (`PutInCommand`). Comme `CutCommand` et `CookCommand`, elle
n'existe que pour démontrer qu'une formulation directe et une formulation locative convergent.

**`BringCommand` est la seule vraiment nouvelle en forme** (trois rôles référentiels). À ne
traiter qu'au lot 4, voire à abandonner si `ServeCommand` suffit à la démo.

**`RecipeParameter` est le seul nouveau type de paramètre.** Purement linguistique : aucun
pointage ne peut le fournir, ce qui en fait un bon contrepoids aux commandes déictiques.

**Rien à faire côté LLM** : découverte par réflexion. Tout l'effort est dans la qualité des
`CommandDescription` et, en RuleBased, dans les `[RuleBasedTriggers]`.

---

## 8. Le registre de délégation

C'est la partie neuve du système. Un serveur est un `SemantizationCore` (donc sélectionnable,
colorable, descriptible comme n'importe quel objet) **plus** une machine à états.

### États

```
Idle ──(ordre reçu)──► Moving ──(arrivé)──► Acting ──(fini)──► Moving ──► Idle
  ▲                                                                        │
  └────────────────────────────(StopCommand)───────────────────────────────┘
```

### Règles de conception

- **File d'attente à un seul créneau.** Un serveur occupé qui reçoit un ordre le **refuse à
  voix haute** (« je termine cette table »). C'est plus simple qu'une file, plus lisible pour
  le joueur, et ça met en scène le retour vocal.
- **Le serveur porte réellement le plat, un seul à la fois.** Le contenant est parenté à sa
  main : c'est un objet de la scène qui se déplace, donc SVEN enregistre sa trajectoire et la
  contenance reste vraie pendant le transport. Une simple icône flottante ne le ferait pas —
  le plat serait « porté » sans que le graphe le sache, et une question comme « où est la
  soupe ? » deviendrait sans réponse. Si le portage physique s'avère pénible à câbler sur un
  agent en déplacement, l'icône reste acceptable **en complément** (un pictogramme au-dessus
  de la tête indiquant quel plat est transporté), jamais **en remplacement**.
- **Un seul plat à la fois est ce qui rend le refus lisible.** Sans cette contrainte, « je
  suis occupé » paraîtrait arbitraire ; avec elle, le joueur voit pourquoi.
- **Un plat refusé revient à la passe, porté par le serveur.** Il ne disparaît pas et ne se
  vide pas tout seul : le joueur doit le vider à la poubelle avant de réutiliser le contenant.
  C'est le seul endroit du jeu où une erreur coûte du temps, et c'est voulu — mais le coût
  doit rester lisible, donc le serveur **annonce le refus à voix haute** en rapportant le plat,
  plutôt que de le reposer en silence.
- **Tout ordre est interruptible** par `StopCommand`, qui ramène à `Idle`.
- **Le serveur parle à chaque transition importante** : accusé de réception, refus, échec
  (« je ne trouve pas de plat pour cette table »), fin de tâche. C'est le canal Piper.
- **Un échec est une information, pas un bug** : il doit être énoncé, pas loggé en silence.

### Ce que ça implique côté sémantisation

L'état de tâche du serveur doit être exposé dans le graphe RDF au même titre que sa position
ou sa couleur, sinon « qui est libre ? » n'est pas répondable et `DescribeCommand` sur un
serveur ne dira rien d'intéressant. C'est le point à vérifier tôt (lot 3).

---

## 9. Lots de chantier

Chaque lot se termine sur un critère vérifiable. Ne pas démarrer le suivant tant que le
critère n'est pas atteint.

### Lot 0 — Le jeu sans la voix

Scène VR, 2 tables, 1 serveur, 3 recettes, pilotage entièrement à la manette.

> **Vérifier :**
> 1. on peut terminer un service complet sans prononcer un mot, et c'est plaisant.
>    **C'est le garde-fou du projet** : si le jeu n'est pas amusant à la manette, la commande
>    vocale ne le sauvera pas — et on l'aura appris avant d'avoir écrit une commande ;
> 2. tout est atteignable sans locomotion, dans une aire debout d'environ 2 × 2 m ;
> 3. `GrabCommand` / `ReleaseCommand` fonctionnent en casque via le XR Interaction Toolkit
>    (code écrit, **jamais compilé ni testé** — voir risque n° 4 du §12) ;
> 4. le journal (§10) écrit déjà une ligne par action, mode inclus.

### Lot 1 — Le domaine sémantique

`sven-restaurant.ttl` + classes C# d'annotation + prefabs annotés + index mis à jour.

> **Vérifier**, dans l'ordre :
> 1. le `SemanticAnnotator` des aliments est en `Dynamic` — ajouter `sven:Cooked` à la volée
>    sur un objet le fait bien apparaître dans le graphe (§6.2). **Rien d'autre ne marche
>    tant que ce point n'est pas acquis** ;
> 2. « sélectionne les fruits » surligne pommes et bananes, et **pas** les carottes ;
> 3. « sélectionne les serveurs » désigne les agents ;
> 4. un objet portant `Beef` + `Cooked` est bien inféré comme `CookedBeef` **et** comme
>    `Beef`.

### Lot 2 — Contenants, stations, recettes, conformité

**La relation de contenance d'abord** (§6.3) — rien d'autre ne peut être vérifié tant qu'une
requête ne sait pas dire ce que contient une assiette. Puis `PutInCommand`, les deux stations,
`PrepareCommand`, `RecipeParameter`, requête SPARQL de conformité.

> **Vérifier :**
> 0. une requête SPARQL énumère correctement le contenu d'un contenant, et le suit quand on
>    ajoute ou retire un objet ;
> 1. « mets tous les fruits rouges dans le saladier » remplit le saladier en un énoncé ;
> 2. une pomme de terre passée à la planche puis à la plaque est reconnue coupée **et** cuite,
>    dans n'importe quel ordre ;
> 3. un sandwich crudités contenant une carotte cuite est jugé **non conforme** ;
> 4. les neuf noms de recettes sont extraits sans être confondus avec les noms d'ingrédients
>    qu'ils contiennent (risque n° 2 du §12).

### Lot 3 — Agents et délégation

NavMesh, machine à états, exposition de l'état dans le graphe, `GoToCommand`,
`TakeOrderCommand`, `ServeCommand`, `StopCommand`, portage du contenant, retours vocaux.

> **Vérifier :**
> 1. « toi 👆, va servir cette table-là 👆 » aboutit à un plat déposé sur la bonne table ;
> 2. le contenant est **visiblement porté** pendant le trajet, et une requête sur son contenu
>    reste juste pendant qu'il se déplace ;
> 3. un second ordre pendant le trajet est refusé à voix haute ;
> 4. le ralenti pendant la parole (§2) ne perturbe ni le déplacement des agents ni le
>    pipeline vocal.

### Lot 4 — Clients, prise de commande, contraintes

Arrivée des clients, `TakeOrderCommand` bouclée de bout en bout, commande énoncée en TTS et
inscrite au tableau, `RepeatOrderCommand`, jauge de patience en temps de jeu, contrainte
alimentaire, refus d'un plat non conforme et retour du contenant.

> **Vérifier :**
> 1. un client ne parle **qu'une fois** qu'un serveur est arrivé à sa table ;
> 2. la commande énoncée s'inscrit au tableau et reste consultable ;
> 3. « répète la commande de cette table 👆 » réénonce la commande, immédiatement ;
> 4. un client qui demande « sans banane » refuse effectivement un plat contenant une banane,
>    et l'accepte sinon — sans règle codée en dur pour ce cas précis ;
> 5. un plat refusé revient à la passe, et son contenant redevient utilisable **seulement**
>    après avoir été vidé à la poubelle ;
> 6. la jauge de patience ralentit bien avec `Time.timeScale` pendant la parole ;
> 7. une commande sous-spécifiée (« je voudrais une soupe ») déclenche une clarification et
>    non un choix arbitraire parmi les trois soupes.

### Lot 5 — Score, niveaux, instrumentation

Progression 1 → 4 tables, score de satisfaction, journalisation (§10).

> **Vérifier :** une partie complète produit un journal exploitable.

---

## 10. Instrumentation

Le jeu est aussi un protocole d'évaluation déguisé. Deux scores en parallèle :

- **visible** — satisfaction des clients, plats servis, plats refusés ;
- **invisible** — **le mode choisi au lancement**, nombre d'énoncés, nombre de tours de
  clarification, échecs de résolution référentielle, latence de chaque commande, temps entre
  deux commandes, répartition parole seule / geste seul / hybride.

Le second est exactement ce qu'on mesurerait dans une étude utilisateur, mais collecté pendant
que les gens jouent, sans consigne et sans biais de tâche artificielle.

Le mode est la variable la plus précieuse : journalisé avec chaque énoncé, il fait de l'écran
de départ (§2) un plan d'expérience — deux populations, deux modes, les mêmes métriques,
et la comparaison latence contre qualité d'interprétation se collecte d'elle-même. Sans cette
colonne, tout le reste du journal est ininterprétable.

**À prévoir dès le lot 0** : le journal coûte presque rien à poser au début et beaucoup à
rétro-adapter.

---

## 11. Hors périmètre

À refuser explicitement pour éviter la dérive :

- **la cuisson et la découpe existent, mais réduites à leur plus simple expression** : poser
  un aliment sur une station lui ajoute un état après un court délai. Pas de degré de cuisson,
  pas de découpe fine, pas d'aliment brûlé, pas de physique de liquides ;
- pas de troisième station ni de troisième état sans qu'une recette l'exige ;
- **un seul type de contenant de service, l'assiette** — pas de bol, pas de barquette, pas de
  verre, quelle que soit la vraisemblance culinaire ;
- pas d'IA de client au-delà d'un minuteur et d'une commande ;
- pas de multijoueur ;
- **pas de locomotion artificielle** : ni téléportation, ni déplacement au joystick. Seul le
  déplacement physique du joueur dans son aire de jeu est permis ;
- pas d'économie, de progression persistante, de déblocage ;
- pas de pathfinding dynamique complexe : des trajets simples suffisent, ils ne servent qu'à
  masquer la latence.

**Version minimale acceptable pour une démonstration** : 1 serveur, 2 tables, 3 recettes, pas
de score. On perd l'orchestration parallèle mais on garde la délégation, la contrainte
sémantique, l'énoncé hybride et le retour vocal — c'est-à-dire tout ce qui compte. Viser ça
d'abord, étendre ensuite.

---

## 12. Risques identifiés

**1. Deux déictiques dans le même énoncé, en mode RuleBased.** C'est le vrai point dur.
« Toi 👆 va servir cette table-là 👆 » exige d'attribuer le premier pointage à l'agent et le
second à la destination. `RuleBasedContext` a bien une notion d'ordre temporel
(`BuildGrabPointParameter` prend le timestamp du premier déictique), mais rien ne segmente
aujourd'hui une phrase en deux zones référentielles.

**Atténué** par le choix de mode au lancement (§2) : le mode RuleBased couvre le sous-ensemble
à un seul déictique et l'annonce au joueur, l'énoncé hybride complet appartient au mode LLM.
Reste à trancher au lot 3 : segmenter la phrase en deux zones référentielles, ou assumer
définitivement la couverture partielle.

**2. Les noms de plats contiennent des noms d'ingrédients.** **Résolu en conception** — voir
la règle de désambiguïsation du §6.4 : recettes d'abord (plus longue d'abord), segment
consommé, puis ingrédients sur le texte restant. Reste à **vérifier** au lot 2 sur les neuf
recettes, et un cas est explicitement laissé de côté jusqu'au lot 4 (le plat déjà préparé
employé comme référent).

**3. L'annulation d'un changement d'état.** Les autres commandes sont réversibles via
`ExecuteReversible` / `CommandHistory`, mais « annule » après une cuisson supposerait de
**retirer** une annotation — l'inverse de la monotonie posée au §6.2, et une opération dont le
support à l'exécution n'est pas acquis. Décision par défaut : **la mise en station n'est pas
annulable**, et le joueur jette l'aliment raté. À énoncer clairement au joueur (retour vocal)
plutôt qu'à laisser échouer en silence.

**4. `GrabCommand` / `ReleaseCommand` étaient liés au contrôleur bureau.** **Traité** :
les deux commandes passent maintenant par le XR Interaction Toolkit quand la scène contient un
`XRInteractionManager`, et retombent sur `DemoCharacterController` sinon — les scènes bureau
existantes continuent donc de fonctionner. `Unity.XR.Interaction.Toolkit` a été ajouté aux
références de `SC4VE.asmdef`.

Reste à faire côté scène, non couvert par le code : les prefabs d'objets manipulables doivent
porter un `XRGrabInteractable`, et la scène un `XRInteractionManager` et au moins un
interactor. **Ce code n'a pas été compilé ni testé en casque** — à valider au lot 0.

**5. Le volume de travail Unity est important.** NavMesh, machine à états, spawn de clients,
UI, score : c'est du développement de jeu, et c'est le gros du temps. Ce n'est pas du temps
perdu — le jeu est un livrable de présentation pour les conférences et la soutenance (§1) —
mais il faut le budgéter comme tel, et non le laisser grignoter le temps consacré aux
commandes et à l'ontologie. D'où le lot 0 en premier et le périmètre minimal du §11.

**6. Le confort VR.** Aucune locomotion artificielle : ni téléportation, ni joystick. Le
joueur peut se déplacer **physiquement** pour mieux voir, ce qui suppose que la cuisine tienne
dans une aire de jeu debout réaliste (environ 2 × 2 m) et que tout soit atteignable sans
marcher. La salle est **regardée**, jamais parcourue : c'est ce qui justifie que les serveurs
existent.

---

## 13. Questions ouvertes

**Toutes les questions de conception sont tranchées.** Elles sont reportées dans les sections
concernées : plat = contenant rempli (§6.3), portage réel du plat et retour d'un plat refusé
(§8), prise de commande par le serveur (§4), ralenti implémenté et réglable, patience ralentie
avec lui (§2), six contenants et poubelle (§5), `RepeatOrderCommand` (§7).

Ce qui reste n'est plus une question de conception mais des **valeurs à régler au playtest** —
elles n'ont pas de bonne réponse sur le papier :

| À régler | Valeur de départ | Ce qu'on observe |
|---|---|---|
| Nombre d'assiettes | 6, interchangeables | des blocages ? regarder d'abord la durée d'immobilisation (§5) |
| Patience du client | 45–90 s | le joueur a-t-il le temps d'un aller-retour serveur + préparation ? |
| Délai des stations | 2–4 s | assez long pour être lisible, assez court pour ne pas ennuyer |
| `ListeningTimeScale` | 0,3 | 1 pour comparer sans ralenti |
| Nombre de tables par niveau | 1 → 4 | où se situe le décrochage |

Une seule inconnue de fond subsiste, et elle est déjà au §12 : **la segmentation d'un énoncé à
deux déictiques en mode RuleBased**, à trancher au lot 3.
