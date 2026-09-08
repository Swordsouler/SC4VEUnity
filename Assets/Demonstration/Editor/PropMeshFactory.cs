using System.Collections.Generic;
using UnityEngine;
using Part = Sc4ve.Demonstration.EditorTools.IngredientMeshFactory.Part;

namespace Sc4ve.Demonstration.EditorTools
{
    /// <summary>
    /// Génère les meshes du mobilier et des personnages : table, plaque, assiette, poubelle,
    /// serveur, client, couteau.
    ///
    /// Même principe que pour les aliments — la reconnaissance passe par la silhouette — mais
    /// l'enjeu diffère : ces objets n'ont pas à être identifiés parmi douze voisins, ils ont à
    /// ne pas ressembler à des primitives. Une capsule bleue ne se lit pas « serveur ».
    ///
    /// Les primitives géométriques sont partagées avec IngredientMeshFactory : les dupliquer
    /// aurait fait diverger deux jeux de formes censés produire le même style.
    /// </summary>
    public static class PropMeshFactory
    {
        /// <summary>Les pièces d'un prop : la première est le corps, les suivantes des enfants.</summary>
        public static List<Part> Parts(string prop) => prop switch
        {
            "Plate" => Plate(),
            "Bin" => Bin(),
            "Stove" => Stove(),
            "CuttingBoard" => CuttingBoard(),
            "Table" => Table(),
            "Waiter" => Person("Waiter", new Color(0.30f, 0.42f, 0.68f), apron: true),
            "Customer" => SeatedPerson("Customer", new Color(0.72f, 0.45f, 0.35f)),
            "Knife" => Knife(),
            _ => new List<Part>(),
        };

        /// <summary>
        /// Assiette creuse : un fond fin et un marli incliné. La rondelle épaisse de 1,5 cm
        /// qu'on avait avant se lisait « palet », et rien n'y montrait qu'on pouvait y poser
        /// quelque chose.
        /// </summary>
        private static List<Part> Plate()
        {
            var white = new Color(0.94f, 0.94f, 0.92f);

            // Une assiette est un récipient creux et évasé, donc la même forme qu'une poubelle
            // en beaucoup plus plat. La couronne que j'avais écrite pour elle produisait une
            // géométrie bancale — un fond en étoile et un bord troué — et ne servait plus
            // qu'ici : elle est supprimée.
            return new List<Part>
            {
                new("Plate", Save("PropPlate", Hollow(bottom: 0.58f, top: 1f, height: 0.20f,
                        wall: 0.045f, sides: 18)), white),
                new("Base", Save("PropPlateBase", IngredientMeshFactory.Cylinder(0.30f, 0.03f, 18)),
                    white, new Vector3(0f, -0.11f, 0f)),
            };
        }

        /// <summary>
        /// Poubelle CREUSE : paroi extérieure évasée, paroi intérieure, fond. Un tube plein
        /// fermé par un couvercle ne se lit pas « poubelle » — c'est le vide qu'on voit d'en
        /// haut qui dit qu'on peut y jeter quelque chose.
        /// </summary>
        private static List<Part> Bin()
        {
            var body = new Color(0.24f, 0.26f, 0.29f);

            // Aucun couvercle ni bandeau au bord : le rebord que j'avais ajouté se lisait
            // comme un couvercle posé dessus, et fermait visuellement ce qu'on veut ouvert.
            return new List<Part>
            {
                new("Bin", Save("PropBin", Hollow(bottom: 0.38f, top: 0.52f, height: 1f,
                        wall: 0.035f, sides: 14)), body),
            };
        }

        /// <summary>
        /// Plaque de cuisson : plan sombre + deux foyers et deux boutons. Sans les foyers, le
        /// cube noir se confond avec la planche à découper.
        /// </summary>
        private static List<Part> Stove()
        {
            var slate = new Color(0.17f, 0.17f, 0.19f);
            var burner = new Color(0.45f, 0.16f, 0.12f);
            var knob = new Color(0.62f, 0.62f, 0.64f);

            var parts = new List<Part>
            {
                new("Stove", Save("PropStove", Box(new Vector3(1f, 0.10f, 0.75f))), slate),
            };

            // Foyers : de simples disques plats. J'avais réutilisé la couronne creuse de
            // l'assiette, dont la paroi inclinée et le fond n'ont aucun sens sur une plaque et
            // produisaient des reliefs incompréhensibles.
            for (int i = 0; i < 2; i++)
                parts.Add(new Part($"Burner{i}", Save("PropStoveBurner",
                        i == 0 ? IngredientMeshFactory.Cylinder(0.15f, 0.02f, 14) : null),
                    burner, new Vector3(-0.22f + i * 0.44f, 0.055f, 0.06f)));

            for (int i = 0; i < 2; i++)
                parts.Add(new Part($"Knob{i}", Save("PropStoveKnob",
                        i == 0 ? IngredientMeshFactory.Cylinder(0.035f, 0.04f, 8) : null),
                    knob, new Vector3(-0.18f + i * 0.36f, 0.06f, -0.30f)));

            return parts;
        }

        /// <summary>Planche : dalle de bois + poignée percée, qui la distingue de la plaque.</summary>
        private static List<Part> CuttingBoard()
        {
            var wood = new Color(0.74f, 0.57f, 0.36f);
            return new List<Part>
            {
                new("CuttingBoard", Save("PropBoard", Box(new Vector3(1f, 0.07f, 0.72f))), wood),
                new("Handle", Save("PropBoardHandle", Box(new Vector3(0.30f, 0.06f, 0.26f))),
                    wood, new Vector3(0.62f, 0f, 0f)),
            };
        }

        /// <summary>
        /// Table de bistrot : plateau, pied central, socle. Le cylindre unique qu'on avait
        /// ressemblait à un tabouret et ne laissait pas voir qu'on peut s'y asseoir autour.
        /// </summary>
        private static List<Part> Table()
        {
            var wood = new Color(0.47f, 0.33f, 0.24f);
            var metal = new Color(0.35f, 0.35f, 0.37f);

            // Le PLATEAU est à l'origine et tout se décrit en dessous de lui. Décrit à 0,48
            // avec un pied à 0,01, le pied traversait le plateau : la position du corps était
            // ignorée, seules celles des pièces annexes comptaient.
            return new List<Part>
            {
                new("Table", Save("PropTableTop", IngredientMeshFactory.Cylinder(0.50f, 0.05f, 16)),
                    wood),
                new("Pillar", Save("PropTablePillar", IngredientMeshFactory.Cylinder(0.05f, 0.88f, 8)),
                    metal, new Vector3(0f, -0.46f, 0f)),
                new("Foot", Save("PropTableFoot", Tube(bottom: 0.34f, top: 0.24f, height: 0.06f, sides: 12)),
                    metal, new Vector3(0f, -0.90f, 0f)),
            };
        }

        /// <summary>
        /// Personnage : buste, tête, bras. Volontairement schématique — un pantin lisible vaut
        /// mieux qu'un humanoïde raté, et les deux serveurs doivent rester indiscernables l'un
        /// de l'autre (§3 du README).
        ///
        /// Le tablier distingue le SERVEUR du client : c'est la seule différence, et elle tient
        /// au rôle, pas à l'individu.
        /// </summary>
        private static List<Part> Person(string name, Color cloth, bool apron)
        {
            var skin = new Color(0.86f, 0.70f, 0.56f);

            // Proportions humaines : la tête fait un septième de la hauteur, pas un tiers.
            // Trop grosse, elle donnait un bonhomme de dessin animé ; et des bras aussi longs
            // que le torse ressortaient en pales.
            var parts = new List<Part>
            {
                new(name, Save($"PropBody{name}",
                        IngredientMeshFactory.Blob(2, new Vector3(0.44f, 0.90f, 0.28f), taper: 0.72f,
                            noise: 0.02f, frequency: 2f, seed: 61, boxiness: 0.35f)),
                    cloth),
                // Tête et cou ENFONCÉS dans le buste. La tête était à 0,60 pour un buste
                // s'arrêtant à 0,45 — et au sommet d'un ellipsoïde la largeur tend vers zéro,
                // si bien que même un contact exact n'aurait donné qu'un point. Elle descend à
                // 0,50, où le buste mesure encore une douzaine de centimètres de large, et le
                // cou est allongé pour couvrir toute la jonction.
                new("Head", Save("PropHead",
                        IngredientMeshFactory.Blob(2, new Vector3(0.24f, 0.28f, 0.24f), taper: 1f,
                            noise: 0.02f, frequency: 3f, seed: 67, boxiness: 0.25f)),
                    skin, new Vector3(0f, 0.50f, 0f)),
                new("Neck", Save("PropNeck", IngredientMeshFactory.Cylinder(0.065f, 0.18f, 8)),
                    skin, new Vector3(0f, 0.38f, 0f)),
                // Bras RENTRÉS dans le torse : posés à ±0,26 pour un buste large de 0,44,
                // ils flottaient à quatre centimètres du corps — et l'écart se creusait vers le
                // bas, le buste étant effilé. À ±0,18 l'épaule mord dans le torse.
                new("ArmLeft", Save("PropArm",
                        IngredientMeshFactory.Blob(1, new Vector3(0.12f, 0.54f, 0.12f), taper: 0.85f,
                            noise: 0f, frequency: 1f, seed: 0, boxiness: 0.4f)),
                    cloth, new Vector3(-0.18f, 0.02f, 0f), new Vector3(0f, 0f, 9f)),
                new("ArmRight", Save("PropArm", null),
                    cloth, new Vector3(0.18f, 0.02f, 0f), new Vector3(0f, 0f, -9f)),
            };

            // Visage en GÉOMÉTRIE et non en texture : ces meshes n'ont aucune coordonnée UV, et
            // les pièces séparées sont de toute façon ce qu'il faut pour animer plus tard —
            // la bouche s'ouvre en changeant une échelle, le regard en tournant la tête.
            //
            // Le visage est IDENTIQUE pour les deux rôles : les serveurs doivent rester
            // indiscernables (§3 du README), donc aucun trait individuel.
            var pupil = new Color(0.15f, 0.13f, 0.12f);

            for (int i = 0; i < 2; i++)
                parts.Add(new Part(i == 0 ? "EyeLeft" : "EyeRight",
                    Save("PropEye", i == 0 ? IngredientMeshFactory.Blob(1, Vector3.one * 0.05f,
                        taper: 1f, noise: 0f, frequency: 1f, seed: 0) : null),
                    pupil, new Vector3(-0.055f + i * 0.11f, 0.545f, -0.105f)));

            parts.Add(new Part("Mouth", Save("PropMouth", Box(new Vector3(0.09f, 0.022f, 0.03f))),
                new Color(0.45f, 0.24f, 0.22f), new Vector3(0f, 0.455f, -0.105f)));

            if (apron)
                // z NÉGATIF : le visage est à z négatif (les yeux à -0,105), donc le devant
                // aussi. À +0,15, le tablier était noué dans le dos.
                parts.Add(new Part("Apron", Save("PropApron", Box(new Vector3(0.34f, 0.46f, 0.04f))),
                    new Color(0.93f, 0.93f, 0.90f), new Vector3(0f, -0.16f, -0.15f)));

            return parts;
        }

        /// <summary>
        /// Personne ASSISE sur un tabouret — le client. Même visage, même cou, mêmes bras que
        /// le personnage debout (les meshes partagés sont réutilisés tels quels) : seule la
        /// POSTURE change, et c'est elle qui distingue un client d'un serveur au premier coup
        /// d'œil, avant même la couleur.
        ///
        /// Un tabouret et non une chaise : pas de dossier à faire cohabiter avec le buste, et
        /// la silhouette reste lisible de tous les côtés. Cuisses et tibias sont des pièces
        /// séparées, ENFONCÉES l'une dans l'autre (la règle des personnages : une pièce ne se
        /// pose pas sur une surface courbe, elle s'y enfonce) — cuisses dans le bas du buste,
        /// genoux dans l'avant des cuisses.
        ///
        /// Le devant est à z NÉGATIF, comme le visage : cuisses et tibias partent vers -z,
        /// donc sous la table quand le client lui fait face.
        /// </summary>
        private static List<Part> SeatedPerson(string name, Color cloth)
        {
            var skin = new Color(0.86f, 0.70f, 0.56f);
            var wood = new Color(0.52f, 0.38f, 0.26f);
            var metal = new Color(0.55f, 0.57f, 0.60f);

            var parts = new List<Part>
            {
                // Buste raccourci du modèle debout : mêmes paramètres, hauteur réduite —
                // les jambes ne sont plus « impliquées » dans le blob, elles existent.
                new(name, Save($"PropBody{name}",
                        IngredientMeshFactory.Blob(2, new Vector3(0.44f, 0.62f, 0.28f), taper: 0.72f,
                            noise: 0.02f, frequency: 2f, seed: 61, boxiness: 0.35f)),
                    cloth, new Vector3(0f, 0.12f, 0f)),

                // Tête, cou, visage : EXACTEMENT les cotes du modèle debout — le buste
                // raccourci culmine à 0,43 comme l'autre, donc tout se réutilise verbatim.
                new("Head", Save("PropHead",
                        IngredientMeshFactory.Blob(2, new Vector3(0.24f, 0.28f, 0.24f), taper: 1f,
                            noise: 0.02f, frequency: 3f, seed: 67, boxiness: 0.25f)),
                    skin, new Vector3(0f, 0.50f, 0f)),
                new("Neck", Save("PropNeck", IngredientMeshFactory.Cylinder(0.065f, 0.18f, 8)),
                    skin, new Vector3(0f, 0.38f, 0f)),

                // Bras légèrement portés vers l'avant (rotation X négative) : mains vers la
                // table plutôt que ballantes — c'est le geste qui dit « attablé ».
                new("ArmLeft", Save("PropArm",
                        IngredientMeshFactory.Blob(1, new Vector3(0.12f, 0.54f, 0.12f), taper: 0.85f,
                            noise: 0f, frequency: 1f, seed: 0, boxiness: 0.4f)),
                    cloth, new Vector3(-0.18f, 0.08f, -0.03f), new Vector3(-16f, 0f, 9f)),
                new("ArmRight", Save("PropArm", null),
                    cloth, new Vector3(0.18f, 0.08f, -0.03f), new Vector3(-16f, 0f, -9f)),

                // Cuisses : un seul bloc horizontal vers -z, enfoncé dans le bas du buste.
                new("Lap", Save("PropLap",
                        IngredientMeshFactory.Blob(1, new Vector3(0.34f, 0.15f, 0.44f), taper: 0.92f,
                            noise: 0.01f, frequency: 2f, seed: 73, boxiness: 0.55f)),
                    cloth, new Vector3(0f, -0.16f, -0.16f)),

                // Tibias : les genoux mordent dans l'avant des cuisses, les pieds descendent
                // au niveau de la base du tabouret.
                new("ShinLeft", Save("PropShin",
                        IngredientMeshFactory.Blob(1, new Vector3(0.11f, 0.46f, 0.11f), taper: 0.9f,
                            noise: 0f, frequency: 1f, seed: 0, boxiness: 0.5f)),
                    cloth, new Vector3(-0.10f, -0.36f, -0.32f)),
                new("ShinRight", Save("PropShin", null),
                    cloth, new Vector3(0.10f, -0.36f, -0.32f)),

                // Le tabouret, dans le langage de la table : assise + fût + socle.
                new("StoolSeat", Save("PropStoolSeat", IngredientMeshFactory.Cylinder(0.21f, 0.035f, 14)),
                    wood, new Vector3(0f, -0.25f, 0f)),
                new("StoolPillar", Save("PropStoolPillar", IngredientMeshFactory.Cylinder(0.035f, 0.32f, 8)),
                    metal, new Vector3(0f, -0.42f, 0f)),
                new("StoolFoot", Save("PropStoolFoot", IngredientMeshFactory.Cylinder(0.12f, 0.03f, 12)),
                    metal, new Vector3(0f, -0.585f, 0f)),
            };

            var pupil = new Color(0.15f, 0.13f, 0.12f);
            for (int i = 0; i < 2; i++)
                parts.Add(new Part(i == 0 ? "EyeLeft" : "EyeRight",
                    Save("PropEye", i == 0 ? IngredientMeshFactory.Blob(1, Vector3.one * 0.05f,
                        taper: 1f, noise: 0f, frequency: 1f, seed: 0) : null),
                    pupil, new Vector3(-0.055f + i * 0.11f, 0.545f, -0.105f)));

            parts.Add(new Part("Mouth", Save("PropMouth", Box(new Vector3(0.09f, 0.022f, 0.03f))),
                new Color(0.45f, 0.24f, 0.22f), new Vector3(0f, 0.455f, -0.105f)));

            return parts;
        }

        /// <summary>
        /// Couteau : lame effilée, garde, manche. Prévu pour une future animation de découpe —
        /// il n'est encore posé nulle part, mais son modèle existe et l'exposition le montre.
        /// </summary>
        private static List<Part> Knife()
        {
            return new List<Part>
            {
                new("Knife", Save("PropKnifeBlade",
                        IngredientMeshFactory.Blob(1, new Vector3(0.05f, 0.30f, 1f), taper: 0.15f,
                            noise: 0f, frequency: 1f, seed: 0, boxiness: 0.9f)),
                    new Color(0.82f, 0.84f, 0.87f)),
                new("Guard", Save("PropKnifeGuard", Box(new Vector3(0.12f, 0.10f, 0.05f))),
                    new Color(0.55f, 0.56f, 0.58f), new Vector3(0f, 0f, 0.52f)),
                new("Handle", Save("PropKnifeHandle",
                        IngredientMeshFactory.Blob(1, new Vector3(0.09f, 0.09f, 0.42f), taper: 0.8f,
                            noise: 0f, frequency: 1f, seed: 0, boxiness: 0.7f)),
                    new Color(0.25f, 0.18f, 0.14f), new Vector3(0f, 0f, 0.76f)),
            };
        }

        #region Formes propres aux props

        private static Mesh Save(string name, Mesh mesh) => IngredientMeshFactory.Save(name, mesh);

        /// <summary>
        /// Cylindre à rayons différents en haut et en bas : fût de poubelle, socle de table.
        /// Un cône tronqué se lit tout de suite comme un objet fabriqué, là où un cylindre droit
        /// reste une primitive.
        /// </summary>
        private static Mesh Tube(float bottom, float top, float height, int sides)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            float half = height * 0.5f;

            for (int i = 0; i < sides; i++)
            {
                float angle = i / (float)sides * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                vertices.Add(new Vector3(cos * bottom * 0.5f, -half, sin * bottom * 0.5f));
                vertices.Add(new Vector3(cos * top * 0.5f, half, sin * top * 0.5f));
            }

            int bottomCentre = vertices.Count; vertices.Add(new Vector3(0f, -half, 0f));
            int topCentre = vertices.Count; vertices.Add(new Vector3(0f, half, 0f));

            for (int i = 0; i < sides; i++)
            {
                int a = i * 2, b = a + 1;
                int c = (i + 1) % sides * 2, d = c + 1;

                triangles.AddRange(new[] { a, b, d, a, d, c });

                // Mêmes couvercles inversés que dans Cylinder : (centre, courant, suivant)
                // regarde vers le bas, (centre, suivant, courant) vers le haut.
                triangles.AddRange(new[] { bottomCentre, a, c });
                triangles.AddRange(new[] { topCentre, d, b });
            }
            return IngredientMeshFactory.Faceted(vertices, triangles);
        }

        /// <summary>
        /// Récipient réellement creux : paroi extérieure évasée, paroi intérieure qui redescend,
        /// et un fond. Ouvert sur le dessus.
        ///
        /// Sans paroi intérieure, on ne voit qu'un tube fermé : le creux ne se déduit pas d'une
        /// silhouette, il se montre.
        /// </summary>
        private static Mesh Hollow(float bottom, float top, float height, float wall, int sides)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            float half = height * 0.5f;

            for (int i = 0; i < sides; i++)
            {
                float angle = i / (float)sides * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);

                // 0 extérieur bas, 1 extérieur haut, 2 intérieur haut, 3 intérieur bas
                vertices.Add(new Vector3(cos * bottom * 0.5f, -half, sin * bottom * 0.5f));
                vertices.Add(new Vector3(cos * top * 0.5f, half, sin * top * 0.5f));
                vertices.Add(new Vector3(cos * (top - wall * 2f) * 0.5f, half, sin * (top - wall * 2f) * 0.5f));
                vertices.Add(new Vector3(cos * (bottom - wall * 2f) * 0.5f, -half + wall, sin * (bottom - wall * 2f) * 0.5f));
            }

            int outerFloor = vertices.Count; vertices.Add(new Vector3(0f, -half, 0f));
            int innerFloor = vertices.Count; vertices.Add(new Vector3(0f, -half + wall, 0f));

            for (int i = 0; i < sides; i++)
            {
                int a = i * 4;
                int b = (i + 1) % sides * 4;

                // Paroi extérieure : normales vers l'extérieur.
                triangles.AddRange(new[] { a, a + 1, b + 1, a, b + 1, b });

                // Paroi intérieure : mêmes quads, mais RETOURNÉS — c'est la seule surface de
                // tout le projet qui doive regarder vers l'intérieur, et l'écrire dans le même
                // sens que la paroi extérieure la rendait invisible de l'endroit d'où on la
                // regarde, c'est-à-dire d'en haut.
                triangles.AddRange(new[] { b + 2, a + 2, a + 3, b + 3, b + 2, a + 3 });

                // Tranche du bord, tournée vers le haut.
                triangles.AddRange(new[] { a + 1, a + 2, b + 2, a + 1, b + 2, b + 1 });

                triangles.AddRange(new[] { outerFloor, a, b });          // dessous, vers −Y
                triangles.AddRange(new[] { innerFloor, b + 3, a + 3 });  // fond intérieur, vers +Y
            }
            return IngredientMeshFactory.Faceted(vertices, triangles);
        }

        private static Mesh Box(Vector3 scale)
        {
            float x = scale.x * 0.5f, y = scale.y * 0.5f, z = scale.z * 0.5f;

            var vertices = new List<Vector3>
            {
                new(-x, -y, -z), new(x, -y, -z), new(x, y, -z), new(-x, y, -z),
                new(-x, -y,  z), new(x, -y,  z), new(x, y,  z), new(-x, y,  z),
            };
            var triangles = new List<int>
            {
                0, 2, 1, 0, 3, 2,  4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4,  3, 7, 6, 3, 6, 2,
                0, 4, 7, 0, 7, 3,  1, 2, 6, 1, 6, 5,
            };
            return IngredientMeshFactory.Faceted(vertices, triangles);
        }

        #endregion
    }
}
