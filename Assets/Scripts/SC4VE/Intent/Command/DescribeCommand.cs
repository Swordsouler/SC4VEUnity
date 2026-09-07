using Sven.Content;
using Sven.Demo;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("décris", "décrire", "infos", "informations", "propriétés",
                       "qu'est-ce que c'est", "c'est quoi")]
    [Serializable, CommandDescription("Affiche les propriétés des objets dans la console. Paramètres: SelectionParameter.")]
    public class DescribeCommand : Command
    {
        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter.Objects;

            // « Qu'est-ce que c'est ? » n'a pas la même réponse selon la cible : sur une pomme
            // c'est une description, sur une assiette c'est le nom du plat qu'elle compose.
            // Aucune analyse lexicale ne peut trancher — seule la sémantique de l'objet le peut,
            // et c'est précisément ce que le graphe sait dire.
            SemantizationCore container = objects.FirstOrDefault(
                o => o != null && o.GetComponent<ContainerContent>() != null);
            if (container != null)
            {
                _ = CheckCommand.Announce(container, null);
                return new List<SemantizationCore> { container };
            }

            var spoken = new List<string>();
            foreach (SemantizationCore obj in objects)
            {
                // Lire le vrai matériau, pas la surbrillance de focus appliquée au survol du pointeur.
                Material mat = DemoCharacterController.GetUnhighlightedMaterial(obj.gameObject);
                bool hasColor = mat != null;
                UnityEngine.Color matColor = hasColor ? ReadColor(mat) : default;
                string colorName = hasColor ? ColorParameter.GetColorName(matColor) : null;
                string colorStr  = hasColor ? matColor.ToString() : "N/A";
                Debug.Log(
                    $"[Describe] UUID: {obj.GetUUID()}\n" +
                    $"  Position : {obj.transform.position}\n" +
                    $"  Rotation : {obj.transform.eulerAngles}\n" +
                    $"  Taille   : {obj.transform.localScale}\n" +
                    $"  Couleur  : {colorName ?? "(non reconnue)"} {colorStr}\n" +
                    $"  Actif    : {obj.gameObject.activeSelf}");

                Vector3 p = obj.transform.position;
                string colorPart = string.IsNullOrEmpty(colorName) ? "" : $"couleur {colorName}, ";
                spoken.Add($"{obj.gameObject.name}, {colorPart}position {Mathf.RoundToInt(p.x)}, {Mathf.RoundToInt(p.y)}, {Mathf.RoundToInt(p.z)}");
            }

            if (objects.Count == 0)
                Speak("Aucun objet à décrire.");
            else if (objects.Count > 5)
                Speak($"{objects.Count} objets sélectionnés.");
            else
                Speak(string.Join(". ", spoken) + ".");

            return objects;
        }

        // En URP/HDRP la couleur visible est dans "_BaseColor" ; material.color ne lit que "_Color"
        // et renvoie du blanc si la propriété est absente. On lit donc _BaseColor en priorité.
        private static UnityEngine.Color ReadColor(Material m)
        {
            if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
            if (m.HasProperty("_Color"))     return m.GetColor("_Color");
            return m.color;
        }
    }
}
