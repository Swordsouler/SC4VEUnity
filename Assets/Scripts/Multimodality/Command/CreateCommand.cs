using Sven.Content;
using Sven.Multimodality;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Sven.Command
{
    public class CreateCommand : Command<CommandSettings, AnnotationParameter>, IBaseCommand
    {
        public DateTime CompletionTime { get; set; }

        public async Task Execute()
        {
            await Task.Yield(); // Garde la méthode asynchrone

            if (Parameter == null || string.IsNullOrEmpty(Parameter.AnnotationType))
            {
                Debug.LogWarning("[CreateCommand] AnnotationParameter is null or has no AnnotationType.");
                return;
            }

            // 1. Récupérer tous les préfabriqués des types enfants
            var allPrefabs = GetPrefabsForTypeAndSubtypes(Parameter.AnnotationType);

            // 2. Instancier un préfabriqué aléatoire
            if (allPrefabs.Count > 0)
            {
                int randomIndex = UnityEngine.Random.Range(0, allPrefabs.Count);
                GameObject prefab = allPrefabs[randomIndex];
                if (prefab != null)
                {
                    Debug.Log($"[CreateCommand] Instantiating prefab '{prefab.name}' for type '{Parameter.AnnotationType}'.");
                    // Instancie l'objet à 2 mètres devant la caméra principale
                    GameObject.Instantiate(prefab, Camera.main.transform.position + Camera.main.transform.forward * 2, Quaternion.identity);
                }
                else
                {
                    Debug.LogWarning($"[CreateCommand] Prefab at index {randomIndex} is null.");
                }
            }
            else
            {
                Debug.LogWarning($"[CreateCommand] No prefabs found for annotation type '{Parameter.AnnotationType}' or its subtypes.");
            }
        }

        private List<GameObject> GetPrefabsForTypeAndSubtypes(string baseAnnotationType)
        {
            try
            {
                // Utilise ISemanticAnnotation pour trouver le type C# de base
                Type baseType = ISemanticAnnotation.GetType(baseAnnotationType);

                // Utilise ISemanticAnnotation pour obtenir tous les types disponibles
                string[] allAnnotationTypes = ISemanticAnnotation.GetAvailableAnnotationTypes();

                // Filtrer pour trouver les sous-types concrets
                var concreteSubtypes = allAnnotationTypes.Where(typeName =>
                {
                    Type subType = ISemanticAnnotation.GetType(typeName);
                    return baseType.IsAssignableFrom(subType) && !subType.IsAbstract;
                });

                var allPrefabs = new List<GameObject>();

                // Récupérer les settings d'annotation
                if (MultimodalityController.Settings.TryGetValue(typeof(AnnotationFilter).FullName, out var settings) && settings is AnnotationFilterSettings annotationSettings)
                {
                    // Parcourir les sous-types et collecter les préfabriqués
                    foreach (var subTypeName in concreteSubtypes)
                    {
                        var entry = annotationSettings.Entries.FirstOrDefault(e => e.AnnotationParameter.AnnotationType == subTypeName);
                        if (entry != null && entry.AnnotationParameter.Prefabs != null)
                        {
                            allPrefabs.AddRange(entry.AnnotationParameter.Prefabs);
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("[CreateCommand] AnnotationFilterSettings not found in global settings.");
                }

                return allPrefabs.Distinct().ToList();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CreateCommand] An error occurred while getting prefabs: {ex.Message}");
                return new List<GameObject>();
            }
        }
    }
}