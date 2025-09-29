# SVENMultimodality

## Création

« Crée une pomme rouge. »
→ CreateCommand(Apple) + ColorizeCommand(Red)

« Génère une banane jaune. »
→ CreateCommand(Banana) + ColorizeCommand(Yellow)

« Ajoute une carotte verte. »
→ CreateCommand(Carrot) + ColorizeCommand(Green)

« Fais apparaître une citrouille rose. »
→ CreateCommand(Pumpkin) + ColorizeCommand(Pink)

« Crée un spray bleu. »
→ CreateCommand(Spray) + ColorizeCommand(Blue)

## Colorier

« Met la citrouille en rouge. »
→ SelectCommand(Pumpkin) + SelectCommand(Pointer) + ColorizeCommand(Red)

« Colorie les pommes en bleu. »
→ SelectCommand(Apple) + SelectCommand(Pointer) + ColorizeCommand(Blue)

« Peins les légumes en vert. »
→ SelectCommand(Vegetable) + SelectCommand(Pointer) + ColorizeCommand(Green)

## Suppression

« Détruis la citrouille. »
→ SelectCommand(Pumpkin) + SelectCommand(Pointer) + DestroyCommand()

« Supprime toutes les pommes. »
→ SelectCommand(Apple) + DestroyCommand()

« Efface les légumes. »
→ SelectCommand(Vegetable) + DestroyCommand()

## Masquer / Afficher

« Cache la banane. »
→ SelectCommand(Banana) + SelectCommand(Pointer) + HideCommand()

« Montre la citrouille. »
→ SelectCommand(Pumpkin) + SelectCommand(Pointer) + ShowCommand()

« Affiche les fruits. »
→ SelectCommand(Fruit) + ShowCommand()

## Redimensionnement

« Agrandis la pomme. »
→ SelectCommand(Apple) + SelectCommand(Pointer) + ScaleUpCommand()

« Rétrécis la carotte. »
→ SelectCommand(Carrot) + SelectCommand(Pointer) + ScaleDownCommand()

« Grossis toutes les citrouilles. »
→ SelectCommand(Pumpkin) + ScaleUpCommand()

## Mouvement

« Déplace la banane ici. »
→ SelectCommand(Banana) + SelectCommand(Pointer) + MoveCommand(Pointer)

« Bouge la citrouille vers ma vision. »
→ SelectCommand(Pumpkin) + MoveCommand(PointOfView)

## Sélection / Désélection

« Sélectionne la pomme rouge. »
→ SelectCommand(Apple) + SelectCommand(Pointer) + SelectCommand(Red)

« Sélectionne les pommes rouges. »
→ SelectCommand(Apple) + SelectCommand(Pointer) + SelectCommand(Red)

« Désélectionne tout. »
→ UnselectCommand(All)

## Événement / Sauvegarde

« Sauvegarde la scène. »
→ EventCommand(SaveAndQuitGraph)

« Mets en pause. »
→ EventCommand(Pause)

---

à prompt :
J'aimerai en deuxième paramètre de chaque commande, le mot qui a permis de trouver la commande :
Par exemple, Crée une pomme rouge. -> CreateCommand(Apple("pomme"), "Crée") + ColorizeCommand(Red("rouge"), null)

j'aimerai aussi que tu raojoute des exemples de ceci dans le csv :

## Colorier

« Met la citrouille en rouge. »
→ SelectCommand(Pumpkin) + SelectCommand(Pointer) + ColorizeCommand(Red)

« Colorie les pommes en bleu. »
→ SelectCommand(Apple) + SelectCommand(Pointer) + ColorizeCommand(Blue)

« Peins les légumes en vert. »
→ SelectCommand(Vegetable) + SelectCommand(Pointer) + ColorizeCommand(Green)
