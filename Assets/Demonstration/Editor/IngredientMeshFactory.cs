using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Sc4ve.Demonstration.EditorTools
{
    /// <summary>
    /// Génère les meshes des huit ingrédients qui n'en ont pas.
    ///
    /// Pourquoi générer plutôt que télécharger : les meshes existants (pomme, banane, carotte,
    /// citrouille) font 44 à 62 Ko, soit quelques centaines de triangles à facettes visibles.
    /// Cette esthétique low-poly se reproduit exactement par code, sans dépendre d'un asset
    /// externe, de sa licence ni de sa cohérence stylistique.
    ///
    /// PRINCIPE : un aliment se reconnaît à sa SILHOUETTE, pas à sa couleur ni à ses bosses.
    /// Une sphère déformée reste un galet, qu'on la peigne en rose ou en beige. Chaque
    /// ingrédient a donc un trait distinctif — l'os du pilon, les veines de gras du pavé de
    /// saumon, le pédoncule de la tomate, les grignes de la baguette — porté par des pièces
    /// annexes.
    /// </summary>
    public static class IngredientMeshFactory
    {
        internal const string MeshesPath = "Assets/Demonstration/Meshes";

        private static readonly string[] IngredientNames =
        {
            "Potato", "Lettuce", "Tomato", "Beef", "Chicken", "Salmon", "Cheese", "Bread",
        };

        /// <summary>
        /// Une pièce d'un ingrédient. La première est le corps et vit sur l'objet lui-même ;
        /// les suivantes sont les traits distinctifs, portés par des enfants et gardant leur
        /// propre couleur.
        /// </summary>
        public readonly struct Part
        {
            public readonly string Name;
            public readonly Mesh Mesh;
            public readonly Color Color;
            public readonly Vector3 Position;
            public readonly Vector3 Rotation;

            public Part(string name, Mesh mesh, Color color, Vector3 position = default, Vector3 rotation = default)
            {
                Name = name;
                Mesh = mesh;
                Color = color;
                Position = position;
                Rotation = rotation;
            }
        }

        [MenuItem("SC4VE/Démonstration/3 — Générer les meshes des ingrédients", priority = 3)]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(MeshesPath);
            foreach (string name in IngredientNames) Parts(name);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Terminé",
                $"Meshes disponibles dans {MeshesPath}.\n\n" +
                "Relancer « 1 — (Re)construire » pour que les prefabs les utilisent.",
                "OK");
        }

        /// <summary>Le corps de l'ingrédient, ou null s'il n'en existe pas.</summary>
        public static Mesh Generate(string name)
        {
            List<Part> parts = Parts(name);
            return parts.Count > 0 ? parts[0].Mesh : null;
        }

        /// <summary>
        /// La version découpée d'un ingrédient : un vrai modèle en tranches, et non l'objet
        /// entier aplati.
        ///
        /// Existe pour TOUS les aliments, y compris les quatre qui ont un mesh importé : une
        /// pomme coupée doit ressembler à des quartiers, pas à une pomme écrasée. La forme des
        /// tranches vient des proportions de l'aliment, pas de son mesh.
        /// </summary>
        public static Mesh Sliced(string name, Vector3 proportions, int seed)
            => Save($"{name}Sliced", Sliced(proportions, count: 4, seed));

        /// <summary>
        /// Toutes les pièces d'un ingrédient : le corps d'abord, puis ses traits distinctifs.
        /// </summary>
        public static List<Part> Parts(string name)
        {
            var parts = new List<Part>();

            switch (name)
            {
                // Tubercule allongé et bosselé. Sa forme irrégulière suffit à le distinguer :
                // c'est le seul aliment dont l'irrégularité EST le trait caractéristique.
                case "Potato":
                    // Trop ronde, elle se lisait « petit pain » : nettement allongée et bosselée.
                    parts.Add(new Part(name, Save(name,
                        Blob(1, new Vector3(0.52f, 0.46f, 1f), taper: 0.70f, noise: 0.22f, frequency: 2.6f, seed: 11)),
                        new Color(0.76f, 0.60f, 0.42f)));
                    break;

                // Boule de feuilles : le corps froissé, plus deux feuilles externes qui
                // débordent — sans elles, ce n'est qu'un ballon vert.
                case "Lettuce":
                    parts.Add(new Part(name, Save(name,
                        Blob(2, new Vector3(1f, 0.85f, 1f), taper: 1f, noise: 0.17f, frequency: 5f, seed: 23)),
                        new Color(0.45f, 0.72f, 0.35f)));
                    parts.Add(new Part("Leaf1", Save("LettuceLeaf",
                        Blob(1, new Vector3(1f, 0.12f, 0.9f), taper: 0.4f, noise: 0.20f, frequency: 4f, seed: 31)),
                        new Color(0.38f, 0.63f, 0.28f), new Vector3(0.18f, -0.28f, 0f), new Vector3(0f, 0f, 18f)));
                    parts.Add(new Part("Leaf2", Save("LettuceLeaf", null),
                        new Color(0.40f, 0.67f, 0.30f), new Vector3(-0.16f, -0.30f, 0.10f), new Vector3(0f, 140f, -14f)));
                    break;

                // Sphère un peu aplatie + pédoncule vert en étoile : c'est l'étoile qui fait
                // lire « tomate » plutôt que « boule rouge ».
                case "Tomato":
                    parts.Add(new Part(name, Save(name,
                        Blob(1, new Vector3(1f, 0.82f, 1f), taper: 1f, noise: 0.035f, frequency: 3f, seed: 5)),
                        new Color(0.85f, 0.18f, 0.15f)));
                    parts.Add(new Part("Calyx", Save("TomatoCalyx", Star(5, 0.12f, 0.34f, 0.05f)),
                        new Color(0.30f, 0.55f, 0.22f), new Vector3(0f, 0.40f, 0f)));
                    parts.Add(new Part("Stem", Save("TomatoStem", Cylinder(0.05f, 0.14f, 6)),
                        new Color(0.32f, 0.50f, 0.20f), new Vector3(0f, 0.46f, 0f)));
                    break;

                // Steak : dalle ovale + bande de gras claire le long d'un bord. L'os que
                // j'avais mis d'abord ressortait sous la dalle et faisait lire « champignon » ;
                // le liseré de gras est le vrai marqueur visuel d'une pièce de viande.
                case "Beef":
                    parts.Add(new Part(name, Save(name,
                        Slab(new Vector3(1f, 0.19f, 0.74f), taper: 0.88f, noise: 0.035f, seed: 17)),
                        new Color(0.55f, 0.18f, 0.16f)));
                    parts.Add(new Part("Fat", Save("BeefFat",
                        Blob(1, new Vector3(0.92f, 0.17f, 0.16f), taper: 0.55f, noise: 0.05f, frequency: 3f, seed: 19)),
                        new Color(0.95f, 0.90f, 0.78f), new Vector3(0f, 0.005f, 0.31f)));
                    break;

                // Pilon : bulbe charnu + os qui dépasse. La silhouette est reconnaissable
                // même en ombre chinoise, ce qu'un blanc de poulet n'est jamais.
                case "Chicken":
                    parts.Add(new Part(name, Save(name,
                        Blob(1, new Vector3(0.78f, 0.78f, 1f), taper: 0.32f, noise: 0.07f, frequency: 2.5f, seed: 37)),
                        new Color(0.90f, 0.76f, 0.55f)));
                    parts.Add(new Part("Bone", Save("ChickenBone", Cylinder(0.055f, 0.42f, 6)),
                        new Color(0.96f, 0.94f, 0.89f), new Vector3(0f, 0f, -0.56f), new Vector3(90f, 0f, 0f)));
                    parts.Add(new Part("Knuckle", Save("ChickenKnuckle", Blob(0, Vector3.one * 0.16f, 1f, 0f, 1f, 0)),
                        new Color(0.96f, 0.94f, 0.89f), new Vector3(0f, 0f, -0.74f)));
                    break;

                case "Salmon":
                    // PAVÉ, pas poisson entier. La silhouette d'un poisson ne survit pas à la
                    // réduction low-poly — elle se lit « galette rose ». Le pavé, lui, porte
                    // deux marqueurs que rien d'autre n'a : les lignes de gras BLANCHES en
                    // travers de la chair, et la peau sombre sur une tranche.
                    // Forme CUBIQUE à arêtes chanfreinées : un pavé est une pièce coupée au
                    // couteau, pas un galet. L'ellipsoïde le faisait lire « boule rose », et
                    // son dos bombé empêchait les veines de se poser à plat.
                    parts.Add(new Part(name, Save(name,
                        Blob(2, new Vector3(0.62f, 0.42f, 1f), taper: 0.90f, noise: 0.02f,
                             frequency: 3f, seed: 29, boxiness: 0.85f)),
                        new Color(0.91f, 0.53f, 0.41f)));

                    // La peau EN DESSOUS, débordant légèrement : posée sur la tranche, elle
                    // sortait du corps par les côtés, celui-ci se rétrécissant vers l'arrière.
                    // Sous le pavé, elle affleure tout autour comme un liseré sombre.
                    parts.Add(new Part("Skin", Save("SalmonSkin",
                        Blob(2, new Vector3(0.64f, 0.09f, 1.02f), taper: 0.90f, noise: 0.01f,
                             frequency: 3f, seed: 47, boxiness: 0.85f)),
                        new Color(0.30f, 0.33f, 0.38f), new Vector3(0f, -0.16f, 0f)));

                    // Trois veines de gras en travers : c'est LA signature du saumon. Le dessus
                    // du pavé étant désormais plat, elles n'ont presque plus besoin de fléchir.
                    for (int i = 0; i < 3; i++)
                        parts.Add(new Part($"Vein{i}", Save("SalmonVein",
                            i == 0 ? Arc(0.44f, 0.034f, 0.05f, bow: 0.04f, segments: 6, sag: 0.02f) : null),
                            new Color(0.99f, 0.93f, 0.88f),
                            new Vector3(0f, 0.185f, -0.26f + i * 0.24f)));
                    break;

                // La part de fromage est déjà lisible à sa seule silhouette : arêtes franches,
                // pas de bruit — c'est le seul ingrédient purement analytique.
                case "Cheese":
                    parts.Add(new Part(name, Save(name, Wedge(new Vector3(1f, 0.55f, 0.85f))),
                        new Color(0.97f, 0.83f, 0.35f)));
                    break;

                // Baguette : corps allongé à bouts ARRONDIS (des bouts pointus donnent un
                // ballon de rugby) et trois grignes en travers.
                case "Bread":
                    // Les grignes faisaient 0,30 de large pour un corps de 0,32 : rotation
                    // comprise, elles débordaient de partout et donnaient une barque à
                    // barreaux. Réduites et ENFONCÉES dans la mie, elles redeviennent des
                    // entailles.
                    parts.Add(new Part(name, Save(name,
                        Blob(1, new Vector3(0.32f, 0.36f, 1f), taper: 0.90f, noise: 0.05f, frequency: 3.5f, seed: 43)),
                        new Color(0.80f, 0.62f, 0.36f)));
                    // Les grignes sont ENFONCÉES sous la surface : y = 0,10 alors que le dessus
                    // du corps culmine à 0,18 au centre et descend vers les bouts (c'est un
                    // ellipsoïde). Placées à 0,145 elles flottaient au-dessus dès qu'on
                    // s'éloignait du milieu. À cette hauteur, seule leur arête supérieure
                    // affleure — c'est ce qu'on veut d'une entaille.
                    // Grignes COURBES : une barre droite se lit « rectangle collé dessus ».
                    //
                    // Leur hauteur SUIT le profil du corps au lieu d'être commune aux trois.
                    // Le dos de la baguette culmine à 0,18 au centre et redescend vers les
                    // bouts ; à hauteur fixe, celles des extrémités s'enterraient — d'où les
                    // deux grignes visibles sur trois.
                    for (int i = 0; i < 3; i++)
                    {
                        float z = -0.24f + i * 0.24f;
                        float crust = 0.18f * Mathf.Sqrt(1f - Mathf.Pow(z / 0.5f, 2f));

                        parts.Add(new Part($"Score{i}", Save("BreadScore",
                            i == 0 ? Arc(0.19f, 0.030f, 0.06f, bow: 0.035f, segments: 6, sag: 0.045f) : null),
                            new Color(0.60f, 0.42f, 0.22f),
                            new Vector3(0f, crust - 0.025f, z), new Vector3(0f, 32f, 0f)));
                    }
                    break;
            }

            return parts;
        }

        /// <summary>
        /// Enregistre le mesh comme asset, ou renvoie celui déjà là. Passer null récupère
        /// simplement un mesh déjà généré — utile quand plusieurs pièces partagent la même forme
        /// (les feuilles de laitue, les grignes du pain).
        /// </summary>
        internal static Mesh Save(string name, Mesh mesh)
        {
            string path = $"{MeshesPath}/{name}.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);

            if (mesh == null) return existing;

            mesh.name = name;

            // Un mesh déjà là est ÉCRASÉ SUR PLACE plutôt que supprimé puis recréé : détruire
            // l'asset lui donnerait un nouveau GUID et casserait toutes les références.
            //
            // La réécriture passe par l'API Mesh et NON par EditorUtility.CopySerialized :
            // celui-ci ne recopie pas fiablement les buffers de sommets et d'indices, et
            // l'ancienne forme survivait en silence — c'est ce qui a laissé les grignes du pain
            // en rectangles alors que le code produisait déjà des arcs.
            if (existing != null)
            {
                existing.Clear();
                existing.vertices = mesh.vertices;
                existing.triangles = mesh.triangles;
                existing.RecalculateNormals();
                existing.RecalculateBounds();
                EditorUtility.SetDirty(existing);
                return existing;
            }

            Directory.CreateDirectory(MeshesPath);
            AssetDatabase.CreateAsset(mesh, path);
            Debug.Log($"[MeshFactory] {path} ({mesh.triangles.Length / 3} triangles).");
            return mesh;
        }

        #region Formes

        /// <summary>
        /// Icosphère déformée, éventuellement effilée d'un bout. Le paramètre taper est ce qui
        /// distingue un corps de poisson d'une pomme de terre : sans lui, tout est ovoïde.
        /// </summary>
        /// <param name="boxiness">
        /// 0 = ellipsoïde, 1 = cube. Entre les deux, un pavé aux arêtes chanfreinées : chaque
        /// direction est projetée sur le cube unité (division par sa plus grande composante)
        /// puis mélangée à sa projection sphérique. La topologie de l'icosphère est conservée,
        /// donc le nombre de facettes ne bouge pas.
        /// </param>
        internal static Mesh Blob(int subdivisions, Vector3 scale, float taper, float noise,
                                 float frequency, int seed, float boxiness = 0f)
        {
            Icosphere(subdivisions, out List<Vector3> vertices, out List<int> triangles);

            Vector2 offset = Offset(seed);
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 direction = vertices[i];

                if (boxiness > 0f)
                {
                    float largest = Mathf.Max(Mathf.Abs(direction.x),
                        Mathf.Max(Mathf.Abs(direction.y), Mathf.Abs(direction.z)));
                    if (largest > Mathf.Epsilon)
                        direction = Vector3.Lerp(direction, direction / largest, boxiness);
                }

                // Rétrécit progressivement vers -Z.
                float t = Mathf.InverseLerp(1f, -1f, direction.z);
                float width = Mathf.Lerp(1f, taper, t);

                float displacement = noise > 0f
                    ? 1f + noise * (Sample(direction, frequency, offset) - 0.5f) * 2f
                    : 1f;

                Vector3 shaped = new(direction.x * width, direction.y * width, direction.z);
                vertices[i] = Vector3.Scale(shaped * displacement, scale) * 0.5f;
            }

            return Faceted(vertices, triangles);
        }

        /// <summary>
        /// La version DÉCOUPÉE d'un aliment : plusieurs tranches fines posées en éventail.
        ///
        /// Un simple aplatissement de l'objet entier ne se lit pas « coupé » — il se lit
        /// « écrasé ». Ce qui fait la découpe, ce sont les tranches séparées et les faces de
        /// coupe visibles entre elles.
        /// </summary>
        private static Mesh Sliced(Vector3 scale, int count, int seed)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            // On tranche PERPENDICULAIREMENT au plus grand axe, quel qu'il soit — on coupe une
            // carotte en rondelles, pas en bâtonnets. L'épaisseur se déduisait auparavant de Y,
            // ce qui donnait des cylindres pour tout ce qui est long en hauteur : la carotte
            // ressortait avec une épaisseur (0,149) presque égale à son diamètre (0,194).
            float longest = Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
            float across = (scale.x + scale.y + scale.z - longest) * 0.5f;

            float diameter = across * 0.95f;
            float thickness = Mathf.Max(longest * 0.5f / count, diameter * 0.12f);
            float spread = longest * 0.90f;

            for (int s = 0; s < count; s++)
            {
                float t = count == 1 ? 0.5f : s / (float)(count - 1);

                // Les tranches des extrémités sont plus petites : elles viennent des bouts
                // arrondis de l'aliment, pas de son milieu.
                float shrink = Mathf.Lerp(0.68f, 1f, 1f - Mathf.Abs(2f * t - 1f));

                Icosphere(1, out List<Vector3> slice, out List<int> sliceTriangles);

                Vector2 offset = Offset(seed + s);
                var size = new Vector3(diameter * shrink, thickness, diameter * shrink);

                // Chaque tranche est légèrement inclinée et décalée : un empilement trop
                // régulier se lit « rondelles de plastique ».
                Quaternion tilt = Quaternion.Euler(
                    (s % 2 == 0 ? 5f : -4f), (s - (count - 1) * 0.5f) * 9f, (s % 3 - 1) * 4f);
                var position = new Vector3((t - 0.5f) * spread, thickness * 0.5f, (s % 2 - 0.5f) * thickness * 0.6f);

                int start = vertices.Count;
                foreach (Vector3 direction in slice)
                {
                    float displacement = 1f + 0.06f * (Sample(direction, 3f, offset) - 0.5f) * 2f;
                    vertices.Add(position + tilt * Vector3.Scale(direction * displacement, size) * 0.5f);
                }
                foreach (int index in sliceTriangles) triangles.Add(start + index);
            }

            return Faceted(vertices, triangles);
        }

        /// <summary>Dalle à bords adoucis : une icosphère très aplatie, plus crédible qu'un cube.</summary>
        internal static Mesh Slab(Vector3 scale, float taper, float noise, int seed)
            => Blob(1, scale, taper, noise, 2.5f, seed);

        /// <summary>Part de fromage : prisme triangulaire, arêtes franches.</summary>
        internal static Mesh Wedge(Vector3 scale)
        {
            float x = scale.x * 0.5f, y = scale.y * 0.5f, z = scale.z * 0.5f;

            var vertices = new List<Vector3>
            {
                new(-x, -y, -z), new(x, -y, -z), new(-x, y, -z),
                new(-x, -y,  z), new(x, -y,  z), new(-x, y,  z),
            };
            var triangles = new List<int>
            {
                0, 2, 1,  3, 4, 5,
                0, 1, 4, 0, 4, 3,
                0, 3, 5, 0, 5, 2,
                1, 2, 5, 1, 5, 4,
            };
            return Faceted(vertices, triangles);
        }

        /// <summary>
        /// Ruban courbe : une barre le long de X, bombée en Z selon une parabole, extrudée en Y.
        ///
        /// Sert aux grignes de la baguette et aux lignes de gras du pavé de saumon. Dans les
        /// deux cas une barre droite se lit « rectangle collé dessus » ; c'est la courbure qui
        /// la fait passer pour une entaille ou une veine.
        /// </summary>
        /// <param name="bow">Flèche horizontale de l'arc. 0 donne une barre droite.</param>
        /// <param name="sag">
        /// Abaissement des extrémités. Indispensable dès que le ruban se pose sur une surface
        /// bombée : un ruban plat sur un dos rond touche au milieu et décolle aux bouts. C'est
        /// ce qui faisait flotter les veines du pavé de saumon.
        /// </param>
        internal static Mesh Arc(float length, float width, float thickness, float bow, int segments, float sag = 0f)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            float halfThickness = thickness * 0.5f, halfWidth = width * 0.5f;

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float away = Mathf.Pow(2f * t - 1f, 2f);

                float x = (t - 0.5f) * length;
                float z = bow * (1f - away);
                float y = -sag * away;

                vertices.Add(new Vector3(x, y + halfThickness, z - halfWidth));
                vertices.Add(new Vector3(x, y + halfThickness, z + halfWidth));
                vertices.Add(new Vector3(x, y - halfThickness, z + halfWidth));
                vertices.Add(new Vector3(x, y - halfThickness, z - halfWidth));
            }

            for (int i = 0; i < segments; i++)
            {
                int a = i * 4, b = (i + 1) * 4;
                for (int k = 0; k < 4; k++)
                {
                    int next = (k + 1) % 4;
                    triangles.AddRange(new[] { a + k, a + next, b + next, a + k, b + next, b + k });
                }
            }

            int last = segments * 4;
            triangles.AddRange(new[] { 0, 1, 2, 0, 2, 3 });
            triangles.AddRange(new[] { last, last + 3, last + 2, last, last + 2, last + 1 });

            return Faceted(vertices, triangles);
        }

        /// <summary>Cylindre à faible nombre de côtés — os, pédoncule.</summary>
        internal static Mesh Cylinder(float radius, float height, int sides)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            float half = height * 0.5f;

            for (int i = 0; i < sides; i++)
            {
                float angle = i / (float)sides * Mathf.PI * 2f;
                vertices.Add(new Vector3(Mathf.Cos(angle) * radius, -half, Mathf.Sin(angle) * radius));
                vertices.Add(new Vector3(Mathf.Cos(angle) * radius, half, Mathf.Sin(angle) * radius));
            }
            int bottom = vertices.Count; vertices.Add(new Vector3(0f, -half, 0f));
            int top = vertices.Count; vertices.Add(new Vector3(0f, half, 0f));

            for (int i = 0; i < sides; i++)
            {
                int a = i * 2, b = a + 1;
                int c = (i + 1) % sides * 2, d = c + 1;

                triangles.AddRange(new[] { a, b, d, a, d, c });

                // Sens des couvercles : un éventail (centre, suivant, courant) regarde vers
                // +Y ; (centre, courant, suivant) vers −Y. Je les avais intervertis, et les
                // deux faces se retrouvaient tournées vers l'intérieur — d'où les dessus noirs
                // et les disques troués, qu'on prend facilement pour un défaut d'UV alors
                // qu'aucun de ces meshes n'a la moindre coordonnée de texture.
                triangles.AddRange(new[] { bottom, a, c });
                triangles.AddRange(new[] { top, d, b });
            }
            return Faceted(vertices, triangles);
        }

        /// <summary>
        /// Étoile plate à N branches, légèrement épaisse. Sert au pédoncule de la tomate et,
        /// à deux branches, à la nageoire caudale du poisson.
        /// </summary>
        internal static Mesh Star(int points, float innerRadius, float outerRadius, float thickness)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            float half = thickness * 0.5f;

            int centreTop = vertices.Count; vertices.Add(new Vector3(0f, half, 0f));
            int centreBottom = vertices.Count; vertices.Add(new Vector3(0f, -half, 0f));

            int count = points * 2;
            for (int i = 0; i < count; i++)
            {
                float angle = i / (float)count * Mathf.PI * 2f;
                float radius = i % 2 == 0 ? outerRadius : innerRadius;
                vertices.Add(new Vector3(Mathf.Cos(angle) * radius, half, Mathf.Sin(angle) * radius));
                vertices.Add(new Vector3(Mathf.Cos(angle) * radius, -half, Mathf.Sin(angle) * radius));
            }

            for (int i = 0; i < count; i++)
            {
                int a = 2 + i * 2, b = a + 1;
                int c = 2 + (i + 1) % count * 2, d = c + 1;

                triangles.AddRange(new[] { centreTop, a, c });
                triangles.AddRange(new[] { centreBottom, d, b });
                triangles.AddRange(new[] { a, b, d, a, d, c });
            }
            return Faceted(vertices, triangles);
        }

        #endregion

        #region Géométrie

        /// <summary>
        /// Icosaèdre subdivisé : 20 triangles à la subdivision 0, 80 à 1, 320 à 2.
        /// Préféré à la sphère UV d'Unity, dont les pôles donnent des facettes irrégulières.
        /// </summary>
        internal static void Icosphere(int subdivisions, out List<Vector3> vertices, out List<int> triangles)
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;

            vertices = new List<Vector3>
            {
                new(-1,  t, 0), new( 1,  t, 0), new(-1, -t, 0), new( 1, -t, 0),
                new( 0, -1, t), new( 0,  1, t), new( 0, -1, -t), new( 0,  1, -t),
                new( t,  0, -1), new( t, 0,  1), new(-t,  0, -1), new(-t,  0, 1),
            };
            for (int i = 0; i < vertices.Count; i++) vertices[i] = vertices[i].normalized;

            triangles = new List<int>
            {
                0, 11, 5,  0, 5, 1,   0, 1, 7,   0, 7, 10,  0, 10, 11,
                1, 5, 9,   5, 11, 4,  11, 10, 2, 10, 7, 6,  7, 1, 8,
                3, 9, 4,   3, 4, 2,   3, 2, 6,   3, 6, 8,   3, 8, 9,
                4, 9, 5,   2, 4, 11,  6, 2, 10,  8, 6, 7,   9, 8, 1,
            };

            for (int pass = 0; pass < subdivisions; pass++)
            {
                var midpoints = new Dictionary<long, int>();
                var refined = new List<int>(triangles.Count * 4);

                for (int i = 0; i < triangles.Count; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    int ab = Midpoint(a, b, vertices, midpoints);
                    int bc = Midpoint(b, c, vertices, midpoints);
                    int ca = Midpoint(c, a, vertices, midpoints);

                    refined.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                triangles = refined;
            }
        }

        /// <summary>
        /// Milieu d'arête, projeté sur la sphère et MUTUALISÉ entre les triangles voisins :
        /// sans ce partage, la déformation écarterait les facettes et ouvrirait des trous.
        /// </summary>
        private static int Midpoint(int a, int b, List<Vector3> vertices, Dictionary<long, int> cache)
        {
            long key = a < b ? ((long)a << 32) + b : ((long)b << 32) + a;
            if (cache.TryGetValue(key, out int existing)) return existing;

            vertices.Add(((vertices[a] + vertices[b]) * 0.5f).normalized);
            cache[key] = vertices.Count - 1;
            return vertices.Count - 1;
        }

        /// <summary>
        /// Duplique les sommets par triangle pour que chaque facette ait sa propre normale.
        /// C'est ce dédoublement, et non le nombre de triangles, qui donne l'aspect facetté.
        /// </summary>
        internal static Mesh Faceted(List<Vector3> vertices, List<int> triangles)
        {
            var flatVertices = new Vector3[triangles.Count];
            var flatTriangles = new int[triangles.Count];

            for (int i = 0; i < triangles.Count; i++)
            {
                flatVertices[i] = vertices[triangles[i]];
                flatTriangles[i] = i;
            }

            var mesh = new Mesh { vertices = flatVertices, triangles = flatTriangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Décalage de bruit dérivé d'une graine, RAMENÉ dans une plage raisonnable.
        ///
        /// Mathf.PerlinNoise indexe une table après un Mathf.FloorToInt : au-delà de deux
        /// milliards, la conversion déborde et renvoie n'importe quoi. Une graine issue de
        /// GetHashCode() suffit à provoquer ce débordement — les tranches sont sorties un
        /// milliard de fois trop grandes, donc invisibles hors champ.
        /// </summary>
        private static Vector2 Offset(int seed)
        {
            int bounded = Mathf.Abs(seed) % 997;
            return new Vector2(bounded * 7.13f, bounded * 3.71f);
        }

        /// <summary>Bruit déterministe : même graine, même forme à chaque génération.</summary>
        private static float Sample(Vector3 direction, float frequency, Vector2 offset)
        {
            float a = Mathf.PerlinNoise(direction.x * frequency + offset.x, direction.y * frequency + offset.y);
            float b = Mathf.PerlinNoise(direction.y * frequency + offset.y, direction.z * frequency + offset.x);
            return (a + b) * 0.5f;
        }

        #endregion
    }
}
