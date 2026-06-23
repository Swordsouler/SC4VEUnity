using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("décris", "décrire", "infos", "informations", "propriétés",
                       "qu'est-ce que c'est", "c'est quoi")]
    [Serializable, CommandDescription("Affiche les propriétés des objets dans la console. Paramètres: SelectionParameter.")]
    public class DescribeCommand : Command
    {
        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter.Objects;
            var spoken = new List<string>();
            foreach (SemantizationCore obj in objects)
            {
                bool hasColor = obj.TryGetComponent(out Renderer r) && r.material != null;
                UnityEngine.Color matColor = hasColor ? ReadColor(r.material) : default;
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
