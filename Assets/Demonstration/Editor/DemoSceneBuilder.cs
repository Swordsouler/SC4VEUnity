using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sven.Content;
using Sven.Multimodality;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

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

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.Refresh();

            Debug.Log($"[DemoSceneBuilder] Contenu reconstruit sous « {RootName} » dans {ScenePath}.");
            EditorUtility.DisplayDialog("Terminé",
                $"Contenu reconstruit sous « {RootName} ».\n\n" +
                (rigCreated ? "Un rig XR a été créé.\n\n" : "Rig XR déjà présent, laissé tel quel.\n\n") +
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

            (string semantic, PrimitiveType shape, Vector3 size, Color color, string prefab)[] ingredients =
            {
                ("sven:Apple",   PrimitiveType.Sphere,   new Vector3(0.09f, 0.09f, 0.09f), new Color(0.80f, 0.16f, 0.16f), "Interactable Apple"),
                ("sven:Banana",  PrimitiveType.Capsule,  new Vector3(0.06f, 0.09f, 0.06f), new Color(0.93f, 0.83f, 0.25f), "Interactable Banana"),
                ("sven:Carrot",  PrimitiveType.Cylinder, new Vector3(0.05f, 0.10f, 0.05f), new Color(0.92f, 0.51f, 0.13f), "Interactable Carrot"),
                ("sven:Pumpkin", PrimitiveType.Sphere,   new Vector3(0.16f, 0.13f, 0.16f), new Color(0.88f, 0.45f, 0.10f), "Interactable Pumpkin_C"),
                ("sven:Potato",  PrimitiveType.Sphere,   new Vector3(0.09f, 0.07f, 0.09f), new Color(0.76f, 0.60f, 0.42f), null),
                ("sven:Lettuce", PrimitiveType.Sphere,   new Vector3(0.13f, 0.11f, 0.13f), new Color(0.45f, 0.72f, 0.35f), null),
                ("sven:Tomato",  PrimitiveType.Sphere,   new Vector3(0.08f, 0.08f, 0.08f), new Color(0.85f, 0.18f, 0.15f), null),
                ("sven:Beef",    PrimitiveType.Cube,     new Vector3(0.14f, 0.04f, 0.10f), new Color(0.55f, 0.18f, 0.16f), null),
                ("sven:Chicken", PrimitiveType.Capsule,  new Vector3(0.08f, 0.06f, 0.08f), new Color(0.93f, 0.85f, 0.68f), null),
                ("sven:Salmon",  PrimitiveType.Cube,     new Vector3(0.16f, 0.03f, 0.09f), new Color(0.95f, 0.55f, 0.42f), null),
                ("sven:Cheese",  PrimitiveType.Cube,     new Vector3(0.10f, 0.06f, 0.10f), new Color(0.97f, 0.83f, 0.35f), null),
                ("sven:Bread",   PrimitiveType.Capsule,  new Vector3(0.09f, 0.13f, 0.09f), new Color(0.80f, 0.62f, 0.36f), null),
            };

            const int copies = 2;
            float step = 2.4f / ingredients.Length;
            float x0 = -1.2f + step * 0.5f;

            for (int i = 0; i < ingredients.Length; i++)
            {
                var (semantic, shape, size, color, prefabName) = ingredients[i];
                for (int c = 0; c < copies; c++)
                {
                    var position = new Vector3(x0 + i * step, ShelfY + size.y * 0.5f, 1.05f + c * 0.16f);
                    string label = ShortName(semantic) + " " + (c + 1);

                    GameObject instance = prefabName != null
                        ? InstantiateExistingPrefab(prefabName, label, position, crates)
                        : null;

                    if (instance != null) continue;
                    Semantized(crates, label, shape, position, size, color, semantic, grabbable: true);
                }
            }
        }

        private static void BuildStations(Transform parent)
        {
            var stations = new GameObject("Stations").transform;
            stations.SetParent(parent);

            Semantized(stations, "Planche à découper", PrimitiveType.Cube,
                new Vector3(-0.85f, CounterY + 0.02f, 0.70f), new Vector3(0.40f, 0.04f, 0.30f),
                new Color(0.72f, 0.55f, 0.35f), "sven:CuttingBoard", grabbable: false);

            Semantized(stations, "Plaque de cuisson", PrimitiveType.Cube,
                new Vector3(-0.35f, CounterY + 0.02f, 0.70f), new Vector3(0.36f, 0.04f, 0.30f),
                new Color(0.18f, 0.18f, 0.20f), "sven:Stove", grabbable: false);
        }

        /// <summary>Six assiettes identiques et interchangeables — un seul type de contenant (§5).</summary>
        private static void BuildPlates(Transform parent)
        {
            var plates = new GameObject("Assiettes").transform;
            plates.SetParent(parent);

            for (int i = 0; i < 6; i++)
            {
                var position = new Vector3(0.25f + (i % 3) * 0.26f, CounterY + 0.02f, 0.60f + (i / 3) * 0.26f);
                Semantized(plates, $"Assiette {i + 1}", PrimitiveType.Cylinder, position,
                    new Vector3(0.22f, 0.015f, 0.22f), new Color(0.93f, 0.93f, 0.90f),
                    "sven:Plate", grabbable: true);
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

        /// <summary>
        /// Crée le rig XR s'il manque, et n'y touche pas s'il existe déjà — il vit HORS de la
        /// racine générée, donc il survit aux reconstructions et peut être réglé à la main.
        ///
        /// On délègue la création à Unity plutôt que de reconstruire le rig composant par
        /// composant : c'est la seule façon fiable d'obtenir un XR Origin correct, sa
        /// composition changeant d'une version du toolkit à l'autre.
        /// </summary>
        /// <returns>Vrai si un rig a été créé, faux s'il en existait déjà un.</returns>
        private static bool EnsureXRRig()
        {
            if (UnityEngine.Object.FindAnyObjectByType<XRInteractionManager>() != null &&
                FindRigRoot() != null)
                return false;

            TryExecuteAny(new[] { "GameObject/XR/Interaction Manager" }, out _);

            string[] originPaths =
            {
                "GameObject/XR/XR Origin (VR)",
                "GameObject/XR/XR Origin (Action-based)",
                "GameObject/XR/XR Origin",
            };

            if (!TryExecuteAny(originPaths, out string used))
            {
                Debug.LogWarning(
                    "[DemoSceneBuilder] Impossible de créer le rig XR automatiquement : aucun des " +
                    "menus attendus n'existe dans cette version du XR Interaction Toolkit.\n" +
                    "À faire à la main : GameObject > XR > XR Origin (VR), puis replacer l'origine en (0, 0, 0).");
                return false;
            }

            Debug.Log($"[DemoSceneBuilder] Rig XR créé via « {used} ».");

            GameObject origin = FindRigRoot();
            if (origin != null) origin.transform.position = Vector3.zero;
            return true;
        }

        private static GameObject FindRigRoot()
            => GameObject.Find("XR Origin (VR)") ?? GameObject.Find("XR Origin");

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

        private static bool TryExecuteAny(IEnumerable<string> menuPaths, out string usedPath)
        {
            foreach (string path in menuPaths)
            {
                try
                {
                    if (!EditorApplication.ExecuteMenuItem(path)) continue;
                    usedPath = path;
                    return true;
                }
                catch (Exception)
                {
                    // Menu absent dans cette version : on essaie le suivant.
                }
            }
            usedPath = null;
            return false;
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
                                             string semanticType, bool grabbable)
        {
            GameObject go = GameObject.CreatePrimitive(shape);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.position = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = GetMaterial(ShortName(semanticType), color);

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

            if (grabbable)
            {
                var body = go.AddComponent<Rigidbody>();
                body.useGravity = true;
                var grab = go.AddComponent<XRGrabInteractable>();
                grab.useDynamicAttach = true;
            }

            return go;
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

        private static GameObject InstantiateExistingPrefab(string prefabName, string name,
                                                            Vector3 position, Transform parent)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/Resources/Prefabs/{prefabName}.prefab");
            if (prefab == null) return null;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            instance.transform.position = position;
            return instance;
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
