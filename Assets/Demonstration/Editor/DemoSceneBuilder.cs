using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sc4ve.Multimodality;
using Sven.Content;
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
            bool rigCreated = EnsureXRRig();
            // Hors du bloc ci-dessus : la caméra parasite doit aussi disparaître des scènes
            // où le rig existait déjà avant cette version de l'outil.
            RemoveStrayMainCamera(FindRigRoot());
            EnsureGraphController();

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
                "Restent à faire à la main :\n" +
                "• cuire le NavMesh (Window > AI > Navigation)\n" +
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

            Semantized(kitchen, "Poubelle", PrimitiveType.Cylinder,
                new Vector3(1.35f, 0.25f, 0.75f), new Vector3(0.30f, 0.25f, 0.30f),
                new Color(0.22f, 0.24f, 0.26f), "sven:Bin", grabbable: false);

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
                GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"Assets/Resources/Prefabs/{ingredient.ExistingPrefab}.prefab");
                if (existing != null) return existing;

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

            // Les traits distinctifs — os, queue, pédoncule, grignes — deviennent des enfants,
            // avec leur propre couleur. C'est ce qui rend l'aliment reconnaissable ; le corps
            // seul n'est qu'un galet coloré.
            AddDistinctiveParts(temporary, parts);

            GameObject asset = PrefabUtility.SaveAsPrefabAsset(temporary, path);
            UnityEngine.Object.DestroyImmediate(temporary);

            Debug.Log($"[DemoSceneBuilder] Prefab fabriqué : {path}" +
                      (mesh != null ? "." : " (primitif de remplacement, aucun mesh généré)."));
            return asset;
        }

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

            GameObject board = Semantized(stations, "Planche à découper", PrimitiveType.Cube,
                new Vector3(-0.85f, CounterY, 0.70f), new Vector3(0.40f, 0.04f, 0.30f),
                new Color(0.72f, 0.55f, 0.35f), "sven:CuttingBoard", grabbable: false);
            RestOnSurface(board, CounterY);
            board.AddComponent<TransformationStation>();

            GameObject stove = Semantized(stations, "Plaque de cuisson", PrimitiveType.Cube,
                new Vector3(-0.35f, CounterY, 0.70f), new Vector3(0.36f, 0.04f, 0.30f),
                new Color(0.18f, 0.18f, 0.20f), "sven:Stove", grabbable: false);
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
                RestOnSurface(Semantized(plates, $"Assiette {i + 1}", PrimitiveType.Cylinder, position,
                    new Vector3(0.22f, 0.015f, 0.22f), new Color(0.93f, 0.93f, 0.90f),
                    "sven:Plate", grabbable: true), CounterY);
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

            for (int i = 0; i < tables.Length; i++)
            {
                var table = Semantized(room, $"Table {(char)('A' + i)}", PrimitiveType.Cylinder,
                    tables[i] + new Vector3(0f, 0.38f, 0f), new Vector3(0.85f, 0.38f, 0.85f),
                    new Color(0.45f, 0.32f, 0.24f), "sven:Table", grabbable: false);
                table.isStatic = true;
            }

            // Deux serveurs délibérément identiques : sans une paire indiscernable,
            // la clarification ne se déclenche jamais (§3 du README).
            Semantized(room, "Serveur 1", PrimitiveType.Capsule, new Vector3(-0.7f, 0.85f, 2.6f),
                new Vector3(0.45f, 0.85f, 0.45f), new Color(0.30f, 0.42f, 0.68f),
                "sven:Waiter", grabbable: false);
            Semantized(room, "Serveur 2", PrimitiveType.Capsule, new Vector3(0.7f, 0.85f, 2.6f),
                new Vector3(0.45f, 0.85f, 0.45f), new Color(0.30f, 0.42f, 0.68f),
                "sven:Waiter", grabbable: false);
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

        #region Fabrique d'objets

        /// <summary>
        /// Crée un objet sémantisé complet : forme, annotation SVEN et, si demandé, saisie XR.
        ///
        /// Le montage de la sémantisation reproduit celui des prefabs de fruits existants
        /// (Transform et Renderer en Dynamic, MeshFilter et composant d'annotation en Static),
        /// à une exception près et volontaire : le SemanticAnnotator est en **Dynamic**, pour
        /// que l'ajout d'un état à l'exécution atteigne le graphe (§6.2 du README).
        /// </summary>
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

            if (mesh != null) ReplaceMesh(go, mesh);

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
            // La première pièce est le corps, déjà posée.
            for (int i = 1; i < parts.Count; i++)
            {
                IngredientMeshFactory.Part part = parts[i];
                if (part.Mesh == null) continue;

                var piece = new GameObject(part.Name);
                piece.transform.SetParent(body.transform, false);
                piece.transform.localPosition = part.Position;
                piece.transform.localEulerAngles = part.Rotation;

                piece.AddComponent<MeshFilter>().sharedMesh = part.Mesh;
                piece.AddComponent<MeshRenderer>().sharedMaterial =
                    GetMaterial($"{body.name}{part.Name}", part.Color);
            }
        }

        /// <summary>
        /// Remplace la forme primitive par un mesh généré, et son collider par un MeshCollider
        /// convexe — un collider convexe est exigé dès qu'un Rigidbody est en jeu, et c'est le
        /// cas de tous les ingrédients, qui sont saisissables.
        /// </summary>
        private static void ReplaceMesh(GameObject go, Mesh mesh)
        {
            go.GetComponent<MeshFilter>().sharedMesh = mesh;

            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = true;
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

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Standard")
                            ?? Shader.Find("Diffuse");

            var material = new Material(shader);
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        #endregion
    }
}
