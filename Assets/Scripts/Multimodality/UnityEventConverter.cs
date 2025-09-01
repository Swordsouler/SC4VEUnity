#if UNITY_EDITOR
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Reflection;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;

namespace Sven.Command
{
    public class UnityEventConverter : JsonConverter<UnityEvent>
    {
        public override void WriteJson(JsonWriter writer, UnityEvent value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartObject();
            writer.WritePropertyName("m_PersistentCalls");
            writer.WriteStartObject();
            writer.WritePropertyName("m_Calls");
            writer.WriteStartArray();

            var persistentCallsField = typeof(UnityEventBase).GetField("m_PersistentCalls", BindingFlags.Instance | BindingFlags.NonPublic);
            var persistentCalls = persistentCallsField.GetValue(value);
            var callsField = persistentCalls.GetType().GetField("m_Calls", BindingFlags.Instance | BindingFlags.NonPublic);
            var calls = (System.Collections.IList)callsField.GetValue(persistentCalls);

            for (int i = 0; i < value.GetPersistentEventCount(); i++)
            {
                var target = value.GetPersistentTarget(i);
                var methodName = value.GetPersistentMethodName(i);

                if (target == null || string.IsNullOrEmpty(methodName)) continue;

                writer.WriteStartObject();

                // Sérialiser la cible
                string path = GetGameObjectPath(target);
                writer.WritePropertyName("targetPath");
                writer.WriteValue(path);
                writer.WritePropertyName("targetType");
                writer.WriteValue(target.GetType().AssemblyQualifiedName);

                // Sérialiser la méthode
                writer.WritePropertyName("methodName");
                writer.WriteValue(methodName);

                var persistentCall = calls[i];
                var argumentsField = persistentCall.GetType().GetField("m_Arguments", BindingFlags.Instance | BindingFlags.NonPublic);
                var argumentCache = argumentsField.GetValue(persistentCall);
                var modeField = persistentCall.GetType().GetField("m_Mode", BindingFlags.Instance | BindingFlags.NonPublic);
                var mode = (PersistentListenerMode)modeField.GetValue(persistentCall);

                writer.WritePropertyName("mode");
                serializer.Serialize(writer, mode);

                if (mode == PersistentListenerMode.Void || mode == PersistentListenerMode.EventDefined)
                {
                    // No arguments to serialize for Void or EventDefined modes
                }
                else
                {
                    object argumentValue = null;
                    string argumentTypeName = null;
                    switch (mode)
                    {
                        case PersistentListenerMode.Bool:
                            argumentValue = argumentCache.GetType().GetProperty("boolArgument").GetValue(argumentCache);
                            argumentTypeName = typeof(bool).AssemblyQualifiedName;
                            break;
                        case PersistentListenerMode.Int:
                            argumentValue = argumentCache.GetType().GetProperty("intArgument").GetValue(argumentCache);
                            argumentTypeName = typeof(int).AssemblyQualifiedName;
                            break;
                        case PersistentListenerMode.Float:
                            argumentValue = argumentCache.GetType().GetProperty("floatArgument").GetValue(argumentCache);
                            argumentTypeName = typeof(float).AssemblyQualifiedName;
                            break;
                        case PersistentListenerMode.String:
                            argumentValue = argumentCache.GetType().GetProperty("stringArgument").GetValue(argumentCache);
                            argumentTypeName = typeof(string).AssemblyQualifiedName;
                            break;
                        case PersistentListenerMode.Object:
                            argumentValue = argumentCache.GetType().GetProperty("unityObjectArgument").GetValue(argumentCache);
                            if (argumentValue != null)
                                argumentTypeName = argumentValue.GetType().AssemblyQualifiedName;
                            break;
                    }

                    writer.WritePropertyName("argumentType");
                    writer.WriteValue(argumentTypeName);
                    writer.WritePropertyName("argumentValue");
                    if (argumentValue is UnityEngine.Object unityObject)
                    {
                        writer.WriteValue(GetGameObjectPath(unityObject));
                    }
                    else
                    {
                        serializer.Serialize(writer, argumentValue);
                    }
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        public override UnityEvent ReadJson(JsonReader reader, Type objectType, UnityEvent existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;

            var newEvent = new UnityEvent();
            JObject jObject = JObject.Load(reader);
            var calls = jObject.SelectToken("m_PersistentCalls.m_Calls") as JArray;

            if (calls == null) return newEvent;

            foreach (var call in calls)
            {
                var targetPath = call["targetPath"]?.Value<string>();
                var targetTypeName = call["targetType"]?.Value<string>();
                var methodName = call["methodName"]?.Value<string>();
                var mode = call["mode"].ToObject<PersistentListenerMode>(serializer);

                if (string.IsNullOrEmpty(targetPath) || string.IsNullOrEmpty(targetTypeName) || string.IsNullOrEmpty(methodName)) continue;

                var targetGO = GameObject.Find(targetPath);
                if (targetGO == null)
                {
                    Debug.LogWarning($"[UnityEventConverter] Could not find GameObject at path: {targetPath}");
                    continue;
                }

                Type targetType = Type.GetType(targetTypeName);
                if (targetType == null)
                {
                    Debug.LogWarning($"[UnityEventConverter] Could not find type: {targetTypeName}");
                    continue;
                }

                UnityEngine.Object targetComponent = (targetType == typeof(GameObject)) ? (UnityEngine.Object)targetGO : targetGO.GetComponent(targetType);
                if (targetComponent == null)
                {
                    Debug.LogWarning($"[UnityEventConverter] Could not find component of type {targetType.Name} on GameObject {targetGO.name}");
                    continue;
                }

                if (mode == PersistentListenerMode.EventDefined || mode == PersistentListenerMode.Void)
                {
                    MethodInfo methodInfo = targetComponent.GetType().GetMethod(methodName, new Type[0]);
                    if (methodInfo != null)
                    {
                        var action = (UnityAction)Delegate.CreateDelegate(typeof(UnityAction), targetComponent, methodInfo);
                        UnityEventTools.AddPersistentListener(newEvent, action);
                    }
                    else
                    {
                        Debug.LogWarning($"[UnityEventConverter] Could not find parameterless method '{methodName}' on component '{targetComponent.GetType().Name}'");
                    }
                }
                else
                {
                    var argumentTypeName = call["argumentType"]?.Value<string>();
                    if (string.IsNullOrEmpty(argumentTypeName)) continue;

                    Type argumentType = Type.GetType(argumentTypeName);
                    if (argumentType == null)
                    {
                        Debug.LogWarning($"[UnityEventConverter] Could not find argument type: {argumentTypeName}");
                        continue;
                    }

                    MethodInfo methodInfo = targetComponent.GetType().GetMethod(methodName, new[] { argumentType });
                    if (methodInfo != null)
                    {
                        switch (mode)
                        {
                            case PersistentListenerMode.Bool:
                                UnityEventTools.AddBoolPersistentListener(newEvent, new UnityAction<bool>((bool arg) => methodInfo.Invoke(targetComponent, new object[] { arg })), call["argumentValue"].Value<bool>());
                                break;
                            case PersistentListenerMode.Int:
                                UnityEventTools.AddIntPersistentListener(newEvent, new UnityAction<int>((int arg) => methodInfo.Invoke(targetComponent, new object[] { arg })), call["argumentValue"].Value<int>());
                                break;
                            case PersistentListenerMode.Float:
                                UnityEventTools.AddFloatPersistentListener(newEvent, new UnityAction<float>((float arg) => methodInfo.Invoke(targetComponent, new object[] { arg })), call["argumentValue"].Value<float>());
                                break;
                            case PersistentListenerMode.String:
                                UnityEventTools.AddStringPersistentListener(newEvent, new UnityAction<string>((string arg) => methodInfo.Invoke(targetComponent, new object[] { arg })), call["argumentValue"].Value<string>());
                                break;
                            case PersistentListenerMode.Object:
                                var argPath = call["argumentValue"]?.Value<string>();
                                var argGO = string.IsNullOrEmpty(argPath) ? null : GameObject.Find(argPath);
                                if (argGO != null)
                                {
                                    var argComp = argGO.GetComponent(argumentType);
                                    UnityEventTools.AddObjectPersistentListener(newEvent, new UnityAction<UnityEngine.Object>((UnityEngine.Object arg) => methodInfo.Invoke(targetComponent, new object[] { arg })), argComp);
                                }
                                break;
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[UnityEventConverter] Could not find method '{methodName}' with parameter of type '{argumentType.Name}' on component '{targetComponent.GetType().Name}'");
                    }
                }
            }

            return newEvent;
        }

        private string GetGameObjectPath(UnityEngine.Object target)
        {
            if (target is Component component) return GetPath(component.transform);
            if (target is GameObject go) return GetPath(go.transform);
            return null;
        }

        private string GetPath(Transform current)
        {
            if (current.parent == null) return "/" + current.name;
            return GetPath(current.parent) + "/" + current.name;
        }
    }
}
#endif