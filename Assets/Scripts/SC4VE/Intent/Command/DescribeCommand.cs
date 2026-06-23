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
                string colorName = hasColor ? ColorParameter.GetNearestColorName(r.material.color) : null;
                string colorStr  = hasColor ? r.material.color.ToString() : "N/A";
                Debug.Log(
                    $"[Describe] UUID: {obj.GetUUID()}\n" +
                    $"  Position : {obj.transform.position}\n" +
                    $"  Rotation : {obj.transform.eulerAngles}\n" +
                    $"  Taille   : {obj.transform.localScale}\n" +
                    $"  Couleur  : {(colorName != null ? colorName + " " : "")}{colorStr}\n" +
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
    }
}
