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
            foreach (SemantizationCore obj in objects)
            {
                string colorStr = obj.TryGetComponent(out Renderer r) && r.material != null
                    ? r.material.color.ToString()
                    : "N/A";
                Debug.Log(
                    $"[Describe] UUID: {obj.GetUUID()}\n" +
                    $"  Position : {obj.transform.position}\n" +
                    $"  Rotation : {obj.transform.eulerAngles}\n" +
                    $"  Taille   : {obj.transform.localScale}\n" +
                    $"  Couleur  : {colorStr}\n" +
                    $"  Actif    : {obj.gameObject.activeSelf}");
            }
            return objects;
        }
    }
}
