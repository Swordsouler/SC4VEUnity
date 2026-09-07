using System.Collections.Generic;
using UnityEngine;
using Part = Sc4ve.Demonstration.EditorTools.IngredientMeshFactory.Part;

namespace Sc4ve.Demonstration.EditorTools
{
    /// <summary>
    /// Un modèle par recette — l'apparence du plat une fois assemblé.
    ///
    /// Ces modèles ne remplacent PAS le contenu de l'assiette : la conformité continue de se
    /// lire sur les ingrédients qu'elle contient, un par un (§6.3 du README). Le plat est un
    /// habillage, affiché par-dessus quand le contenu satisfait la recette — les ingrédients
    /// restent dans le graphe, ils cessent seulement d'être montrés en vrac.
    ///
    /// C'est ce qui permet d'avoir à la fois la manipulation ingrédient par ingrédient et une
    /// assiette qui ressemble à un plat.
    ///
    /// Les modèles sont dessinés dans un disque de rayon 1 centré à l'origine, prêts à être
    /// posés dans une assiette et remis à son échelle.
    /// </summary>
    public static class DishMeshFactory
    {
        public static readonly string[] Recipes =
        {
            "FruitSalad", "CaesarSalad", "SteakFrites",
            "FishSoup", "PumpkinSoup", "CarrotSoup",
            "SalmonSandwich", "CruditesSandwich", "BeefSandwich",
        };

        public static List<Part> Parts(string recipe) => recipe switch
        {
            // Les SALADES sont des tas de morceaux : ce qu'on voit, c'est un monticule irrégulier
            // dans l'assiette, pas un objet lisse.
            "FruitSalad" => Heap("FruitSalad", 7,
                new Color(0.88f, 0.32f, 0.28f), new Color(0.93f, 0.83f, 0.30f), seed: 3),

            "CaesarSalad" => Heap("CaesarSalad", 8,
                new Color(0.47f, 0.72f, 0.36f), new Color(0.92f, 0.83f, 0.62f), seed: 9),

            // Steak frites : la pièce de viande ENTIÈRE reste lisible, entourée de bâtonnets.
            // Un tas indifférencié perdrait le steak, qui est ce qui nomme le plat.
            "SteakFrites" => SteakAndChips(),

            // Les SOUPES sont une surface liquide, pas un empilement — c'est le seul cas où un
            // modèle apporte vraiment quelque chose que le contenu en vrac ne montre pas.
            // Les trois soupes ne se distinguaient que par une nuance d'orange et portaient la
            // même volute de crème : elles étaient interchangeables. Ce qui nomme une soupe,
            // c'est l'ingrédient qu'on voit flotter dessus — et les trois recettes en ont un
            // qui leur est propre.
            "FishSoup" => Soup("FishSoup", new Color(0.94f, 0.80f, 0.66f),
                garnish: new Color(0.90f, 0.50f, 0.42f), shape: Garnish.Flake, cream: false),

            "PumpkinSoup" => Soup("PumpkinSoup", new Color(0.88f, 0.40f, 0.09f),
                garnish: new Color(0.55f, 0.62f, 0.32f), shape: Garnish.Seed, cream: false),

            "CarrotSoup" => Soup("CarrotSoup", new Color(0.96f, 0.63f, 0.20f),
                garnish: new Color(0.93f, 0.48f, 0.12f), shape: Garnish.Round, cream: false),

            // Les SANDWICHS sont deux tranches de pain avec une garniture visible sur la
            // tranche : c'est l'empilement qui les nomme.
            "SalmonSandwich" => Sandwich("SalmonSandwich", new Color(0.89f, 0.52f, 0.42f)),
            "CruditesSandwich" => Sandwich("CruditesSandwich", new Color(0.52f, 0.74f, 0.38f)),
            "BeefSandwich" => Sandwich("BeefSandwich", new Color(0.56f, 0.22f, 0.18f)),

            _ => new List<Part>(),
        };

        /// <summary>
        /// Monticule de morceaux, deux couleurs alternées. Les morceaux sont irréguliers et se
        /// chevauchent : un empilement régulier se lirait « billes ».
        /// </summary>
        private static List<Part> Heap(string name, int pieces, Color first, Color second, int seed)
        {
            var parts = new List<Part>();

            for (int i = 0; i < pieces; i++)
            {
                float angle = i / (float)pieces * Mathf.PI * 2f + seed;
                float radius = i == 0 ? 0f : Mathf.Lerp(0.18f, 0.55f, (i % 3) / 2f);

                Mesh mesh = i == 0
                    ? IngredientMeshFactory.Blob(1, new Vector3(0.42f, 0.30f, 0.42f), taper: 0.8f,
                        noise: 0.22f, frequency: 3f, seed: seed + i)
                    : null;

                parts.Add(new Part($"Piece{i}", IngredientMeshFactory.Save($"{name}Piece", mesh),
                    i % 2 == 0 ? first : second,
                    new Vector3(Mathf.Cos(angle) * radius, 0.06f + (i % 3) * 0.05f, Mathf.Sin(angle) * radius),
                    new Vector3(i * 37f, i * 53f, i * 29f)));
            }
            return parts;
        }

        /// <summary>
        /// Surface de liquide : un disque plat, pas une boule aplatie.
        ///
        /// La demi-sphère que j'avais faite ressortait comme un galet posé sur l'assiette. Une
        /// soupe se lit à sa SURFACE PLANE affleurant le bord — c'est le seul indice visuel qui
        /// dise « liquide » plutôt que « morceau ». Le trait de crème doit y être posé à plat,
        /// et non flotter au-dessus.
        /// </summary>
        private static List<Part> Soup(string name, Color broth, Color garnish, Garnish shape, bool cream)
        {
            var parts = new List<Part>
            {
                new(name, IngredientMeshFactory.Save($"{name}Broth",
                        IngredientMeshFactory.Cylinder(0.80f, 0.10f, 20)),
                    broth),
            };

            // La volute de crème n'est plus systématique : identique sur les trois, elle
            // contribuait à les confondre au lieu de les distinguer.
            if (cream)
                parts.Add(new Part("Cream", IngredientMeshFactory.Save("SoupCream",
                        IngredientMeshFactory.Arc(0.72f, 0.08f, 0.03f, bow: 0.24f, segments: 10)),
                    new Color(0.97f, 0.94f, 0.88f), new Vector3(0f, 0.045f, 0f)));

            // Cinq morceaux flottants, répartis sans symétrie : c'est eux qui nomment la soupe.
            for (int i = 0; i < 5; i++)
            {
                float angle = i * 2.399f;                     // angle d'or : pas de motif visible
                float radius = 0.10f + 0.22f * (i % 3) / 2f;

                parts.Add(new Part($"Garnish{i}",
                    IngredientMeshFactory.Save($"SoupGarnish{shape}", i == 0 ? GarnishMesh(shape) : null),
                    garnish,
                    new Vector3(Mathf.Cos(angle) * radius, 0.05f, Mathf.Sin(angle) * radius),
                    new Vector3(0f, i * 47f, 0f)));
            }

            return parts;
        }

        /// <summary>Forme des morceaux qui flottent sur une soupe.</summary>
        private enum Garnish
        {
            /// <summary>Rondelle de carotte.</summary>
            Round,
            /// <summary>Miette de poisson, irrégulière.</summary>
            Flake,
            /// <summary>Graine de courge, plate et allongée.</summary>
            Seed,
        }

        private static Mesh GarnishMesh(Garnish shape) => shape switch
        {
            Garnish.Round => IngredientMeshFactory.Cylinder(0.09f, 0.025f, 10),
            Garnish.Flake => IngredientMeshFactory.Blob(1, new Vector3(0.17f, 0.05f, 0.12f),
                taper: 0.6f, noise: 0.25f, frequency: 4f, seed: 13),
            Garnish.Seed => IngredientMeshFactory.Blob(1, new Vector3(0.07f, 0.025f, 0.13f),
                taper: 0.5f, noise: 0.04f, frequency: 3f, seed: 19),
            _ => null,
        };

        /// <summary>Deux tranches de pain, une garniture qui dépasse — la coupe se voit de côté.</summary>
        private static List<Part> Sandwich(string name, Color filling)
        {
            var crust = new Color(0.82f, 0.65f, 0.40f);
            return new List<Part>
            {
                new(name, IngredientMeshFactory.Save("SandwichBottom",
                        IngredientMeshFactory.Blob(1, new Vector3(1.15f, 0.16f, 0.80f), taper: 0.95f,
                            noise: 0.04f, frequency: 3f, seed: 12, boxiness: 0.6f)),
                    crust),

                new("Filling", IngredientMeshFactory.Save($"{name}Filling",
                        IngredientMeshFactory.Blob(1, new Vector3(1.20f, 0.13f, 0.86f), taper: 0.95f,
                            noise: 0.18f, frequency: 5f, seed: 24)),
                    filling, new Vector3(0f, 0.15f, 0f)),

                // Le pain du dessus REPREND le mesh du dessous. Je l'avais demandé sous le nom
                // « SandwichTop », qui n'a jamais été créé : passer null ne récupère un mesh
                // que si l'asset existe DÉJÀ sous ce nom-là. La pièce était donc silencieusement
                // ignorée, et la garniture restait à l'air libre.
                new("Top", IngredientMeshFactory.Save("SandwichBottom", null),
                    crust, new Vector3(0f, 0.28f, 0f)),
            };
        }

        /// <summary>La pièce de viande reste entière et reconnaissable, les frites l'entourent.</summary>
        private static List<Part> SteakAndChips()
        {
            var parts = new List<Part>
            {
                new("SteakFrites", IngredientMeshFactory.Save("DishSteak",
                        IngredientMeshFactory.Slab(new Vector3(1f, 0.22f, 0.72f), taper: 0.88f,
                            noise: 0.04f, seed: 17)),
                    new Color(0.42f, 0.16f, 0.13f), new Vector3(-0.22f, 0.08f, 0f)),
            };

            for (int i = 0; i < 5; i++)
                parts.Add(new Part($"Chip{i}",
                    IngredientMeshFactory.Save("DishChip",
                        i == 0 ? IngredientMeshFactory.Blob(1, new Vector3(0.14f, 0.14f, 0.60f),
                            taper: 0.9f, noise: 0.05f, frequency: 3f, seed: 5, boxiness: 0.8f) : null),
                    new Color(0.90f, 0.72f, 0.34f),
                    new Vector3(0.42f, 0.07f + (i % 2) * 0.09f, -0.22f + i * 0.11f),
                    new Vector3(0f, -18f + i * 12f, (i % 2) * 8f)));

            return parts;
        }
    }
}
