# Implémenter une nouvelle commande multimodale

Ce guide explique comment ajouter une nouvelle commande vocale au système SC4VE/SVEN, reconnue à la fois par le moteur **RuleBased** (sans LLM) et le moteur **LLM** (OpenAI / serveur local).

---

## Sommaire

1. [Architecture du pipeline](#1-architecture-du-pipeline)
2. [Étape 1 — Créer la classe Command](#2-étape-1--créer-la-classe-command)
3. [Étape 2 — Mode RuleBased : ajouter les déclencheurs](#3-étape-2--mode-rulebased--ajouter-les-déclencheurs)
4. [Étape 3 — Mode LLM : rien à faire](#4-étape-3--mode-llm--rien-à-faire)
5. [Référence des paramètres](#5-référence-des-paramètres)
6. [Exemple complet : RotateCommand](#6-exemple-complet--rotatecommand)

---

## 1. Architecture du pipeline

```
Microphone
    ↓
BaseSpeechToText (Whisper ou Vosk)
    ↓  OnTranscriptionResult(json)
MultimodalityController
    ↓
[Mode RuleBased]                    [Mode LLM]
RuleBasedIntentRecognizer           OpenAI / serveur local
    ↓                                   ↓
    └──────────── commandJson ──────────┘
                      ↓
              DeserializeCommand()
              CommandConverter → réflexion sur le nom de type
                      ↓
         CommandToGraphOutputCommandAsync()
         Semanticize() → graphe RDF SVEN
                      ↓
               ResolveCommands()
               Command.Execute()
                      ↓
               Objets Unity / SVEN
```

Une commande est une classe C# héritant de `Command`. Elle décrit son intention via `CommandDescriptionAttribute`, est instanciée depuis le JSON par réflexion, et implémente `Execute()` pour manipuler les objets Unity/SVEN.

Le retour de `Execute()` alimente `Command.LastObjects`, qui permet la **coréférence** : l'utilisateur peut dire « agrandis-les » dans la phrase suivante pour désigner les mêmes objets.

---

## 2. Étape 1 — Créer la classe Command

**Emplacement :** `Assets/Scripts/SC4VE/Intent/Command/YourCommand.cs`

```csharp
using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [Serializable, CommandDescription(
        "Description claire de la commande et du contexte dans lequel la générer. " +
        "Paramètres: SelectionParameter (les objets cibles).")]
    public class YourCommand : Command
    {
        // GetParameter<T>()  → 1er paramètre de type T dans la liste Parameters
        // GetParameter<T>(2) → 2e paramètre de type T
        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter?.Objects ?? new();
            foreach (SemantizationCore obj in objects)
            {
                // Votre logique ici
                Debug.Log($"[YourCommand] Exécuté sur {obj.GetUUID()}");
            }
            // Retourner les objets affectés pour alimenter Command.LastObjects
            return objects;
        }
    }
}
```

**Règles importantes :**
- `[Serializable]` est **obligatoire** — sans lui, la désérialisation JSON échoue silencieusement.
- `CommandDescriptionAttribute` est utilisé par le LLM pour savoir quand et comment générer cette commande. La description doit mentionner explicitement les paramètres attendus.
- La classe est découverte automatiquement par réflexion : aucun enregistrement manuel n'est nécessaire.

---

## 3. Étape 2 — Mode RuleBased : déclarer les déclencheurs

### 3.1 — Ajouter l'attribut `[RuleBasedTriggers]` sur la classe

```csharp
[RuleBasedTriggers("tourne", "tourner", "pivote", "pivoter",
                   "fais tourner", "faire tourner", "fais pivoter", "faire pivoter")]
[Serializable, CommandDescription("...")]
public class RotateCommand : Command { ... }
```

C'est **tout ce qui est nécessaire** pour une commande simple (SelectionParameter par défaut).  
Le `RuleBasedIntentRecognizer` découvre automatiquement les triggers par réflexion via `RuleBasedTriggersAttribute.GetAllMappings()` — aucun enregistrement manuel dans un fichier central.

**Règle de priorité :** tous les triggers de toutes les commandes sont triés par longueur décroissante avant la comparaison — les phrases multi-mots passent toujours avant les mots courts. Aucun ordre d'enregistrement à respecter.

Le moteur tente d'abord une correspondance exacte de sous-chaîne (multi-mots), puis une comparaison de racines (stemming français) pour les mots seuls.

### 3.2 — Surcharger `BuildRuleBasedParameters` (si nécessaire)

Par défaut, `Command.BuildRuleBasedParameters` retourne un `SelectionParameter` standard. Il faut surcharger **uniquement** si la commande a besoin d'autres paramètres :

```csharp
// Exemple : commande qui n'a pas besoin de SelectionParameter
public override List<Parameter> BuildRuleBasedParameters(RuleBasedContext ctx)
    => new List<Parameter>();

// Exemple : MoveCommand — sélection source + point destination
public override List<Parameter> BuildRuleBasedParameters(RuleBasedContext ctx)
    => new List<Parameter>
    {
        ctx.BuildSelectionParameter(useStartedAt: true),
        ctx.BuildDestinationParameter()
    };

// Exemple : ColorizeCommand — couleur cible + sélection filtrée par couleur source
public override List<Parameter> BuildRuleBasedParameters(RuleBasedContext ctx)
{
    var ps = new List<Parameter>();
    if (ctx.TargetColors.Count > 0)
        ps.Add(new ColorParameter { Type = "ColorParameter",
                                    Value = ctx.TargetColors[0].Value,
                                    Timestamp = ctx.TargetColors[0].Timestamp });
    ps.Add(ctx.BuildSelectionParameter());
    return ps;
}
```

**Helpers disponibles dans `RuleBasedContext` :**

| Méthode | Retourne | Usage |
|---|---|---|
| `BuildSelectionParameter(useStartedAt)` | `SelectionParameter` | Sélection standard (annotations, couleurs source, déictiques, coréférence) |
| `BuildDestinationParameter()` | `PointParameter` | Point de destination (fin de phrase + délai) — pour MoveCommand |
| `BuildGrabPointParameter()` | `PointParameter` | Timestamp du premier déictique ou fin de phrase — pour GrabCommand |

**Modèles selon le type de commande :**

| Commande | Paramètres à construire | Exemple existant |
|----------|------------------------|------------------|
| Action simple sur des objets | *(défaut)* `SelectionParameter` | `ScaleUpCommand`, `HideCommand` |
| Colorisation | `ColorParameter` + `SelectionParameter` | `ColorizeCommand` |
| Déplacement | `SelectionParameter(useStartedAt: true)` + `BuildDestinationParameter()` | `MoveCommand` |
| Saisie | `SelectionParameter` + `BuildGrabPointParameter()` | `GrabCommand` |
| Sans paramètre | `new List<Parameter>()` | `UndoCommand`, `SelectAllCommand` |

---

## 4. Étape 3 — Mode LLM : rien à faire

Le LLM reçoit dynamiquement la liste de toutes les commandes disponibles via :

```csharp
CommandDescriptionAttribute.GetAvailableCommandsString()
// → "- RotateCommand: Fait pivoter / tourne les objets..."
```

Le prompt système injecte cette liste automatiquement. La seule chose qui compte est la **qualité de la description** dans `CommandDescriptionAttribute` : elle doit indiquer clairement quand générer la commande et quels paramètres inclure.

**Conseils pour la description :**
```
"Fait pivoter des objets de 45°. " +
"Paramètres: SelectionParameter (les objets à faire pivoter, identifiés par annotation, " +
"couleur ou pointage déictique)."
```

---

## 5. Référence des paramètres

### Types de paramètres disponibles

| Type C# | Clé JSON | Description |
|---------|----------|-------------|
| `SelectionParameter` | `"SelectionParameter"` | Sélectionne des objets du graphe RDF via des filtres |
| `ColorParameter` | `"ColorParameter"` | Couleur cible (nom de couleur ontologique, ex: `"Rouge"`) |
| `PointParameter` | `"PointParameter"` | Position 3D récupérée dans le graphe via un timestamp |
| `SentenceParameter` | `"SentenceParameter"` | Phrase texte (pour `SpeechCommand`) |

### Récupérer un paramètre dans `Execute()`

```csharp
// Sélection d'objets (1er SelectionParameter)
private SelectionParameter Selection   => GetParameter<SelectionParameter>();
// 2e SelectionParameter (ex: sélection destination pour un MoveCommand annotation-based)
private SelectionParameter Destination => GetParameter<SelectionParameter>(2);
// Couleur cible
private ColorParameter     Color       => GetParameter<ColorParameter>();
// Point 3D
private PointParameter     Point       => GetParameter<PointParameter>();
// Texte à prononcer
private SentenceParameter  Sentence    => GetParameter<SentenceParameter>();
```

### Filtres de `SelectionParameter`

| Type de filtre | Valeur | Sélectionne |
|---------------|--------|-------------|
| `"Annotation"` | Nom ontologique (ex: `"Pomme"`) | Objets de ce type au timestamp donné |
| `"Color"` | Nom de couleur (ex: `"Rouge"`) | Objets ayant cette couleur au timestamp |
| `"Event"` | `"Pointeur"` ou `"Caméra"` | Objets en collision avec le composant déictique au timestamp |
| `"Coreference"` | *(aucune)* | Objets de la commande précédente (`Command.LastObjects`) |

Les filtres peuvent être combinés avec les opérateurs `"AND"` et `"OR"` dans le tableau `filters`.

### `useStartedAt` dans `BuildSelectionParameter`

| Valeur | Timestamp utilisé | Quand l'utiliser |
|--------|------------------|------------------|
| `false` | `EndedAt` du mot pertinent | Commandes standards (sélection après avoir prononcé le mot) |
| `true` | `StartedAt` du mot pertinent | MoveCommand source — l'utilisateur pointait AVANT de parler |

---

## 6. Exemple complet : RotateCommand

Fait pivoter de 45° les objets sélectionnés autour de l'axe Y.

### `RotateCommand.cs`

```csharp
using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [Serializable, CommandDescription(
        "Fait pivoter / tourne les objets de 45° autour de l'axe Y. " +
        "Paramètres: SelectionParameter (les objets à faire pivoter, identifiés " +
        "par annotation, couleur ou pointage déictique).")]
    public class RotateCommand : Command
    {
        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter?.Objects ?? new();
            foreach (SemantizationCore obj in objects)
            {
                obj.transform.Rotate(Vector3.up, 45f);
                Debug.Log($"[RotateCommand] {obj.GetUUID()} pivoté de 45°");
            }
            return objects;
        }
    }
}
```

### Ajout dans `ActionMappings` (RuleBasedIntentRecognizer.cs)

```csharp
(new[] { "tourne", "tourner", "pivote", "pivoter",
          "fais tourner", "faire tourner", "fais pivoter", "faire pivoter" },
    "RotateCommand"),
```

### Ajout dans `BuildCommands` (RuleBasedIntentRecognizer.cs)

```csharp
case "RotateCommand":
{
    SelectionParameter selParam = BuildSelectionParameter(
        annotations, colors.Where(c => !c.IsTarget).ToList(),
        deictics, hasCoreference, limit, useStartedAt: false);
    Command cmd = CreateCommand("RotateCommand");
    cmd.Parameters = new List<Parameter> { selParam };
    commands.Add(cmd);
    break;
}
```

### Phrases reconnues

```
« Tourne la pomme rouge. »
→ RotateCommand(SelectionParameter[Annotation=Pomme, Color=Rouge])

« Tourne ça. »
→ RotateCommand(SelectionParameter[Event=Pointeur, timestamp=début de phrase])

« Fais pivoter les citrouilles. »
→ RotateCommand(SelectionParameter[Annotation=Citrouille])

« Tourne les. »
→ RotateCommand(SelectionParameter[Coreference])
```

---

## Checklist rapide

- [ ] Fichier `YourCommand.cs` dans `Assets/Scripts/SC4VE/Intent/Command/`
- [ ] `[Serializable]` sur la classe
- [ ] `[CommandDescription("...")]` avec description des paramètres
- [ ] `Execute()` retourne la liste des objets affectés
- [ ] (Mode RuleBased) Entrée dans `ActionMappings`
- [ ] (Mode RuleBased) `case "YourCommand":` dans `BuildCommands`
