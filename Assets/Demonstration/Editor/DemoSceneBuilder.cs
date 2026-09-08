using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sc4ve.Multimodality;
using Sven.Content;
using Unity.AI.Navigation;
using UnityEngine.AI;
using Sven.Context;
using Sven.GraphManagement;
using Sven.Multimodality;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Sc4ve.Demonstration.EditorTools
{
    /// <summary>
    /// Construit le contenu du mini-jeu de service par code, dans la scène « Demo Mini Game ».
    ///
    /// Pourquoi un script plutôt qu'une scène montée à la main : le contenu est reproductible,
    /// relisible en diff, et se reconstruit quand le vocabulaire change.
    ///
    /// L'outil ne possède QU'UN SEUL objet racine (RootName) : il le détruit et le reconstruit
    /// à chaque exécution, et ne touche à rien d'autre. Tout ce que l'on ajoute à la scène en
    /// dehors de cette racine — rig XR, MultimodalityController, éclairage, NavMesh —
    /// survit aux reconstructions.
    ///
    /// Voir Assets/Demonstration/README.md, lots 0 et 1.
    /// </summary>
    public static class DemoSceneBuilder
    {
        private const string ScenePath = "Assets/Demonstration/Scenes/Demo Mini Game.unity";
        private const string MaterialsPath = "Assets/Demonstration/Materials";
        private const string PrefabsPath   = "Assets/Demonstration/Prefabs";

        /// <summary>Le seul objet que cet outil possède. Tout le reste de la scène lui est étranger.</summary>
        private const string RootName = "Mini-jeu (généré)";

        private const string ExpositionPath = "Assets/Demonstration/Scenes/Demo Exposition.unity";
        private const string ExpositionRootName = "Exposition (généré)";

        // Aire de jeu debout : tout doit être atteignable sans locomotion (§12 du README).
        private const float CounterY = 0.90f;
        private const float ShelfY = 1.25f;

        #region Menu

        [MenuItem("SC4VE/Démonstration/1 — (Re)construire le contenu du mini-jeu", priority = 1)]
        public static void BuildScene()
        {
            Scene scene = EditorSceneManager.GetActiveScene();

            if (scene.path != ScenePath)
            {
                if (!EditorUtility.DisplayDialog(
                        "Ouvrir la scène du mini-jeu",
                        $"La scène active n'est pas celle du mini-jeu.\n\n" +
                        $"Ouvrir {ScenePath} ?\n" +
                        "(les modifications non enregistrées de la scène courante seront proposées à l'enregistrement)",
                        "Ouvrir", "Annuler"))
                    return;

                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            if (!EditorUtility.DisplayDialog(
                    "(Re)construire le contenu du mini-jeu",
                    $"L'objet « {RootName} » va être détruit puis reconstruit.\n\n" +
                    "Le reste de la scène n'est pas touché : rig XR, contrôleur multimodal, " +
                    "éclairage et NavMesh survivent.\n\n" +
                    "Ne rien placer à la main SOUS cette racine.",
                    "Construire", "Annuler"))
                return;

            // D'abord les prefabs : la scène en instancie, et ils resteraient en Static.
            int patched = PatchPrefabs();
            if (patched > 0) Debug.Log($"[DemoSceneBuilder] {patched} prefab(s) corrigé(s) avant construction.");

            Transform root = ResetRoot(scene);

            BuildEnvironment(root);
            BuildKitchen(root);
            BuildDiningRoom(root);
            BuildOrderBoard(root);
            bool rigCreated = EnsureXRRig();
            // Hors du bloc ci-dessus : la caméra parasite doit aussi disparaître des scènes
            // où le rig existait déjà avant cette version de l'outil.
            RemoveStrayMainCamera(FindRigRoot());
            EnsureGraphController();
            EnsureListeningTimeScale();
            BakeNavMesh(root);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.Refresh();

            Debug.Log($"[DemoSceneBuilder] Contenu reconstruit sous « {RootName} » dans {ScenePath}.");
            EditorUtility.DisplayDialog("Terminé",
                $"Contenu reconstruit sous « {RootName} ».\n\n" +
                (rigCreated
                    ? "Rig XR complet mis en place (contrôleurs + Pointer + PointOfView), " +
                      "locomotion désactivée.\n\n"
                    : "Rig XR déjà utilisable, laissé tel quel ; interactors SVEN vérifiés.\n\n") +
                "4 clients installés (couples famille+contrainte fixes), tableau des " +
                "commandes au-dessus de la passe, NavMesh cuit.\n\n" +
                "Reste à faire à la main :\n" +
                "• ajouter le MultimodalityController et le pipeline vocal",
                "OK");
        }

        /// <summary>
        /// Détruit la racine générée si elle existe, et en recrée une vide.
        /// C'est ce qui rend l'outil rejouable sans détruire le reste de la scène.
        /// </summary>
        private static Transform ResetRoot(Scene scene)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
                if (go.name == RootName)
                    UnityEngine.Object.DestroyImmediate(go);

            return new GameObject(RootName).transform;
        }

        [MenuItem("SC4VE/Démonstration/4 — Peupler la scène d'exposition", priority = 4)]
        public static void BuildExposition()
        {
            Scene scene = EditorSceneManager.GetActiveScene();

            if (scene.path != ExpositionPath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ExpositionPath, OpenSceneMode.Single);
            }

            foreach (GameObject go in scene.GetRootGameObjects())
                if (go.name == ExpositionRootName)
                    UnityEngine.Object.DestroyImmediate(go);

            var root = new GameObject(ExpositionRootName).transform;
            BuildExpositionContent(root);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.Refresh();

            Debug.Log($"[DemoSceneBuilder] Exposition peuplée dans {ExpositionPath}.");
        }

        [MenuItem("SC4VE/Démonstration/2 — Corriger les prefabs existants", priority = 2)]
        public static void PatchExistingPrefabs()
        {
            int patched = PatchPrefabs();
            EditorUtility.DisplayDialog("Terminé", $"{patched} prefab(s) corrigé(s).", "OK");
        }

        /// <summary>
        /// Passe les prefabs manipulables existants en Dynamic et les rend saisissables en VR.
        /// Idempotent : relancer ne fait rien de plus.
        /// </summary>
        private static int PatchPrefabs()
        {
            string[] names =
            {
                "Interactable Apple", "Interactable Banana", "Interactable Carrot",
                "Interactable Pumpkin_A", "Interactable Pumpkin_B", "Interactable Pumpkin_C",
                "Interactable Pumpkin_D", "Interactable Pumpkin_E", "Interactable Pumpkin_F",
                "Interactable Pumpkin_G",
            };

            int patched = 0;
            foreach (string name in names)
            {
                string path = $"Assets/Resources/Prefabs/{name}.prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    Debug.LogWarning($"[DemoSceneBuilder] Prefab introuvable : {path}");
                    continue;
                }

                GameObject root = PrefabUtility.LoadPrefabContents(path);
                bool changed = MakeAnnotatorDynamic(root) | MakeGrabbable(root);
                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    patched++;
                    Debug.Log($"[DemoSceneBuilder] Prefab corrigé : {name}");
                }
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            return patched;
        }

        #endregion

        #region Correctifs

        /// <summary>
        /// Passe le SemanticAnnotator en Dynamic. Sans ça, l'annotation est figée au
        /// démarrage et un changement d'état (« cuit ») n'atteint jamais le graphe —
        /// le plat serait jugé non conforme sans raison visible (§6.2 du README).
        /// </summary>
        private static bool MakeAnnotatorDynamic(GameObject root)
        {
            var core = root.GetComponent<SemantizationCore>();
            var annotator = root.GetComponent<SemanticAnnotator>();
            if (core == null || annotator == null) return false;

            SemanticComponent entry = core.componentsToSemanticize
                .FirstOrDefault(c => c != null && c.Component == annotator);

            if (entry == null)
            {
                core.componentsToSemanticize.Add(new SemanticComponent
                {
                    Component = annotator,
                    ProcessingMode = SemanticProcessingMode.Dynamic
                });
                return true;
            }

            if (entry.ProcessingMode == SemanticProcessingMode.Dynamic) return false;
            entry.ProcessingMode = SemanticProcessingMode.Dynamic;
            return true;
        }

        /// <summary>Ajoute de quoi saisir l'objet en VR — sans ça GrabCommand ne trouve rien.</summary>
        private static bool MakeGrabbable(GameObject root)
        {
            if (root.GetComponentInChildren<XRGrabInteractable>() != null) return false;
            if (root.GetComponentInChildren<Collider>() == null) return false;

            var grab = root.AddComponent<XRGrabInteractable>();
            grab.useDynamicAttach = true;
            return true;
        }

        #endregion

        #region Environnement

        private static void BuildEnvironment(Transform root)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Sol";
            floor.transform.SetParent(root);
            floor.transform.localScale = new Vector3(2f, 1f, 2f);
            floor.GetComponent<Renderer>().sharedMaterial = GetMaterial("Sol", new Color(0.32f, 0.30f, 0.28f));
            floor.isStatic = true;
        }

        #endregion

        #region Cuisine

        private static void BuildKitchen(Transform root)
        {
            var kitchen = new GameObject("Cuisine").transform;
            kitchen.SetParent(root);

            Box(kitchen, "Plan de travail", new Vector3(0f, CounterY - 0.05f, 0.70f),
                new Vector3(2.6f, 0.10f, 0.70f), new Color(0.55f, 0.52f, 0.48f));
            Box(kitchen, "Étagère", new Vector3(0f, ShelfY - 0.04f, 1.15f),
                new Vector3(2.6f, 0.08f, 0.35f), new Color(0.48f, 0.45f, 0.42f));

            BuildCrates(kitchen);
            BuildStations(kitchen);
            BuildPlates(kitchen);

            RestOnSurface(Prop(kitchen, "Poubelle", "sven:Bin",
                new Vector3(1.35f, 0f, 0.95f), height: 0.72f), 0f);

            Box(kitchen, "Passe", new Vector3(0f, CounterY - 0.05f, 1.85f),
                new Vector3(2.0f, 0.10f, 0.45f), new Color(0.62f, 0.58f, 0.50f));
        }

        /// <summary>
        /// Les 11 ingrédients du §6.1. Deux exemplaires de chacun : c'est le minimum
        /// pour que deux objets strictement identiques coexistent, sans quoi la
        /// clarification (« laquelle ? ») ne se déclenche jamais (§3 du README).
        /// </summary>
        private static void BuildCrates(Transform parent)
        {
            var crates = new GameObject("Ingrédients").transform;
            crates.SetParent(parent);

            const int copies = 2;
            float step = 2.4f / Ingredients.Length;
            float x0 = -1.2f + step * 0.5f;

            for (int i = 0; i < Ingredients.Length; i++)
            {
                Ingredient ingredient = Ingredients[i];
                GameObject prefab = EnsurePrefab(ingredient);
                if (prefab == null) continue;

                for (int c = 0; c < copies; c++)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, crates);
                    instance.name = $"{ShortName(ingredient.Semantic)} {c + 1}";
                    instance.transform.position = new Vector3(x0 + i * step, ShelfY, 1.05f + c * 0.16f);

                    // Mettre à l'échelle AVANT de poser : la hauteur à compenser dépend de la
                    // taille finale.
                    NormalizeSize(instance, ingredient.Size);
                    RestOnSurface(instance, ShelfY);
                }
            }
        }

        /// <summary>
        /// Un ingrédient du jeu. <see cref="Size"/> est sa plus grande dimension en mètres :
        /// c'est cette valeur, et non l'échelle brute, qui garantit que les onze ingrédients
        /// soient à la même échelle les uns des autres, qu'ils viennent d'un mesh existant ou
        /// d'un primitif de remplacement.
        /// </summary>
        private readonly struct Ingredient
        {
            public readonly string Semantic;
            public readonly float Size;
            public readonly PrimitiveType Shape;
            public readonly Vector3 Proportions;
            public readonly Color Color;

            /// <summary>Prefab existant, ou null s'il faut en fabriquer un.</summary>
            public readonly string ExistingPrefab;

            public Ingredient(string semantic, float size, PrimitiveType shape,
                              Vector3 proportions, Color color, string existingPrefab = null)
            {
                Semantic = semantic;
                Size = size;
                Shape = shape;
                Proportions = proportions;
                Color = color;
                ExistingPrefab = existingPrefab;
            }
        }

        /// <summary>
        /// Les onze ingrédients du §6.1, avec leur taille réelle. Les proportions donnent la
        /// forme (un steak est plat, une baguette allongée) ; la taille finale est imposée par
        /// NormalizeSize, donc changer une proportion ne change pas l'encombrement.
        /// </summary>
        private static readonly Ingredient[] Ingredients =
        {
            new("sven:Apple",   0.08f, PrimitiveType.Sphere,   new Vector3(1f, 1f, 1f),      new Color(0.80f, 0.16f, 0.16f), "Interactable Apple"),
            new("sven:Banana",  0.18f, PrimitiveType.Capsule,  new Vector3(0.35f, 1f, 0.35f), new Color(0.93f, 0.83f, 0.25f), "Interactable Banana"),
            new("sven:Carrot",  0.16f, PrimitiveType.Cylinder, new Vector3(0.25f, 1f, 0.25f), new Color(0.92f, 0.51f, 0.13f), "Interactable Carrot"),
            new("sven:Pumpkin", 0.22f, PrimitiveType.Sphere,   new Vector3(1f, 0.8f, 1f),     new Color(0.88f, 0.45f, 0.10f), "Interactable Pumpkin_C"),
            new("sven:Potato",  0.09f, PrimitiveType.Sphere,   new Vector3(1f, 0.75f, 0.8f),  new Color(0.76f, 0.60f, 0.42f)),
            new("sven:Lettuce", 0.14f, PrimitiveType.Sphere,   new Vector3(1f, 0.85f, 1f),    new Color(0.45f, 0.72f, 0.35f)),
            new("sven:Tomato",  0.07f, PrimitiveType.Sphere,   new Vector3(1f, 0.9f, 1f),     new Color(0.85f, 0.18f, 0.15f)),
            new("sven:Beef",    0.14f, PrimitiveType.Cube,     new Vector3(1f, 0.25f, 0.7f),  new Color(0.55f, 0.18f, 0.16f)),
            new("sven:Chicken", 0.12f, PrimitiveType.Capsule,  new Vector3(0.6f, 1f, 0.6f),   new Color(0.93f, 0.85f, 0.68f)),
            new("sven:Salmon",  0.13f, PrimitiveType.Cube,     new Vector3(1f, 0.18f, 0.5f),  new Color(0.95f, 0.55f, 0.42f)),
            new("sven:Cheese",  0.10f, PrimitiveType.Cube,     new Vector3(1f, 0.5f, 0.9f),   new Color(0.97f, 0.83f, 0.35f)),
            new("sven:Bread",   0.20f, PrimitiveType.Capsule,  new Vector3(0.4f, 1f, 0.4f),   new Color(0.80f, 0.62f, 0.36f)),
        };

        /// <summary>
        /// Le prefab d'un ingrédient : celui qui existe déjà, ou un prefab fabriqué et enregistré
        /// dans le dossier de la démonstration.
        ///
        /// Tous les ingrédients doivent être des prefabs et pas de simples objets de scène :
        /// c'est ce qui permet de corriger un ingrédient une fois pour toutes, et de remplacer
        /// un primitif par un vrai modèle sans toucher au script.
        /// </summary>
        private static GameObject EnsurePrefab(Ingredient ingredient)
        {
            if (ingredient.ExistingPrefab != null)
            {
                string existingPath = $"Assets/Resources/Prefabs/{ingredient.ExistingPrefab}.prefab";
                GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(existingPath);

                if (existing != null)
                {
                    // Les fruits à mesh importé ont besoin du modèle découpé comme les autres :
                    // une pomme coupée doit ressembler à des quartiers, pas à une pomme écrasée.
                    //
                    // Le mesh est régénéré À CHAQUE FOIS, hors de toute condition. Il ne l'était
                    // qu'à l'ajout du composant, et ces quatre prefabs n'étant jamais reconstruits
                    // — contrairement à ceux du dossier de l'outil — ils ont gardé des tranches
                    // périmées bien après la correction du générateur.
                    //
                    // Comme Save réécrit l'asset SUR PLACE, la référence déjà posée sur le prefab
                    // pointe automatiquement sur la version corrigée : seul l'ajout du composant
                    // impose de réenregistrer le prefab.
                    Mesh sliced = IngredientMeshFactory.Sliced(ShortName(ingredient.Semantic),
                        ingredient.Proportions, SliceSeed(ingredient.Semantic));

                    // Le composant est réécrit à chaque fois, sans condition d'existence. Ne
                    // l'écrire qu'à l'ajout laissait ces quatre prefabs figés dans l'état de la
                    // première exécution : d'abord avec des tranches démesurées, puis sans
                    // couleur de chair. Trois symptômes différents, une seule cause — un test
                    // de fraîcheur qui se trompe.
                    GameObject contents = PrefabUtility.LoadPrefabContents(existingPath);

                    FoodStateMeshes meshes = contents.GetComponent<FoodStateMeshes>()
                                             ?? contents.AddComponent<FoodStateMeshes>();
                    meshes.SetSliced(sliced, ingredient.Color);

                    PrefabUtility.SaveAsPrefabAsset(contents, existingPath);
                    PrefabUtility.UnloadPrefabContents(contents);

                    return AssetDatabase.LoadAssetAtPath<GameObject>(existingPath);
                }

                Debug.LogWarning($"[DemoSceneBuilder] Prefab {ingredient.ExistingPrefab} introuvable : " +
                                 "un primitif de remplacement est fabriqué à la place.");
            }

            string name = ShortName(ingredient.Semantic);
            string path = $"{PrefabsPath}/{name}.prefab";

            // Les meshes d'abord : c'est le corps qui décide si un prefab déjà là est valable.
            List<IngredientMeshFactory.Part> parts = IngredientMeshFactory.Parts(name);
            Mesh mesh = parts.Count > 0 ? parts[0].Mesh : null;

            // Le prefab est TOUJOURS reconstruit.
            //
            // J'ai d'abord tenté de ne le refaire que s'il paraissait périmé — encore sur un
            // primitif, ou à court de pièces. Cette heuristique s'est trompée deux fois : elle
            // a gardé un steak à l'os parce que l'ancienne pièce et la nouvelle se comptaient
            // pareil. Un test de fraîcheur qui échoue en silence coûte plus cher que la
            // reconstruction qu'il évite.
            //
            // Ce dossier appartient donc entièrement à l'outil. Pour garder un modèle fait
            // main, le sortir d'ici et le référencer comme un prefab existant (cf. le champ
            // ExistingPrefab des ingrédients).
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                AssetDatabase.DeleteAsset(path);

            Directory.CreateDirectory(PrefabsPath);

            // Le mesh généré porte DÉJÀ sa forme : lui réappliquer les proportions l'écraserait
            // une seconde fois. Elles ne servent donc qu'au primitif de repli.
            Vector3 shape = mesh != null ? Vector3.one : ingredient.Proportions;

            GameObject temporary = Semantized(
                null, name, ingredient.Shape, Vector3.zero, shape,
                ingredient.Color, ingredient.Semantic, grabbable: true, mesh);

            // Les traits distinctifs — os, veines, pédoncule, grignes — deviennent des enfants,
            // avec leur propre couleur. C'est ce qui rend l'aliment reconnaissable ; le corps
            // seul n'est qu'un galet coloré.
            AddDistinctiveParts(temporary, parts);

            // Le modèle en tranches voyage AVEC le prefab : les meshes générés ne vivent pas
            // dans Resources, donc rien ne pourrait les retrouver par leur nom à l'exécution.
            temporary.AddComponent<FoodStateMeshes>().SetSliced(
                IngredientMeshFactory.Sliced(name, ingredient.Proportions, SliceSeed(name)),
                ingredient.Color);

            GameObject asset = PrefabUtility.SaveAsPrefabAsset(temporary, path);
            UnityEngine.Object.DestroyImmediate(temporary);

            Debug.Log($"[DemoSceneBuilder] Prefab fabriqué : {path}" +
                      (mesh != null ? "." : " (primitif de remplacement, aucun mesh généré)."));
            return asset;
        }

        /// <summary>
        /// Graine de découpe, stable pour un aliment donné et bornée à une petite valeur.
        ///
        /// Un hash brut ferait déborder l'indexation de Mathf.PerlinNoise et produirait des
        /// tranches d'un milliard d'unités. IngredientMeshFactory.Offset borne déjà la graine ;
        /// la réduire ici aussi évite de dépendre de cette protection.
        /// </summary>
        private static int SliceSeed(string name) => Mathf.Abs(name.GetHashCode() % 89);

        /// <summary>
        /// Met l'objet à sa taille réelle, mesurée sur ses Renderer plutôt que déduite de son
        /// échelle : c'est la seule façon de mettre à la même échelle un mesh importé et un
        /// primitif, dont les dimensions natives n'ont aucune raison de coïncider.
        /// </summary>
        private static void NormalizeSize(GameObject go, float targetLargestDimension)
        {
            Bounds bounds = WorldBounds(go);
            float largest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (largest <= Mathf.Epsilon) return;

            go.transform.localScale *= targetLargestDimension / largest;
        }

        /// <summary>
        /// Pose l'objet SUR une surface : son point le plus bas vient toucher <paramref name="surfaceY"/>.
        ///
        /// Calculer la hauteur à partir de la taille cible ne marche pas : cette taille est la
        /// plus GRANDE dimension, qui n'est pas la hauteur — un steak est plat, une baguette est
        /// longue. Et le pivot d'un mesh importé n'est pas nécessairement son centre. Mesurer
        /// l'englobant réel est la seule méthode qui vaille pour les onze ingrédients à la fois.
        /// </summary>
        private static void RestOnSurface(GameObject go, float surfaceY)
        {
            Bounds bounds = WorldBounds(go);
            if (bounds.size == Vector3.zero) return;

            go.transform.position += new Vector3(0f, surfaceY - bounds.min.y, 0f);
        }

        /// <summary>Englobant monde de tous les Renderer de l'objet, enfants compris.</summary>
        private static Bounds WorldBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static void BuildStations(Transform parent)
        {
            var stations = new GameObject("Stations").transform;
            stations.SetParent(parent);

            GameObject board = Prop(stations, "Planche à découper", "sven:CuttingBoard",
                new Vector3(-0.85f, CounterY, 0.70f), height: 0.05f);
            RestOnSurface(board, CounterY);
            board.AddComponent<TransformationStation>();

            GameObject stove = Prop(stations, "Plaque de cuisson", "sven:Stove",
                new Vector3(-0.35f, CounterY, 0.70f), height: 0.09f);
            RestOnSurface(stove, CounterY);
            stove.AddComponent<TransformationStation>();

            // Aucun état n'est passé en paramètre : chaque station lit le sien dans l'ontologie
            // (sven:appliesState). Une troisième station ne demanderait qu'une ligne de Turtle.
        }

        /// <summary>Six assiettes identiques et interchangeables — un seul type de contenant (§5).</summary>
        private static void BuildPlates(Transform parent)
        {
            var plates = new GameObject("Assiettes").transform;
            plates.SetParent(parent);

            for (int i = 0; i < 6; i++)
            {
                var position = new Vector3(0.25f + (i % 3) * 0.26f, CounterY, 0.60f + (i / 3) * 0.26f);
                RestOnSurface(Prop(plates, $"Assiette {i + 1}", "sven:Plate", position,
                    height: 0.05f, grabbable: true), CounterY);
            }
        }

        #endregion

        #region Salle

        private static void BuildDiningRoom(Transform root)
        {
            var room = new GameObject("Salle").transform;
            room.SetParent(root);

            // Tables volontairement NON numérotées : c'est ce qui force le pointage
            // (« cette table-là 👆 »). Le nom Unity reste neutre.
            Vector3[] tables =
            {
                new(-1.6f, 0f, 3.6f), new(1.6f, 0f, 3.6f),
                new(-1.6f, 0f, 5.4f), new(1.6f, 0f, 5.4f),
            };

            // Le couple (famille, contrainte) de chaque table est ÉCRIT, jamais tiré : les
            // critères 4 et 7 du lot 4 doivent être montrables à CHAQUE lancement, pas dans
            // 74 % des cas. Les quatre couples découpent quatre sous-ensembles différents et
            // non dégénérés des neuf recettes — vérifié contre les sven:requires réels :
            //   A  Salade    sans banane   → refuse la salade de fruits POURTANT CONFORME
            //   B  Soupe     sans poisson  → l'inférence de branche (Salmon ⊑ Fish)
            //   C  Salade    végétarien    → l'union de branches, un seul plat conforme
            //   D  Sandwich  sans lactose  → Cheese ⊑ Dairy
            // La colonne « plats acceptables » n'est écrite nulle part : le client la CALCULE.
            (string family, string constraint)[] couples =
            {
                ("sven:Salad",    "sven:NoBanana"),
                ("sven:Soup",     "sven:FishAllergy"),
                ("sven:Salad",    "sven:Vegetarian"),
                ("sven:Sandwich", "sven:LactoseFree"),
            };

            for (int i = 0; i < tables.Length; i++)
            {
                GameObject table = Prop(room, $"Table {(char)('A' + i)}", "sven:Table",
                    tables[i], height: 0.75f);
                RestOnSurface(table, 0f);
                table.isStatic = true;

                MakeCustomer(room, table, couples[i].family, couples[i].constraint, i);
            }

            // Deux serveurs délibérément identiques : sans une paire indiscernable,
            // la clarification ne se déclenche jamais (§3 du README).
            MakeWaiter(Prop(room, "Serveur 1", "sven:Waiter", new Vector3(-0.7f, 0f, 2.6f), 1.75f));
            MakeWaiter(Prop(room, "Serveur 2", "sven:Waiter", new Vector3(0.7f, 0f, 2.6f), 1.75f));
        }

        /// <summary>
        /// Rend un serveur délégable : agent de navigation, machine à états, et exposition de
        /// son état de tâche au graphe.
        ///
        /// La sémantisation est **Dynamic** et non Static : l'activité change en cours de
        /// partie, et en Static elle serait observée une fois au démarrage puis figée — le
        /// graphe montrerait tous les serveurs éternellement disponibles.
        ///
        /// Le composant s'appelle Delegation et non Waiter, et ce n'est pas cosmétique : ce
        /// fichier vit dans Sc4ve.Demonstration.EditorTools, où le nom simple « Waiter »
        /// désignait la classe d'ANNOTATION Sc4ve.Demonstration.Waiter — C# examine les espaces
        /// de noms englobants avant les using. AddComponent&lt;Waiter&gt;() posait donc un second
        /// marqueur sémantique au lieu de la machine à états, sans la moindre erreur de
        /// compilation, et aucun serveur n'était délégable.
        /// </summary>
        private static void MakeWaiter(GameObject waiter)
        {
            RestOnSurface(waiter, 0f);

            // Le gabarit est réglé par Delegation.Awake, qui seul connaît l'échelle appliquée au
            // modèle : Unity multiplie rayon et hauteur par la transform, et le modèle est mis
            // à l'échelle pour faire 1,75 m. Poser des valeurs ici les ferait écraser — ou pire,
            // paraître correctes dans l'inspecteur tout en étant fausses en jeu.
            NavMeshAgent agent = waiter.AddComponent<NavMeshAgent>();
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.GoodQualityObstacleAvoidance;

            Delegation delegation = waiter.AddComponent<Delegation>();

            if (waiter.TryGetComponent(out SemantizationCore core))
                Register(core, delegation, SemanticProcessingMode.Dynamic);
        }

        /// <summary>
        /// Installe un client à sa table : modèle, annotation de contrainte, jauge de
        /// patience, et composant CustomerOrder sémantisé en Dynamic.
        ///
        /// La contrainte est ajoutée EXPLICITEMENT aux annotations, et non par
        /// SemanticHierarchy : il n'existe aucune classe C# pour les six contraintes, et c'est
        /// le but — en ajouter une septième est une ligne de Turtle, pas une recompilation.
        /// GetSemanticTypes lèverait, et le repli poserait l'annotation sans son parent.
        /// </summary>
        private static void MakeCustomer(Transform room, GameObject table,
                                         string family, string constraint, int index)
        {
            // Du côté OPPOSÉ à la cuisine (z de la table + 0,55) : le serveur arrive toujours
            // du côté cuisine et n'a jamais à traverser le client pour atteindre la table.
            Vector3 position = table.transform.position + new Vector3(0f, 0f, 0.55f);

            GameObject customer = Prop(room, $"Client {(char)('A' + index)}", "sven:Customer",
                position, height: 1.20f);
            RestOnSurface(customer, 0f);
            // Face à la table (et à la cuisine derrière elle).
            customer.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            // Exclu de la cuisson du NavMesh. Sans cela, son collider concave serait CUIT en
            // obstacle à 55 cm de la table — CollectObjects.Children ramasse tout collider,
            // les drapeaux static n'y changent rien — et le serveur expirerait sur « je
            // n'arrive pas à passer » sans qu'aucune erreur ne dise pourquoi. Les serveurs,
            // eux, sont exclus d'office par leur NavMeshAgent.
            var modifier = customer.AddComponent<NavMeshModifier>();
            modifier.ignoreFromBuild = true;

            if (customer.TryGetComponent(out SemanticAnnotator annotator))
            {
                if (!annotator.Annotations.Contains(constraint)) annotator.Annotations.Add(constraint);
                if (!annotator.Annotations.Contains("sven:DietaryConstraint"))
                    annotator.Annotations.Add("sven:DietaryConstraint");
            }

            Transform gaugeFill = MakeGauge(customer.transform);

            var order = customer.AddComponent<CustomerOrder>();
            order.Bind(table.GetComponent<SemantizationCore>(), gaugeFill, family, constraint);

            if (customer.TryGetComponent(out SemantizationCore customerCore))
                Register(customerCore, order, SemanticProcessingMode.Dynamic);
        }

        /// <summary>
        /// La jauge de patience, au-dessus de la tête, face à la cuisine. Rend le PIVOT dont
        /// CustomerOrder pilote l'échelle X (1 → 0, le remplissage fond vers la gauche).
        ///
        /// Le porte-jauge annule l'échelle du client : le modèle est mis à l'échelle pour
        /// faire 1,20 m, et tout enfant en hériterait — exactement le piège documenté par
        /// Delegation.Awake pour la main. Les colliders des primitives sont détruits : ils
        /// intercepteraient le pointeur XR (« mets ça ici 👆 » viserait la jauge).
        /// </summary>
        private static Transform MakeGauge(Transform customer)
        {
            var holder = new GameObject("Jauge");
            holder.transform.SetParent(customer, worldPositionStays: false);
            Vector3 s = customer.lossyScale;
            holder.transform.localScale = new Vector3(1f / Mathf.Max(0.001f, s.x),
                                                      1f / Mathf.Max(0.001f, s.y),
                                                      1f / Mathf.Max(0.001f, s.z));
            holder.transform.localPosition = Vector3.Scale(
                new Vector3(0f, 1.45f, 0f), holder.transform.localScale);
            // Le client regarde -z (tourné de 180°) ; la jauge doit regarder la cuisine comme
            // lui — donc pas de rotation supplémentaire dans son repère local.
            holder.transform.localRotation = Quaternion.identity;

            GameObject back = GameObject.CreatePrimitive(PrimitiveType.Cube);
            back.name = "Fond";
            back.transform.SetParent(holder.transform, worldPositionStays: false);
            back.transform.localPosition = Vector3.zero;
            back.transform.localScale = new Vector3(0.5f, 0.08f, 0.02f);
            back.GetComponent<Renderer>().sharedMaterial =
                GetMaterial("JaugeFond", new Color(0.12f, 0.12f, 0.12f));
            UnityEngine.Object.DestroyImmediate(back.GetComponent<Collider>());

            var pivot = new GameObject("Pivot");
            pivot.transform.SetParent(holder.transform, worldPositionStays: false);
            // Au bord GAUCHE du fond : réduire l'échelle X du pivot fait fondre le
            // remplissage vers la gauche au lieu de le rétrécir par les deux bouts.
            pivot.transform.localPosition = new Vector3(-0.24f, 0f, 0f);
            pivot.transform.localScale = Vector3.one;

            GameObject fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fill.name = "Remplissage";
            fill.transform.SetParent(pivot.transform, worldPositionStays: false);
            fill.transform.localPosition = new Vector3(0.24f, 0f, 0f);
            fill.transform.localScale = new Vector3(0.48f, 0.06f, 0.015f);
            fill.GetComponent<Renderer>().sharedMaterial =
                GetMaterial("JaugeRemplissage", new Color(0.35f, 0.65f, 0.30f));
            UnityEngine.Object.DestroyImmediate(fill.GetComponent<Collider>());

            return pivot.transform;
        }

        #endregion

        #region Rig XR

        /// <summary>Nom du prefab de rig complet, livré avec les Starter Assets du toolkit.</summary>
        private const string RigPrefabName = "XR Origin (XR Rig)";

        /// <summary>
        /// Met en place un rig XR **utilisable**, c'est-à-dire muni de contrôleurs.
        ///
        /// Le menu « GameObject > XR > XR Origin (VR) » d'Unity ne crée qu'une origine, un
        /// offset et une caméra : aucun interactor, donc ni pointage déictique ni saisie —
        /// GrabCommand ne trouverait rien et « mets ça ici 👆 » ne résoudrait aucun point.
        /// On instancie donc le prefab des Starter Assets, qui apporte les deux contrôleurs.
        ///
        /// Le rig vit HORS de la racine générée : il survit aux reconstructions et peut être
        /// réglé à la main. Un rig déjà pourvu d'interactors n'est jamais remplacé.
        /// </summary>
        /// <returns>Vrai si un rig a été mis en place, faux s'il en existait déjà un d'utilisable.</returns>
        private static bool EnsureXRRig()
        {
            GameObject existing = FindRigRoot();
            if (existing != null)
            {
                if (existing.GetComponentInChildren<XRBaseInteractor>(true) != null)
                {
                    EnsureInteractionManager();
                    EnsureSvenInteractors(existing);
                    return false;
                }

                Debug.Log("[DemoSceneBuilder] Rig XR sans contrôleur détecté (celui du menu Unity) : " +
                          "remplacé par le rig complet des Starter Assets.");
                UnityEngine.Object.DestroyImmediate(existing);
            }

            GameObject prefab = LoadRigPrefab();
            if (prefab == null)
            {
                Debug.LogWarning(
                    $"[DemoSceneBuilder] Prefab « {RigPrefabName} » introuvable. Importer les " +
                    "Starter Assets du XR Interaction Toolkit (Package Manager > XR Interaction " +
                    "Toolkit > Samples), puis relancer.");
                return false;
            }

            var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(rig, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            rig.transform.position = Vector3.zero;

            DisableLocomotion(rig);
            EnsureInteractionManager();
            EnsureSvenInteractors(rig);

            Debug.Log($"[DemoSceneBuilder] Rig XR complet mis en place depuis « {RigPrefabName} ».");
            return true;
        }

        private static GameObject FindRigRoot()
        {
            foreach (GameObject go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                if (go.name.StartsWith("XR Origin"))
                    return go;
            return null;
        }

        /// <summary>Recherche par nom plutôt que par chemin : la version du sample change.</summary>
        private static GameObject LoadRigPrefab()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab XR Origin"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == RigPrefabName)
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            return null;
        }

        /// <summary>
        /// Désactive le sous-arbre « Locomotion » du rig : téléportation, déplacement au
        /// joystick, rotation, escalade. Aucune locomotion artificielle (§11 du README) — le
        /// joueur reste en cuisine et ne se déplace que physiquement.
        ///
        /// Désactivé plutôt que supprimé : les contrôleurs référencent ces fournisseurs, et
        /// les détruire laisserait des références nulles.
        /// </summary>
        private static void DisableLocomotion(GameObject rig)
        {
            Transform locomotion = rig.transform.Find("Locomotion");
            if (locomotion == null) return;

            locomotion.gameObject.SetActive(false);
            Debug.Log("[DemoSceneBuilder] Sous-arbre « Locomotion » désactivé : ni téléportation " +
                      "ni joystick, le joueur ne se déplace que physiquement (§11 du README).");
        }

        private static void EnsureInteractionManager()
        {
            if (UnityEngine.Object.FindAnyObjectByType<XRInteractionManager>() != null) return;
            new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
        }

        /// <summary>
        /// Greffe les interactors SVEN sur le rig — c'est ce qui alimente le graphe en
        /// pointage et en point de vue, et donc ce qui rend la deixis possible.
        ///
        /// Sans Pointer, « mets ça **ici** 👆 » n'a aucun point à résoudre : MoveCommand
        /// cherche explicitement un Pointer dans la scène. Sans lui, l'énoncé hybride —
        /// le critère d'acceptation du §3 — est intestable.
        ///
        /// Les interactors sont autonomes : Interactor.Awake récupère lui-même son
        /// SemantizationCore (ajouté par RequireComponent) et lance sa propre boucle.
        ///
        /// GraspArea n'est volontairement PAS posé : dans la scène bureau de référence il est
        /// sur la caméra, ce qui n'a pas de sens en VR où l'on saisit avec les mains. À
        /// trancher quand la saisie sera réellement testée en casque.
        /// </summary>
        private static void EnsureSvenInteractors(GameObject rig)
        {
            Camera camera = rig.GetComponentInChildren<Camera>(true);
            if (camera != null)
            {
                if (camera.GetComponent<PointOfView>() == null)
                {
                    var pov = camera.gameObject.AddComponent<PointOfView>();
                    pov.cameraComponent = camera;
                    Debug.Log("[DemoSceneBuilder] PointOfView ajouté sur la caméra du rig.");
                }

                // On sémantise le composant Camera, PAS le PointOfView : la table
                // MappedComponents associe explicitement `typeof(PointOfView)` à `null`,
                // donc l'enregistrer n'écrirait rien. C'est aussi ce que fait « New Demo ».
                var core = camera.GetComponent<SemantizationCore>();
                Register(core, camera.transform, SemanticProcessingMode.Dynamic);
                Register(core, camera, SemanticProcessingMode.Dynamic);
            }

            // Un Pointer par contrôleur. Valeurs reprises de « New Demo », la scène de
            // référence qui fonctionne : portée 4 m, cône de 5°.
            string[] controllers = { "Right Controller", "Left Controller" };
            for (int i = 0; i < controllers.Length; i++)
            {
                Transform hand = FindDeep(rig.transform, controllers[i]);
                if (hand == null)
                {
                    Debug.LogWarning($"[DemoSceneBuilder] « {controllers[i]} » introuvable dans le rig : " +
                                     "pas de Pointer pour cette main.");
                    continue;
                }

                Pointer pointer = hand.GetComponent<Pointer>();
                if (pointer == null)
                {
                    pointer = hand.gameObject.AddComponent<Pointer>();
                    pointer.PointerIndex = i;
                    pointer.PointerDistance = 4f;
                    pointer.PointerConeAngle = 5f;
                    Debug.Log($"[DemoSceneBuilder] Pointer ajouté sur « {controllers[i]} » (index {i}).");
                }

                // Le Transform ET le composant Pointer, sinon `pointerHitPosition` n'atteint
                // jamais le graphe et PointParameter.QueryPoint ne trouve aucun point :
                // « mets ça ici 👆 » resterait sans effet.
                var core = hand.GetComponent<SemantizationCore>();
                Register(core, hand, SemanticProcessingMode.Dynamic);
                Register(core, pointer, SemanticProcessingMode.Dynamic);
            }
        }

        /// <summary>
        /// Coche un composant dans la liste de sémantisation, sans doublon.
        /// Un composant absent de cette liste n'est jamais observé : c'est elle, et non la
        /// simple présence du composant, qui décide de ce qui entre dans le graphe.
        /// </summary>
        private static void Register(SemantizationCore core, Component component, SemanticProcessingMode mode)
        {
            if (core == null || component == null) return;
            if (core.componentsToSemanticize.Any(c => c != null && c.Component == component)) return;

            core.componentsToSemanticize.Add(Entry(component, mode));
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            foreach (Transform child in parent)
            {
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// Le panneau de bons de commande, au-dessus de la passe, face à la cuisine (§5).
        /// Ni collider, ni SemantizationCore : c'est du HUD — un collider intercepterait le
        /// pointeur XR, une sémantisation ferait répondre « sélectionne le tableau ».
        /// </summary>
        private static void BuildOrderBoard(Transform root)
        {
            var board = new GameObject("Tableau des commandes");
            board.transform.SetParent(root);
            board.transform.position = new Vector3(0f, 1.75f, 1.95f);
            board.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            GameObject back = GameObject.CreatePrimitive(PrimitiveType.Cube);
            back.name = "Fond";
            back.transform.SetParent(board.transform, worldPositionStays: false);
            back.transform.localPosition = new Vector3(0f, 0f, 0.015f);
            back.transform.localScale = new Vector3(1.15f, 0.62f, 0.02f);
            back.GetComponent<Renderer>().sharedMaterial =
                GetMaterial("TableauFond", new Color(0.16f, 0.20f, 0.17f));
            UnityEngine.Object.DestroyImmediate(back.GetComponent<Collider>());

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var textHolder = new GameObject("Texte");
            textHolder.transform.SetParent(board.transform, worldPositionStays: false);
            textHolder.transform.localPosition = new Vector3(0f, 0.26f, 0f);

            var mesh = textHolder.AddComponent<TextMesh>();
            mesh.text = "COMMANDES";
            mesh.font = font;
            mesh.fontSize = 64;
            mesh.characterSize = 0.011f;
            mesh.anchor = TextAnchor.UpperCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = new Color(0.92f, 0.90f, 0.82f);
            if (font != null) textHolder.GetComponent<MeshRenderer>().sharedMaterial = font.material;

            // Le composant vit dans Assembly-CSharp (Demonstration/Scripts) : il ne fait que
            // relire les CustomerOrder de la scène — aucune commande ne le référence.
            textHolder.AddComponent<Sc4ve.Demonstration.OrderBoard>();
        }

        /// <summary>
        /// Pose le ralenti-pendant-la-parole (§2) sur l'objet SVEN — l'infrastructure de scène
        /// qui survit aux reconstructions, même précédent qu'EnsureGraphController.
        ///
        /// PAS sur le GameObject du VoiceProcessor : le builder ne construit pas le pipeline
        /// vocal, donc à la construction il n'existe le plus souvent PAS ENCORE — l'accrocher
        /// là reviendrait à ne jamais le poser. ListeningTimeScale résout lui-même son
        /// VoiceProcessor dans Awake, et journalise s'il n'en trouve pas.
        /// </summary>
        private static void EnsureListeningTimeScale()
        {
            if (UnityEngine.Object.FindAnyObjectByType<ListeningTimeScale>() != null) return;

            GameObject host = UnityEngine.Object.FindAnyObjectByType<GraphController>() is { } controller
                ? controller.gameObject
                : new GameObject("SVEN");
            host.AddComponent<ListeningTimeScale>();

            if (UnityEngine.Object.FindAnyObjectByType<Sc4ve.Voice.VoiceProcessor>() == null)
                Debug.LogWarning("[DemoSceneBuilder] ListeningTimeScale posé, mais aucun " +
                                 "VoiceProcessor dans la scène : le ralenti du §2 restera " +
                                 "inactif — et le critère 6 du lot 4 inobservable — tant que " +
                                 "le pipeline vocal n'est pas ajouté.");
        }

        /// <summary>
        /// Cuit le NavMesh sur lequel les serveurs circulent.
        ///
        /// Cuire ici plutôt que de le laisser à la main : le contenu se reconstruit à chaque
        /// exécution de l'outil, donc un NavMesh cuit une fois serait périmé dès la
        /// reconstruction suivante — et un NavMesh périmé ne produit aucune erreur, seulement
        /// des serveurs qui refusent de bouger.
        ///
        /// La surface est posée SUR la racine générée : elle est donc détruite et recuite avec
        /// le reste, ce qui est exactement ce qu'on veut.
        /// </summary>
        private static void BakeNavMesh(Transform root)
        {
            NavMeshSurface surface = root.gameObject.AddComponent<NavMeshSurface>();

            // Children et non All : « All » ratisserait TOUTE la scène, rig XR compris, et le
            // capsule collider du joueur creuserait un trou dans le NavMesh là où il se tient.
            // Limiter aux enfants de la racine générée revient à cuire exactement le décor —
            // et rien de ce que l'utilisateur a posé à la main autour.
            surface.collectObjects = CollectObjects.Children;

            // Les colliders plutôt que les meshes de rendu : les tables, le plan de travail et
            // la poubelle deviennent des obstacles sans qu'on ait à les marquer un par un.
            // Les serveurs, eux, portent un NavMeshAgent — NavMeshSurface les exclut d'office,
            // sans quoi chacun se creuserait un trou sous les pieds.
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            // Type d'agent par défaut : c'est celui que porte le NavMeshAgent des serveurs.
            // Cuire pour un autre gabarit laisserait croire à des passages qu'ils ne peuvent
            // pas emprunter.
            surface.agentTypeID = 0;
            surface.BuildNavMesh();

            Debug.Log("[DemoSceneBuilder] NavMesh cuit sur la géométrie générée.");
        }

        /// <summary>
        /// Sans GraphController, rien n'est sémantisé : c'est son Awake() qui appelle
        /// GraphManager.Reload(), et SemantizationCore abandonne après dix secondes d'attente
        /// avec « GraphManager is not initialized ». Pas de graphe, donc pas de SPARQL, donc
        /// aucune commande vocale ne peut résoudre quoi que ce soit.
        ///
        /// Placé HORS de la racine générée, comme le rig : c'est de l'infrastructure de scène,
        /// et on veut pouvoir la déplacer ou la configurer sans qu'une reconstruction l'écrase.
        /// </summary>
        private static void EnsureGraphController()
        {
            if (UnityEngine.Object.FindAnyObjectByType<GraphController>() != null) return;

            var go = new GameObject("SVEN");
            go.AddComponent<GraphController>();
            Debug.Log("[DemoSceneBuilder] GraphController ajouté : sans lui, aucun objet n'est " +
                      "sémantisé et aucune commande ne peut résoudre de cible.");
        }

        /// <summary>
        /// Supprime la « Main Camera » par défaut laissée par la scène vide.
        ///
        /// Le menu XR d'Unity ajoute son propre rig sans retirer la caméra existante : la scène
        /// se retrouve avec deux AudioListener, et Unity le signale à chaque image. La caméra
        /// par défaut ne sert plus à rien une fois le rig en place — c'est celle du rig qui rend
        /// dans le casque.
        /// </summary>
        private static void RemoveStrayMainCamera(GameObject rigRoot)
        {
            if (rigRoot == null) return;

            // Le rig nomme AUSSI sa caméra « Main Camera » : la scène en contient donc deux.
            // GameObject.Find n'en renvoie qu'une, et rien ne dit laquelle — il faut les
            // parcourir toutes et ne garder que celle du rig.
            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include);

            foreach (Camera camera in cameras)
            {
                if (camera == null) continue;
                if (camera.transform.IsChildOf(rigRoot.transform)) continue;
                if (camera.name != "Main Camera") continue;

                UnityEngine.Object.DestroyImmediate(camera.gameObject);
                Debug.Log("[DemoSceneBuilder] « Main Camera » par défaut supprimée : le rig XR " +
                          "apporte la sienne, et deux AudioListener font râler Unity à chaque image.");
            }
        }

        #endregion

        #region Exposition

        /// <summary>
        /// Aligne un exemplaire de chaque objet manipulable, à sa taille réelle, sur un établi.
        ///
        /// C'est une planche de contact, pas une scène de jeu : elle sert à repérer d'un coup
        /// d'œil un modèle raté, une taille incohérente ou une pièce décollée. Les objets y
        /// gardent leurs composants sémantiques — ce sont les mêmes prefabs que dans le jeu,
        /// pas des copies — mais rien ne les sémantise tant qu'on n'entre pas en mode Play.
        /// </summary>
        private static void BuildExpositionContent(Transform root)
        {
            const float benchTop = 0.95f;

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Sol";
            floor.transform.SetParent(root);
            floor.transform.localScale = new Vector3(2f, 1f, 2f);
            floor.GetComponent<Renderer>().sharedMaterial = GetMaterial("Sol", new Color(0.32f, 0.30f, 0.28f));

            // Six rangées à couvrir : quatre états d'ingrédients, les ustensiles, les plats.
            Box(root, "Établi", new Vector3(0f, benchTop - 0.05f, 0.80f),
                new Vector3(5.0f, 0.10f, 3.1f), new Color(0.55f, 0.52f, 0.48f));

            // Un ingrédient par colonne, un ÉTAT par rangée. Les états changent l'apparence
            // (brunissement, aplatissement) : sans eux, l'exposition ne montrerait qu'un tiers
            // de ce que le joueur verra.
            (string label, string[] states)[] rows =
            {
                ("cru", new string[0]),
                ("coupé", new[] { "sven:Sliced" }),
                ("cuit", new[] { "sven:Cooked" }),
                ("coupé + cuit", new[] { "sven:Sliced", "sven:Cooked" }),
            };

            var foods = new GameObject("Ingrédients").transform;
            foods.SetParent(root);

            float step = 4.4f / Ingredients.Length;

            // Les prefabs sont obtenus UNE SEULE FOIS, avant toute instanciation.
            // EnsurePrefab supprime puis recrée l'asset : l'appeler à chaque rangée détruisait
            // le prefab dont la rangée précédente venait de tirer des instances, qui perdaient
            // alors leur liaison et leur contenu.
            var prefabs = new GameObject[Ingredients.Length];
            for (int i = 0; i < Ingredients.Length; i++)
                prefabs[i] = EnsurePrefab(Ingredients[i]);

            for (int r = 0; r < rows.Length; r++)
            {
                var (rowLabel, states) = rows[r];
                float z = -0.45f + r * 0.50f;

                var row = new GameObject(rowLabel).transform;
                row.SetParent(foods);

                for (int i = 0; i < Ingredients.Length; i++)
                {
                    Ingredient ingredient = Ingredients[i];
                    GameObject prefab = prefabs[i];
                    if (prefab == null) continue;

                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, row);
                    instance.name = ShortName(ingredient.Semantic);
                    instance.transform.position = new Vector3(-2.2f + step * (i + 0.5f), benchTop, z);

                    NormalizeSize(instance, ingredient.Size);

                    // L'état est appliqué APRÈS la mise à l'échelle, comme en jeu : l'objet est
                    // à sa taille quand il passe à la station. C'est le code de la station lui-
                    // même qui est appelé, pour que la vitrine ne puisse pas diverger du jeu.
                    foreach (string state in states)
                        TransformationStation.ApplyState(instance.GetComponent<SemantizationCore>(), state);

                    RestOnSurface(instance, benchTop);

                    // Le nom sur la première rangée seulement : répété quatre fois, il
                    // encombrerait plus qu'il n'aiderait.
                    if (r == 0) Label(instance.transform, instance.name, ingredient.Size);
                }

                LabelAt(row, rowLabel, new Vector3(-2.55f, benchTop + 0.06f, z));
            }

            // Rangée 2 : contenants et stations, à la même échelle que dans le jeu.
            var wares = new GameObject("Contenants et stations").transform;
            wares.SetParent(root);

            (string name, string semantic, float height)[] containers =
            {
                ("Assiette", "sven:Plate", 0.05f),
                ("Poubelle", "sven:Bin", 0.72f),
                ("Planche à découper", "sven:CuttingBoard", 0.05f),
                ("Plaque de cuisson", "sven:Stove", 0.09f),
                ("Couteau", "sven:Knife", 0.03f),
            };

            for (int i = 0; i < containers.Length; i++)
            {
                var (name, semantic, height) = containers[i];
                GameObject item = Prop(wares, name, semantic,
                    new Vector3(-1.9f + i * 0.95f, benchTop, 1.55f), height);
                RestOnSurface(item, benchTop);
                Label(item.transform, name, height + 0.12f);
            }

            // Rangée intermédiaire : les neuf plats assemblés, chacun dans une assiette.
            // Ce sont des HABILLAGES — la conformité continue de se lire sur les ingrédients
            // contenus, pas sur ces modèles (§6.3 du README).
            var dishes = new GameObject("Plats").transform;
            dishes.SetParent(root);

            float dishStep = 4.4f / DishMeshFactory.Recipes.Length;
            for (int i = 0; i < DishMeshFactory.Recipes.Length; i++)
            {
                string recipe = DishMeshFactory.Recipes[i];
                var position = new Vector3(-2.2f + dishStep * (i + 0.5f), benchTop, 2.05f);

                GameObject plate = Prop(dishes, recipe, "sven:Plate", position, height: 0.05f);
                RestOnSurface(plate, benchTop);

                GameObject dish = BuildDish(recipe);
                if (dish == null) continue;

                // Placement en MONDE, puis parentage en conservant la position. Posé en
                // coordonnées locales, le plat héritait de l'échelle de l'assiette et se
                // retrouvait projeté loin au-dessus d'elle.
                //
                // Sa taille est MESURÉE, pas devinée : le plat est mis à l'échelle pour occuper
                // les trois quarts du diamètre de l'assiette. Les neuf modèles n'ont pas la
                // même envergure native — un tas de fruits déborde là où une soupe est un
                // disque — et une constante commune en faisait forcément déborder certains.
                Bounds plateBounds = WorldBounds(plate);
                float plateWidth = Mathf.Max(plateBounds.size.x, plateBounds.size.z);
                float dishWidth = Mathf.Max(WorldBounds(dish).size.x, WorldBounds(dish).size.z);

                if (dishWidth > Mathf.Epsilon)
                    dish.transform.localScale = Vector3.one * (plateWidth * 0.75f / dishWidth);

                dish.transform.position = new Vector3(
                    plateBounds.center.x, plateBounds.max.y - plateBounds.size.y * 0.35f, plateBounds.center.z);
                dish.transform.SetParent(plate.transform, worldPositionStays: true);

                Label(plate.transform, recipe, 0.16f);
            }

            // Rangée 3 : le mobilier et les personnes, au sol — leur taille les y oblige.
            var stage = new GameObject("Mobilier et personnes").transform;
            stage.SetParent(root);

            (string name, string semantic, Vector3 position, float height)[] actors =
            {
                ("Table", "sven:Table", new Vector3(-1.4f, 0f, 2.6f), 0.75f),
                ("Serveur", "sven:Waiter", new Vector3(0f, 0f, 2.6f), 1.75f),
                ("Client", "sven:Customer", new Vector3(1.4f, 0f, 2.6f), 1.70f),
            };

            foreach (var (name, semantic, position, height) in actors)
            {
                GameObject item = Prop(stage, name, semantic, position, height);
                RestOnSurface(item, 0f);
                Label(item.transform, name, height + 0.15f);
            }
        }

        /// <summary>
        /// Étiquette flottante au-dessus d'un objet. Sans elle, distinguer un pavé de saumon
        /// d'un steak dans une rangée de douze demande de cliquer sur chacun.
        ///
        /// TextMesh hérité plutôt que TextMeshPro : il ne dépend d'aucun asset à importer, ce
        /// qui convient à une scène d'inspection dont la typographie n'a aucune importance.
        /// </summary>
        private static void Label(Transform target, string text, float objectHeight)
            => LabelAt(target.parent, text, target.position + Vector3.up * (objectHeight + 0.10f));

        /// <summary>
        /// Étiquette posée à un point donné, indépendante de l'objet.
        ///
        /// Détachée de sa cible et non enfant : parentée, elle hériterait de son échelle — or
        /// les aliments vont de 0,07 à 0,22 m, ce qui donnerait des textes de tailles absurdes.
        /// </summary>
        private static void LabelAt(Transform parent, string text, Vector3 position)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) return;

            var label = new GameObject($"— {text}");
            label.transform.SetParent(parent, worldPositionStays: false);
            label.transform.position = position;
            label.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            label.transform.localScale = Vector3.one;

            var mesh = label.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.font = font;
            mesh.fontSize = 72;
            mesh.characterSize = 0.012f;
            mesh.anchor = TextAnchor.LowerCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = Color.white;

            label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        #endregion

        #region Fabrique d'objets

        /// <summary>
        /// Crée un objet sémantisé complet : forme, annotation SVEN et, si demandé, saisie XR.
        ///
        /// Le montage de la sémantisation reproduit celui des prefabs de fruits existants
        /// (Transform et Renderer en Dynamic, MeshFilter et composant d'annotation en Static),
        /// à une exception près et volontaire : le SemanticAnnotator est en **Dynamic**, pour
        /// que l'ajout d'un état à l'exécution atteigne le graphe (§6.2 du README).
        /// </summary>
        /// <summary>
        /// Version « prop » de la fabrique : la forme et les couleurs viennent de
        /// PropMeshFactory au lieu d'être passées à la main.
        ///
        /// La taille demandée est la HAUTEUR réelle de l'objet, appliquée après coup : les
        /// meshes de props sont dessinés dans un cube unité, il faut les remettre à l'échelle
        /// du monde.
        /// </summary>
        private static GameObject Prop(Transform parent, string name, string semanticType,
                                       Vector3 position, float height, bool grabbable = false)
        {
            List<IngredientMeshFactory.Part> parts = PropMeshFactory.Parts(ShortName(semanticType));
            if (parts.Count == 0)
                return Semantized(parent, name, PrimitiveType.Cube, position,
                    Vector3.one * height, Color.magenta, semanticType, grabbable);

            GameObject prop = Semantized(parent, name, PrimitiveType.Cube, position,
                Vector3.one, parts[0].Color, semanticType, grabbable, parts[0].Mesh);

            AddDistinctiveParts(prop, parts);
            NormalizeHeight(prop, height);
            return prop;
        }

        /// <summary>
        /// L'habillage visuel d'un plat : un objet purement graphique, sans sémantisation ni
        /// collider.
        ///
        /// Il ne porte AUCUNE annotation, délibérément. Le plat n'existe pas dans le graphe —
        /// ce qui y existe, c'est une assiette et son contenu. Annoter cet habillage créerait
        /// un second objet concurrent, que « sélectionne la salade » désignerait au lieu de
        /// l'assiette réelle.
        /// </summary>
        private static GameObject BuildDish(string recipe)
        {
            List<IngredientMeshFactory.Part> parts = DishMeshFactory.Parts(recipe);
            if (parts.Count == 0) return null;

            var dish = new GameObject(recipe);

            foreach (IngredientMeshFactory.Part part in parts)
            {
                if (part.Mesh == null) continue;

                var piece = new GameObject(part.Name);
                piece.transform.SetParent(dish.transform, false);
                piece.transform.localPosition = part.Position;
                piece.transform.localEulerAngles = part.Rotation;

                piece.AddComponent<MeshFilter>().sharedMesh = part.Mesh;
                piece.AddComponent<MeshRenderer>().sharedMaterial =
                    GetMaterial($"Dish{recipe}{part.Name}", part.Color);
            }
            return dish;
        }

        /// <summary>Met l'objet à une hauteur réelle, mesurée sur ses Renderer.</summary>
        private static void NormalizeHeight(GameObject go, float height)
        {
            float current = WorldBounds(go).size.y;
            if (current > Mathf.Epsilon) go.transform.localScale *= height / current;
        }

        private static GameObject Semantized(Transform parent, string name, PrimitiveType shape,
                                             Vector3 position, Vector3 scale, Color color,
                                             string semanticType, bool grabbable, Mesh mesh = null)
        {
            GameObject go = GameObject.CreatePrimitive(shape);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.position = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = GetMaterial(ShortName(semanticType), color);

            if (mesh != null) ReplaceMesh(go, mesh, convex: grabbable);

            string[] hierarchy = SemanticHierarchy(semanticType);
            Type annotationType = ResolveAnnotationType(semanticType);

            var core = go.AddComponent<SemantizationCore>();
            var annotator = go.AddComponent<SemanticAnnotator>();
            annotator.Annotations = hierarchy.ToList();

            core.componentsToSemanticize.Add(Entry(go.transform, SemanticProcessingMode.Dynamic));
            core.componentsToSemanticize.Add(Entry(go.GetComponent<MeshFilter>(), SemanticProcessingMode.Static));
            core.componentsToSemanticize.Add(Entry(go.GetComponent<MeshRenderer>(), SemanticProcessingMode.Dynamic));
            core.componentsToSemanticize.Add(Entry(annotator, SemanticProcessingMode.Dynamic));

            if (annotationType != null && go.AddComponent(annotationType) is Component annotation)
                core.componentsToSemanticize.Add(Entry(annotation, SemanticProcessingMode.Static));

            // Tout sven:Container — assiette, poubelle, station — expose son contenu au graphe.
            // C'est l'ontologie qui décide : on regarde si « sven:Container » figure dans la
            // hiérarchie d'annotations, plutôt que d'énumérer les types à la main.
            if (hierarchy.Contains("sven:Container"))
                core.componentsToSemanticize.Add(
                    Entry(go.AddComponent<ContainerContent>(), SemanticProcessingMode.Dynamic));

            if (grabbable)
            {
                var body = go.AddComponent<Rigidbody>();
                body.useGravity = true;
                var grab = go.AddComponent<XRGrabInteractable>();
                grab.useDynamicAttach = true;
            }

            return go;
        }

        /// <summary>
        /// Monte les pièces annexes en enfants du corps.
        ///
        /// Elles n'ont ni collider ni sémantisation : ce sont des détails de silhouette, pas
        /// des objets. Le collider du corps suffit à la saisie, et un os qui serait un objet
        /// distinct polluerait le graphe comme les sélections.
        /// </summary>
        private static void AddDistinctiveParts(GameObject body, List<IngredientMeshFactory.Part> parts)
        {
            // La position déclarée du CORPS sert d'origine commune.
            //
            // Elle était purement et simplement ignorée — le corps étant posé par l'appelant —
            // alors que les pièces annexes gardaient la leur. Le pied de la table, décrit sous
            // un plateau situé à 0,48, se retrouvait donc à traverser ce plateau ramené à zéro.
            Vector3 origin = parts.Count > 0 ? parts[0].Position : Vector3.zero;

            for (int i = 1; i < parts.Count; i++)
            {
                IngredientMeshFactory.Part part = parts[i];
                if (part.Mesh == null) continue;

                var piece = new GameObject(part.Name);
                piece.transform.SetParent(body.transform, false);
                piece.transform.localPosition = part.Position - origin;
                piece.transform.localEulerAngles = part.Rotation;

                piece.AddComponent<MeshFilter>().sharedMesh = part.Mesh;
                piece.AddComponent<MeshRenderer>().sharedMaterial =
                    GetMaterial($"{body.name}{part.Name}", part.Color);
            }
        }

        /// <summary>
        /// Remplace la forme primitive par un mesh généré, et lui donne un collider adapté.
        ///
        /// Convexe UNIQUEMENT si l'objet est saisissable, c'est-à-dire s'il porte un Rigidbody :
        /// c'est la seule situation où Unity l'exige. Un collider convexe est plafonné à 256
        /// faces, et le dépassement ne produit qu'un avertissement — « the partial hull will be
        /// used » — jamais une erreur : la silhouette du collider s'écarte alors silencieusement
        /// du modèle. Les serveurs et les clients, faits d'un blob à 320 faces, tombaient
        /// exactement dans ce cas.
        ///
        /// Pour tout le reste — table, poubelle, station, personnage — un collider CONCAVE est
        /// à la fois autorisé, exact et sans plafond. Il vaut mieux ici : le pointage frappe la
        /// vraie silhouette, et la cuisson du NavMesh découpe les obstacles à leur forme réelle
        /// plutôt qu'à leur enveloppe convexe — un plateau de table ne bouche plus le dessous.
        /// </summary>
        private static void ReplaceMesh(GameObject go, Mesh mesh, bool convex)
        {
            go.GetComponent<MeshFilter>().sharedMesh = mesh;

            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = convex;
        }
        private static SemanticComponent Entry(Component component, SemanticProcessingMode mode)
            => new() { Component = component, ProcessingMode = mode };

        /// <summary>
        /// Hiérarchie d'annotations, parents compris. L'inspecteur SVEN matérialise les
        /// parents à l'édition (cocher « Apple » écrit aussi Fruit et Food) et le filtre de
        /// sélection ne fait aucune inférence : sans les parents, « sélectionne les fruits »
        /// ne trouverait rien.
        /// </summary>
        private static string[] SemanticHierarchy(string semanticType)
        {
            try
            {
                return ISemanticAnnotation.GetSemanticTypes(semanticType);
            }
            catch (ArgumentException)
            {
                Debug.LogWarning($"[DemoSceneBuilder] Aucune classe C# pour {semanticType} : " +
                                 "annotation posée seule, sans ses parents.");
                return new[] { semanticType };
            }
        }

        private static Type ResolveAnnotationType(string semanticType)
        {
            try { return ISemanticAnnotation.GetType(semanticType); }
            catch (ArgumentException) { return null; }
        }

        private static GameObject Box(Transform parent, string name, Vector3 position,
                                      Vector3 scale, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.position = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = GetMaterial(name, color);
            go.isStatic = true;
            return go;
        }

        private static string ShortName(string semanticType)
        {
            int colon = semanticType.IndexOf(':');
            return colon >= 0 ? semanticType[(colon + 1)..] : semanticType;
        }

        #endregion

        #region Matériaux

        /// <summary>
        /// Matériaux de remplacement : 8 des 11 ingrédients n'ont pas encore de mesh (§6.2).
        /// La sémantique ne dépend pas du mesh — la lisibilité de la démo, si. À remplacer
        /// par de vrais modèles avant toute présentation publique.
        /// </summary>
        private static Material GetMaterial(string name, Color color)
        {
            Directory.CreateDirectory(MaterialsPath);
            string path = $"{MaterialsPath}/{name.Replace(" ", "")}.mat";

            // La couleur est réappliquée MÊME sur un matériau déjà là. Rendre l'existant tel
            // quel était le dernier test de fraîcheur de l'outil, et il se trompait comme les
            // précédents : changer une couleur dans le code ne changeait rien à l'écran, le
            // matériau de la première exécution survivant à toutes les suivantes.
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                Tint(existing, color);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Standard")
                            ?? Shader.Find("Diffuse");

            var material = new Material(shader);
            Tint(material, color);

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>
        /// Pose la couleur sur les deux propriétés : `color` pour les shaders hérités,
        /// `_BaseColor` pour URP. L'une seule ne suffit pas selon le pipeline actif.
        /// </summary>
        private static void Tint(Material material, Color color)
        {
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        }

        #endregion
    }
}
