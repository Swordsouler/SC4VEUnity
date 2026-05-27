# Référence des commandes SC4VE

Documentation complète de toutes les commandes vocales disponibles, avec phrases-types à prononcer pour valider le bon fonctionnement.

---

## Légende

| Symbole | Signification |
|---|---|
| `[OBJET]` | Nom d'un type d'objet dans l'ontologie (ex : `pomme`, `citrouille`) |
| `[COULEUR]` | Couleur de l'ontologie (ex : `rouge`, `bleu`, `vert`) |
| `👆 [DÉICTIQUE]` | Mot de pointage (`ça`, `ceci`) prononcé **en pointant** l'objet avec le contrôleur |
| ↩️ | La commande supporte **l'annulation** via `UndoCommand` |
| ⚠️ | Note / limitation importante |

**Spécification des objets** — dans toutes les commandes qui opèrent sur des objets, la sélection peut se faire de quatre façons :

| Méthode | Exemple | Mécanisme |
|---|---|---|
| Par annotation | *« les pommes »* | Filtre `Annotation` |
| Par couleur | *« les objets rouges »* | Filtre `Color` |
| Par pointage déictique | *« 👆 ça »* | Filtre `Event` (timestamp du pointeur) |
| Par coréférence | *« les »*, *« eux »*, *« ça »* après une commande | Filtre `Coreference` → `LastObjects` |

---

## Sommaire par catégorie

1. [Visibilité](#1-visibilité)
2. [Taille](#2-taille)
3. [Transformation géométrique](#3-transformation-géométrique)
4. [Couleur](#4-couleur)
5. [Manipulation d'objets](#5-manipulation-dobjets)
6. [Sélection](#6-sélection)
7. [Navigation caméra](#7-navigation-caméra)
8. [Information](#8-information)
9. [Historique et état de la scène](#9-historique-et-état-de-la-scène)
10. [Tableau récapitulatif](#10-tableau-récapitulatif)

---

## 1. Visibilité

### HideCommand
**Rend les objets invisibles** (désactive le Renderer).

| Phrase | Résultat attendu |
|---|---|
| `« Cache les pommes. »` | Toutes les pommes disparaissent. |
| `« Masque les objets rouges. »` | Les objets rouges sont cachés. |
| `« 👆 Rends ça invisible. »` | L'objet pointé est masqué. |
| `« Cache-les. »` *(après une commande)* | Les objets de la commande précédente sont masqués. |

---

### ShowCommand
**Rend les objets visibles** (réactive le Renderer).

| Phrase | Résultat attendu |
|---|---|
| `« Montre les pommes. »` | Les pommes réapparaissent. |
| `« Révèle les objets bleus. »` | Les objets bleus sont rendus visibles. |
| `« Affiche tout ça. »` *(en pointant)* | L'objet pointé est réaffiché. |
| `« Démasque-les. »` *(après HideCommand)* | Les objets précédents redeviennent visibles. |

---

### SetTransparentCommand ↩️
**Rend les objets semi-transparents** (alpha 30%, mode Transparent du Standard Shader).

| Phrase | Résultat attendu |
|---|---|
| `« Rends les pommes transparentes. »` | Les pommes deviennent semi-transparentes. |
| `« 👆 Rends ça transparent. »` | L'objet pointé est semi-transparent. |
| `« Transparence sur les objets rouges. »` | Les objets rouges passent à alpha 30%. |

> ⚠️ Requiert un matériau **Standard Shader** Unity pour le changement de mode de rendu. Sur d'autres shaders (URP Lit, etc.), seul l'alpha est modifié sans garantie du rendu.

---

### SetOpaqueCommand ↩️
**Remet les objets entièrement opaques** (alpha 100%).

| Phrase | Résultat attendu |
|---|---|
| `« Rends les pommes opaques. »` | Les pommes redeviennent solides. |
| `« Enlève la transparence. »` | L'objet sélectionné repasse en opaque. |
| `« 👆 Opaque. »` | L'objet pointé est rendu opaque. |

---

### HighlightCommand ↩️
**Active/désactive une mise en évidence** (émission jaune sur le matériau). Appeler deux fois la même commande sur le même objet éteint le highlight.

| Phrase | Résultat attendu |
|---|---|
| `« Surligne les pommes. »` | Les pommes s'illuminent en jaune. |
| `« Met en évidence les objets rouges. »` | Les objets rouges sont mis en évidence. |
| `« 👆 Surligne ça. »` | L'objet pointé s'illumine. |
| `« Surligne les pommes. »` *(2e fois)* | L'émission est désactivée. |

> ⚠️ Requiert que le matériau dispose d'une propriété `_EmissionColor` (Standard Shader).

---

## 2. Taille

### ScaleUpCommand ↩️ *(existant)*
**Agrandit les objets** (×1.1).

| Phrase | Résultat attendu |
|---|---|
| `« Agrandis les pommes. »` | Les pommes grossissent légèrement. |
| `« 👆 Grossis ça. »` | L'objet pointé est agrandi. |
| `« Scale up les objets bleus. »` | Les objets bleus grossissent. |

---

### ScaleDownCommand ↩️ *(existant)*
**Réduit les objets** (÷1.1).

| Phrase | Résultat attendu |
|---|---|
| `« Réduis les pommes. »` | Les pommes rapetissent légèrement. |
| `« 👆 Rapetisse ça. »` | L'objet pointé est réduit. |
| `« Diminue la taille des objets rouges. »` | Les objets rouges rétrécissent. |

---

### ResetScaleCommand ↩️
**Remet la taille à (1, 1, 1).**

| Phrase | Résultat attendu |
|---|---|
| `« Taille normale pour les pommes. »` | Les pommes retrouvent la scale (1,1,1). |
| `« Reset la taille de ça. »` *(en pointant)* | L'objet pointé retrouve (1,1,1). |
| `« Taille originale. »` *(après ScaleUp/Down)* | Les objets de la commande précédente retrouvent (1,1,1). |

---

## 3. Transformation géométrique

### RotateRightCommand ↩️
**Pivote les objets de 45° vers la droite** (autour de l'axe Y monde).  
Trigger générique `« tourne »` sans précision de direction → rotation droite par défaut.

| Phrase | Résultat attendu |
|---|---|
| `« Tourne les pommes à droite. »` | Les pommes pivotent de +45° autour de Y. |
| `« 👆 Tourne ça. »` | L'objet pointé pivote de +45°. |
| `« Pivote les objets rouges à droite. »` | Les objets rouges tournent vers la droite. |

---

### RotateLeftCommand ↩️
**Pivote les objets de 45° vers la gauche** (autour de l'axe Y monde).

| Phrase | Résultat attendu |
|---|---|
| `« Tourne les pommes à gauche. »` | Les pommes pivotent de −45° autour de Y. |
| `« 👆 Pivote ça à gauche. »` | L'objet pointé tourne à gauche. |
| `« Rotation gauche sur les objets bleus. »` | Les objets bleus pivotent à gauche. |

> 💡 Répéter la commande plusieurs fois pour des angles plus grands (chaque appel ajoute 45°).

---

### FlipCommand ↩️
**Retourne les objets à 180°** (rotation autour de Y, équivalent miroir vertical).

| Phrase | Résultat attendu |
|---|---|
| `« Retourne les pommes. »` | Les pommes se retrouvent face opposée. |
| `« 👆 Inverse ça. »` | L'objet pointé est retourné. |
| `« Flip les objets rouges. »` | Les objets rouges sont inversés. |

---

### MoveCommand *(existant)*
**Déplace les objets vers la position pointée.**

| Phrase | Résultat attendu |
|---|---|
| `« 👆 Met la pomme ici. »` | La pomme se déplace là où le pointeur indique. |
| `« 👆 Déplace les objets rouges là-bas. »` | Les objets rouges se téléportent au point pointé. |
| `« 👆 Mais ça, ici. »` | L'objet pointé se déplace vers la destination pointée. |
| `« 👆 Bouge-les là. »` *(coréférence)* | Les objets précédents se déplacent vers la destination. |

---

### SnapToGroundCommand ↩️
**Pose les objets sur le sol** (raycast vertical vers le bas).

| Phrase | Résultat attendu |
|---|---|
| `« Pose les pommes au sol. »` | Les pommes descendent jusqu'à toucher le sol (collider). |
| `« 👆 Met ça au sol. »` | L'objet pointé est déposé sur le sol. |
| `« Aligne au sol les objets rouges. »` | Les objets rouges tombent sur le sol. |

> ⚠️ Requiert un collider sur l'objet "sol" de la scène. Sinon, l'objet est déplacé à y=0.

---

### AlignCommand ↩️
**Aligne tous les objets sélectionnés à la même hauteur (Y)** que le premier objet de la sélection.

| Phrase | Résultat attendu |
|---|---|
| `« Aligne les pommes. »` | Toutes les pommes passent à la même hauteur Y que la première. |
| `« Même hauteur pour les objets rouges. »` | Les objets rouges s'alignent horizontalement. |

> ⚠️ Nécessite au minimum 2 objets sélectionnés. Avec un seul objet, la commande n'a aucun effet.

---

### ResetTransformCommand ↩️
**Remet les objets à leur position/rotation/taille d'origine** (état au chargement de la scène).

| Phrase | Résultat attendu |
|---|---|
| `« Réinitialise les pommes. »` | Les pommes retrouvent leur position de départ. |
| `« 👆 Remet ça en place. »` | L'objet pointé retrouve son état initial. |
| `« Reset les objets rouges. »` | Les objets rouges retrouvent leur transform d'origine. |
| `« Position d'origine. »` *(après MoveCommand)* | Les objets de la commande précédente retrouvent leur position initiale. |

> 💡 L'état "d'origine" est capturé automatiquement **au chargement de la scène** via `OriginalStateStore`. Si un objet est créé dynamiquement après le chargement, son état initial est capturé au premier appel de ResetTransformCommand.

---

## 4. Couleur

### ColorizeCommand *(existant)*
**Applique une couleur aux objets.**

| Phrase | Résultat attendu |
|---|---|
| `« Colorie les pommes en rouge. »` | Les pommes deviennent rouges. |
| `« Met les objets bleus en vert. »` | Les objets bleus deviennent verts. |
| `« 👆 Colorie ça en bleu. »` | L'objet pointé devient bleu. |
| `« Mets-les en jaune. »` *(coréférence)* | Les objets précédents deviennent jaunes. |

---

### ColorizeDarkerCommand *(existant)*
**Assombrit la couleur** (divise les composantes RGB par 2).

| Phrase | Résultat attendu |
|---|---|
| `« Assombris les pommes. »` | Les pommes passent à une teinte plus sombre. |
| `« 👆 Noircis ça. »` | L'objet pointé est assombri. |
| `« Rends plus sombre les objets rouges. »` | Les objets rouges foncent. |

---

### ColorizeLighterCommand *(existant)*
**Éclaircit la couleur** (multiplie les composantes RGB par 2).

| Phrase | Résultat attendu |
|---|---|
| `« Éclaircis les pommes. »` | Les pommes passent à une teinte plus claire. |
| `« 👆 Illumine ça. »` | L'objet pointé est éclairci. |
| `« Rends plus clair les objets bleus. »` | Les objets bleus s'éclaircissent. |

---

### ColorizeCopyCommand *(existant)*
**Copie la couleur** d'un objet source vers des objets cibles.

| Phrase | Résultat attendu |
|---|---|
| `« Colorie les pommes comme les citrouilles. »` | Les pommes prennent la couleur des citrouilles. |
| `« Copie la couleur des objets bleus. »` | La couleur bleue est copiée vers les objets sélectionnés. |

> ⚠️ En mode RuleBased, la distinction source/cible repose sur les annotations détectées dans la phrase. Avec une seule annotation, la cible et la source peuvent être ambiguës — privilégier le mode LLM pour les phrases complexes.

---

### ResetColorCommand ↩️
**Remet les objets à leur couleur d'origine** (couleur au chargement de la scène).

| Phrase | Résultat attendu |
|---|---|
| `« Couleur d'origine pour les pommes. »` | Les pommes retrouvent leur couleur initiale. |
| `« Reset la couleur des objets rouges. »` | Les objets rouges retrouvent leur couleur de départ. |
| `« 👆 Restaure la couleur de ça. »` | L'objet pointé retrouve sa couleur initiale. |
| `« Couleur par défaut. »` *(après ColorizeCommand)* | Les objets de la commande précédente retrouvent leur couleur d'origine. |

---

## 5. Manipulation d'objets

### GrabCommand *(existant)*
**Saisit un objet** (interaction physique avec le contrôleur).

| Phrase | Résultat attendu |
|---|---|
| `« 👆 Attrape ça. »` | L'objet pointé est saisi. |
| `« Prends la pomme. »` | La pomme est attrapée. |
| `« Grab l'objet rouge. »` | L'objet rouge est saisi. |

---

### ReleaseCommand *(existant)*
**Lâche l'objet en main.**

| Phrase | Résultat attendu |
|---|---|
| `« Lâche ça. »` | L'objet en main est relâché. |
| `« Pose la pomme. »` | La pomme est déposée. |
| `« Release. »` | L'objet saisi est libéré. |

---

### DuplicateCommand *(existant)*
**Duplique les objets** (crée une copie décalée de +1 unité en Y).

| Phrase | Résultat attendu |
|---|---|
| `« Duplique les pommes. »` | Une copie de chaque pomme est créée au-dessus. |
| `« Clone les objets rouges. »` | Les objets rouges sont clonés. |
| `« 👆 Crée une copie de ça. »` | L'objet pointé est dupliqué. |

---

### DeleteCommand ↩️
**Supprime les objets** (désactivation douce — annulable via `« annule »`).

| Phrase | Résultat attendu |
|---|---|
| `« Supprime les pommes. »` | Les pommes disparaissent de la scène. |
| `« Efface les objets rouges. »` | Les objets rouges sont supprimés. |
| `« 👆 Détruis ça. »` | L'objet pointé est supprimé. |
| `« Retire-les. »` *(coréférence)* | Les objets précédents sont supprimés. |
| `« Annule. »` *(juste après)* | Les objets réapparaissent. |

> ⚠️ La suppression utilise `SetActive(false)` et non `Destroy()` — les objets sont récupérables via `UndoCommand` ou `ResetSceneCommand`. Pour une suppression définitive, il faudrait appeler `Destroy()` directement dans votre scénario.

---

## 6. Sélection

### SelectAllCommand
**Sélectionne tous les objets actifs de la scène** (alimente `LastObjects` pour la coréférence suivante).

| Phrase | Résultat attendu |
|---|---|
| `« Sélectionne tout. »` | Tous les objets actifs sont sélectionnés. |
| `« Tout sélectionner. »` | Idem. |
| `« Sélectionne tous les objets. »` | Idem. |

> 💡 Utile avant d'enchaîner une action globale : `« Sélectionne tout. »` → `« Cache-les. »`

---

### InvertSelectionCommand
**Inverse la sélection courante** (sélectionne ce qui n'était pas sélectionné).

| Phrase | Résultat attendu |
|---|---|
| `« Inverse la sélection. »` | Les objets non sélectionnés deviennent sélectionnés. |
| `« Inversion. »` | Idem. |

> ⚠️ Requiert une sélection préalable dans `LastObjects`. Sans sélection précédente, sélectionne tous les objets.

---

### SelectCommand *(non implémenté)*
> ⚠️ La logique de sélection Unity n'est pas encore implémentée (`Execute()` lève une `NotImplementedException`). Les triggers sont déclarés pour préparer la grammaire Vosk.

---

### UnselectCommand *(non implémenté)*
> ⚠️ Même statut que `SelectCommand`.

---

## 7. Navigation caméra

### FocusCommand
**Oriente la caméra principale vers les objets sélectionnés** et se positionne à une distance adaptée.

| Phrase | Résultat attendu |
|---|---|
| `« Focus sur les pommes. »` | La caméra se positionne face aux pommes. |
| `« Regarde les objets rouges. »` | La caméra pointe vers les objets rouges. |
| `« 👆 Zoom sur ça. »` | La caméra se focalise sur l'objet pointé. |
| `« Focalise sur les citrouilles. »` | La caméra se centre sur les citrouilles. |

> ⚠️ **En VR** : `Camera.main` est généralement contrôlée par le SDK XR (OpenXR, OVR…). Le déplacement peut être ignoré si la caméra fait partie d'un XR Rig. Dans ce cas, une implémentation spécifique à votre SDK de VR est nécessaire.

---

## 8. Information

### MeasureCommand *(existant)*
**Mesure la distance** entre deux points ou objets.

| Phrase | Résultat attendu |
|---|---|
| `« Mesure la distance entre la pomme et la citrouille. »` | La distance est affichée dans la console. |
| `« Calcule la distance. »` | Mesure entre les deux PointParameters détectés. |
| `« Quelle est la distance ? »` | Idem. |

---

### DescribeCommand
**Affiche les propriétés** de l'objet dans la console Unity (position, rotation, taille, couleur, état actif).

| Phrase | Résultat attendu |
|---|---|
| `« Décris les pommes. »` | Log Unity : UUID, position, rotation, scale, couleur pour chaque pomme. |
| `« 👆 Infos sur ça. »` | Propriétés de l'objet pointé dans la console. |
| `« Propriétés des objets rouges. »` | Log des propriétés des objets rouges. |
| `« C'est quoi ? »` *(en pointant)* | Informations sur l'objet pointé. |

---

### CountCommand
**Compte les objets** correspondant au filtre et affiche le résultat dans la console.

| Phrase | Résultat attendu |
|---|---|
| `« Combien de pommes ? »` | Log : `[Count] 3 objet(s) trouvé(s).` |
| `« Compte les objets rouges. »` | Log : nombre d'objets rouges. |
| `« Nombre de citrouilles. »` | Log : nombre de citrouilles. |

---

## 9. Historique et état de la scène

### UndoCommand
**Annule la dernière action** enregistrée dans `CommandHistory`.

Commandes annulables : `RotateLeft`, `RotateRight`, `Flip`, `ResetScale`, `ResetTransform`, `SnapToGround`, `Align`, `SetTransparent`, `SetOpaque`, `ResetColor`, `Highlight`, `Delete`.

| Phrase | Résultat attendu |
|---|---|
| `« Annule. »` | La dernière action est annulée. |
| `« Undo. »` | Idem. |
| `« Défais ça. »` | La dernière modification est inversée. |
| `« Annule. »` *(plusieurs fois)* | Remonte l'historique action par action. |

> ⚠️ Les commandes `Move`, `Colorize`, `ScaleUp`, `ScaleDown`, `Hide`, `Show` (commandes existantes) ne sont **pas encore enregistrées** dans `CommandHistory`. Seules les nouvelles commandes le sont.

---

### RedoCommand
**Rétablit la dernière action annulée.**

| Phrase | Résultat attendu |
|---|---|
| `« Rétablis. »` | La dernière annulation est refaite. |
| `« Redo. »` | Idem. |
| `« Refais. »` | La modification est réappliquée. |

---

### ResetSceneCommand
**Remet toute la scène à son état initial** : positions, rotations, tailles, couleurs, objets supprimés réactivés. Vide également l'historique undo/redo.

| Phrase | Résultat attendu |
|---|---|
| `« Réinitialise la scène. »` | Tous les objets retrouvent leur état de départ. |
| `« Reset la scène. »` | Idem. |
| `« Remet tout. »` | Scène entièrement restaurée. |
| `« Restaure la scène. »` | Idem. |

---

## 10. Tableau récapitulatif

| Commande | Catégorie | Undo | Paramètre RuleBased | Mode |
|---|---|---|---|---|
| `HideCommand` | Visibilité | — | SelectionParameter | RB + LLM |
| `ShowCommand` | Visibilité | — | SelectionParameter | RB + LLM |
| `SetTransparentCommand` | Visibilité | ✅ | SelectionParameter | RB + LLM |
| `SetOpaqueCommand` | Visibilité | ✅ | SelectionParameter | RB + LLM |
| `HighlightCommand` | Visibilité | ✅ | SelectionParameter | RB + LLM |
| `ScaleUpCommand` | Taille | — | SelectionParameter | RB + LLM |
| `ScaleDownCommand` | Taille | — | SelectionParameter | RB + LLM |
| `ResetScaleCommand` | Taille | ✅ | SelectionParameter | RB + LLM |
| `RotateRightCommand` | Transform | ✅ | SelectionParameter | RB + LLM |
| `RotateLeftCommand` | Transform | ✅ | SelectionParameter | RB + LLM |
| `FlipCommand` | Transform | ✅ | SelectionParameter | RB + LLM |
| `MoveCommand` | Transform | — | SelectionParameter + PointParameter | RB + LLM |
| `SnapToGroundCommand` | Transform | ✅ | SelectionParameter | RB + LLM |
| `AlignCommand` | Transform | ✅ | SelectionParameter | RB + LLM |
| `ResetTransformCommand` | Transform | ✅ | SelectionParameter | RB + LLM |
| `ColorizeCommand` | Couleur | — | ColorParameter + SelectionParameter | RB + LLM |
| `ColorizeDarkerCommand` | Couleur | — | SelectionParameter | RB + LLM |
| `ColorizeLighterCommand` | Couleur | — | SelectionParameter | RB + LLM |
| `ColorizeCopyCommand` | Couleur | — | SelectionParameter | RB + LLM |
| `ResetColorCommand` | Couleur | ✅ | SelectionParameter | RB + LLM |
| `GrabCommand` | Manipulation | — | SelectionParameter + PointParameter | RB + LLM |
| `ReleaseCommand` | Manipulation | — | SelectionParameter | RB + LLM |
| `DuplicateCommand` | Manipulation | — | SelectionParameter | RB + LLM |
| `DeleteCommand` | Manipulation | ✅ | SelectionParameter | RB + LLM |
| `SelectCommand` | Sélection | — | SelectionParameter | RB + LLM |
| `UnselectCommand` | Sélection | — | SelectionParameter | RB + LLM |
| `SelectAllCommand` | Sélection | — | *(aucun)* | RB + LLM |
| `InvertSelectionCommand` | Sélection | — | *(aucun)* | RB + LLM |
| `FocusCommand` | Caméra | — | SelectionParameter | RB + LLM |
| `MeasureCommand` | Information | — | PointParameter ×2 | RB + LLM |
| `DescribeCommand` | Information | — | SelectionParameter | RB + LLM |
| `CountCommand` | Information | — | SelectionParameter | RB + LLM |
| `UndoCommand` | Historique | — | *(aucun)* | RB + LLM |
| `RedoCommand` | Historique | — | *(aucun)* | RB + LLM |
| `ResetSceneCommand` | Historique | — | *(aucun)* | RB + LLM |

*RB = RuleBased, LLM = OpenAI / serveur local*

---

## Scénarios de test enchaînés

Ces séquences permettent de valider plusieurs commandes en interaction.

### Scénario 1 — Manipulation de base
```
1. « Agrandis les pommes. »                       → ScaleUp
2. « Colorie-les en rouge. »                      → Colorize (coréférence)
3. « 👆 Déplace ça ici. »                         → Move (vers pointeur)
4. « Duplique-les. »                              → Duplicate (coréférence)
5. « Annule. »                                    → Undo (annule Duplicate)
6. « Réinitialise les pommes. »                   → ResetTransform
```

### Scénario 2 — Visibilité et transparence
```
1. « Cache les objets bleus. »                    → Hide
2. « Rends les pommes transparentes. »            → SetTransparent
3. « Surligne les citrouilles. »                  → Highlight
4. « Montre les objets bleus. »                   → Show
5. « Rends les pommes opaques. »                  → SetOpaque
6. « Annule. »                                    → Undo (annule SetOpaque)
```

### Scénario 3 — Sélection globale
```
1. « Sélectionne tout. »                          → SelectAll
2. « Cache-les. »                                 → Hide (coréférence → tous les objets)
3. « Inverse la sélection. »                      → InvertSelection (0 objets → sélectionne tout)
4. « Montre-les. »                                → Show
5. « Réinitialise la scène. »                     → ResetScene
```

### Scénario 4 — Rotation et alignement
```
1. « Tourne les pommes à droite. »                → RotateRight
2. « Tourne-les encore à droite. »                → RotateRight (coréférence, +45°)
3. « Retourne les citrouilles. »                  → Flip (180°)
4. « Aligne les pommes. »                         → Align (même hauteur Y)
5. « Annule. »                                    → Undo (annule Align)
6. « Annule. »                                    → Undo (annule Flip)
```

### Scénario 5 — Suppression et récupération
```
1. « Supprime les pommes. »                       → Delete
2. « Compte les objets. »                         → Count (pommes absentes)
3. « Annule. »                                    → Undo → pommes réapparaissent
4. « Compte les pommes. »                         → Count (pommes présentes)
5. « Réinitialise la scène. »                     → ResetScene (état initial complet)
```
