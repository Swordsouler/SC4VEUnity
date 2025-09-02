using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Sven.Command
{
    public class CreateCommand : Command<CommandSettings, AnnotationParameter>, IBaseCommand
    {
        public DateTime CompletionTime { get; set; }

        public async Task Execute()
        {
            await Task.Yield();
            Debug.Log(Parameter);
            if (Parameter != null && Parameter.Prefabs != null && Parameter.Prefabs.Count > 0)
            {
                // get a random prefab from the list
                int randomIndex = UnityEngine.Random.Range(0, Parameter.Prefabs.Count);
                GameObject prefab = Parameter.Prefabs[randomIndex];
                GameObject.Instantiate(prefab, Camera.main.transform.position, Quaternion.identity);
            }
        }
    }
}