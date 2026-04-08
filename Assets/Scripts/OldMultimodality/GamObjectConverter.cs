#if UNITY_EDITOR
using Newtonsoft.Json;
using System;
using UnityEditor;
using UnityEngine;

namespace Sven.Command
{
    public class GameObjectConverter : JsonConverter<GameObject>
    {
        public override void WriteJson(JsonWriter writer, GameObject value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            string path = AssetDatabase.GetAssetPath(value);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning($"GameObject '{value.name}' is not a prefab asset and will not be serialized.");
                writer.WriteNull();
            }
            else
            {
                writer.WriteValue(path);
            }
        }

        public override GameObject ReadJson(JsonReader reader, Type objectType, GameObject existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonToken.String)
            {
                string path = (string)reader.Value;
                if (string.IsNullOrEmpty(path))
                {
                    return null;
                }

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    Debug.LogWarning($"[GameObjectConverter] Could not load GameObject from path: {path}");
                }
                return prefab;
            }

            return null;
        }
    }
}
#endif