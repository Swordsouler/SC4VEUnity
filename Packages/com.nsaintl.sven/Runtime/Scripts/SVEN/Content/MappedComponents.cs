// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using PrimeTween;
using Sven.Context;
using Sven.GeoData;
using Sven.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sven.Content
{
    /// <summary>
    /// Mapped component to semantize.
    /// </summary>
    public static class MapppedComponents
    {

        private static readonly float lerpSpeed = 0.4f;

        /// <summary>
        /// Values of the properties of the components.
        /// </summary>
        private static readonly Dictionary<Type, ComponentMapping> Values = new()
        {
            {
                typeof(Rigidbody), new("RigidBody",
                new List<Delegate>
                {
                    (Func<Rigidbody, ComponentProperty>)(rigidbody => new ComponentProperty("mass", () => rigidbody.mass, value => rigidbody.mass = (float)value, 1)),
                    (Func<Rigidbody, ComponentProperty>)(rigidbody => new ComponentProperty("drag", () => rigidbody.linearDamping, value => rigidbody.linearDamping = (float)value, 1)),
                    (Func<Rigidbody, ComponentProperty>)(rigidbody => new ComponentProperty("angularDrag", () => rigidbody.angularDamping, value => rigidbody.angularDamping = (float)value, 1)),
                    (Func<Rigidbody, ComponentProperty>)(rigidbody => new ComponentProperty("useGravity", () => rigidbody.useGravity, value => rigidbody.useGravity = value.ToString().ToLower() == "true", 1)),
                    (Func<Rigidbody, ComponentProperty>)(rigidbody => new ComponentProperty("isKinematic", () => rigidbody.isKinematic, value => rigidbody.isKinematic = value.ToString().ToLower() == "true", 1)),
                })
            },
            {
                typeof(BoxCollider), new("geo:Feature",
                new List<Delegate>
                {
                    (Func<BoxCollider, ComponentProperty>)(collider => new ComponentProperty("enabled", () => collider.enabled, value => collider.enabled = value.ToString().ToLower() == "true", 1)),
                    (Func<BoxCollider, ComponentProperty>)(collider => new ComponentProperty("size", () => collider.size, value => collider.size = (Vector3)value, 1)),
                    (Func<BoxCollider, ComponentProperty>)(collider => new ComponentProperty("center", () => collider.center, value => collider.center = (Vector3)value, 1)),
                    (Func<BoxCollider, ComponentProperty>)(collider => new ComponentProperty("isTrigger", () => collider.isTrigger, value => collider.isTrigger = value.ToString().ToLower() == "true", 1)),
                    (Func<BoxCollider, ComponentProperty>)(collider => new ComponentProperty("geo:hasGeometry", () => new GeoWKT(collider.bounds), value => {}, 1)),
                })
            },
            {
                typeof(Transform), new("Transform",
                new List<Delegate>
                {
                    (Func<Transform, ComponentProperty>)(transform => new ComponentProperty("position", () => transform.position, value => Tween.Position(transform, (Vector3)value, lerpSpeed), 1/*, "virtualPosition"*/)),
                    (Func<Transform, ComponentProperty>)(transform => new ComponentProperty("rotation", () => transform.rotation, value => Tween.Rotation(transform, (Quaternion)value, lerpSpeed), 1/*, "virtualRotation"*/)),
                    (Func<Transform, ComponentProperty>)(transform => new ComponentProperty("scale", () => transform.localScale, value => Tween.Scale(transform, (Vector3)value, lerpSpeed), 1/*, "virtualSize"*/)),
                })
            },
            {
                typeof(AudioSource), new("Audio",
                new List<Delegate>
                {
                    (Func<AudioSource, ComponentProperty>)(audioSource => new ComponentProperty("enabled", () => audioSource.enabled, value => audioSource.enabled = value.ToString().ToLower() == "true", 1)),
                    (Func<AudioSource, ComponentProperty>)(audioSource => new ComponentProperty("minAudioDistance", () => audioSource.minDistance, value => audioSource.minDistance = (float)value, 1/*, "minSoundDistance"*/)),
                    (Func<AudioSource, ComponentProperty>)(audioSource => new ComponentProperty("maxAudioDistance", () => audioSource.maxDistance, value => audioSource.maxDistance = (float)value, 1/*, "maxSoundDistance"*/)),
                })
            },
            {
                typeof(Camera), new("Camera",
                new List<Delegate>
                {
                    (Func<Camera, ComponentProperty>)(camera => new ComponentProperty("enabled", () => camera.enabled, value => camera.enabled = value.ToString().ToLower() == "true", 1)),
                    (Func<Camera, ComponentProperty>)(camera => new ComponentProperty("nearClipPlane", () => camera.nearClipPlane, value => camera.nearClipPlane = (float)value, 1/*, "POVNear"*/)),
                    (Func<Camera, ComponentProperty>)(camera => new ComponentProperty("farClipPlane", () => camera.farClipPlane, value => camera.farClipPlane = (float)value, 1/*, "POVFar"*/)),
                    (Func<Camera, ComponentProperty>)(camera => new ComponentProperty("fieldOfView", () => camera.fieldOfView, value => camera.fieldOfView = (float)value, 1/*, "POVFOV"*/)),
                    (Func<Camera, ComponentProperty>)(camera => new ComponentProperty("orthographic", () => camera.orthographic, value => camera.orthographic = value.ToString() == "true", 1)),
                    (Func<Camera, ComponentProperty>)(camera => new ComponentProperty("orthographicSize", () => camera.orthographicSize, value => camera.orthographicSize = (float)value, 1)),
                    (Func<Camera, ComponentProperty>)(camera => new ComponentProperty("clearFlags", () => camera.clearFlags.ToString(), value => camera.clearFlags = (CameraClearFlags)Enum.Parse(typeof(CameraClearFlags), (string)value), 1)),
                    (Func<Camera, ComponentProperty>)(camera => new ComponentProperty("backgroundColor", () => camera.backgroundColor, value => camera.backgroundColor = (Color)value, 1)),
                })
            },
            {
                typeof(MeshRenderer), new("3DRender",
                new List<Delegate>
                {
                    (Func<MeshRenderer, ComponentProperty>)(meshRenderer => new ComponentProperty("enabled", () => meshRenderer.enabled, value => meshRenderer.enabled = value.ToString().ToLower() == "true", 1)),
                    (Func<MeshRenderer, ComponentProperty>)(meshRenderer => new ComponentProperty("color", () => meshRenderer.material.HasProperty("_Color") ? meshRenderer.material.color : (Color?)null, value => { if(meshRenderer.material.HasProperty("_Color")) Tween.MaterialColor(meshRenderer.material, (Color)value, lerpSpeed); }, 1/*, "virtualColor"*/)),
                    (Func<MeshRenderer, ComponentProperty>)(meshRenderer => new ComponentProperty("material1", () => meshRenderer.sharedMaterials.Length > 0 && meshRenderer.sharedMaterials[0] != null ? meshRenderer.sharedMaterials[0].name.Replace(" (Instance)", "") : null, value => {
                        string currentMaterialName = meshRenderer.materials.Length > 0 ? meshRenderer.materials[0].name.Replace(" (Instance)", "") : null;
                        if (value == null || (string)value == currentMaterialName) return;
                        Material[] materials = meshRenderer.materials;
                        if(materials.Length > 0 && materials[0] != null && materials[0].name == (string)value) return;
                        if (materials.Length < 1) Array.Resize(ref materials, 1);
                        materials[0] = Resources.Load<Material>($"Materials/{(string)value}");
                        meshRenderer.materials = materials;
                    }, 1)),
                    (Func<MeshRenderer, ComponentProperty>)(meshRenderer => new ComponentProperty("material2", () => meshRenderer.sharedMaterials.Length > 1 && meshRenderer.sharedMaterials[1] != null ? meshRenderer.sharedMaterials[1].name.Replace(" (Instance)", "") : null, value => {
                        string currentMaterialName = meshRenderer.materials.Length > 1 ? meshRenderer.materials[1].name.Replace(" (Instance)", "") : null;
                        if (value == null || (string)value == currentMaterialName) return;
                        Material[] materials = meshRenderer.materials;
                        if (materials.Length > 1 && materials[1] != null && materials[1].name == (string)value) return;
                        if (materials.Length < 2) Array.Resize(ref materials, 2);
                        materials[1] = Resources.Load<Material>($"Materials/{(string)value}");
                        meshRenderer.materials = materials;
                    }, 1)),
                    (Func<MeshRenderer, ComponentProperty>)(meshRenderer => new ComponentProperty("material3", () => meshRenderer.sharedMaterials.Length > 2 && meshRenderer.sharedMaterials[2] != null ? meshRenderer.sharedMaterials[2].name.Replace(" (Instance)", "") : null, value => {
                        string currentMaterialName = meshRenderer.materials.Length > 2 ? meshRenderer.materials[2].name.Replace(" (Instance)", "") : null;
                        if (value == null || (string)value == currentMaterialName) return;
                        Material[] materials = meshRenderer.materials;
                        if (materials.Length > 2 && materials[2] != null && materials[2].name == (string)value) return;
                        if (materials.Length < 3) Array.Resize(ref materials, 3);
                        materials[2] = Resources.Load<Material>($"Materials/{(string)value}");
                        meshRenderer.materials = materials;
                    }, 1)),
                    (Func<MeshRenderer, ComponentProperty>)(meshRenderer => new ComponentProperty("material4", () => meshRenderer.sharedMaterials.Length > 3 && meshRenderer.sharedMaterials[3] != null ? meshRenderer.sharedMaterials[3].name.Replace(" (Instance)", "") : null, value => {
                        string currentMaterialName = meshRenderer.materials.Length > 3 ? meshRenderer.materials[3].name.Replace(" (Instance)", "") : null;
                        if (value == null || (string)value == currentMaterialName) return;
                        Material[] materials = meshRenderer.materials;
                        if (materials.Length > 3 && materials[3] != null && materials[3].name == (string)value) return;
                        if (materials.Length < 4) Array.Resize(ref materials, 4);
                        materials[3] = Resources.Load<Material>($"Materials/{(string)value}");
                        meshRenderer.materials = materials;
                    }, 1)),
                })
            },
            {
                typeof(SpriteRenderer), new("2DRender",
                new List<Delegate>
                {
                    (Func<SpriteRenderer, ComponentProperty>)(spriteRenderer => new ComponentProperty("enabled", () => spriteRenderer.enabled, value => spriteRenderer.enabled = value.ToString().ToLower() == "true", 1)),
                    (Func<SpriteRenderer, ComponentProperty>)(spriteRenderer => new ComponentProperty("color", () => spriteRenderer.color, value => spriteRenderer.color = (Color)value, 1/*, "virtualColor"*/)),
                    (Func<SpriteRenderer, ComponentProperty>)(spriteRenderer => new ComponentProperty("sprite", () => spriteRenderer.sprite.name, value => spriteRenderer.sprite = Resources.Load<Sprite>((string)value), 1)),
                })
            },
            {
                typeof(ParticleSystem), new("Particle",
                new List<Delegate>
                {
                    (Func<ParticleSystem, ComponentProperty>)(particleSystem => new ComponentProperty("playing", () => particleSystem.isPlaying, value => {
                        if (value.ToString().ToLower() == "true")
                            particleSystem.Play();
                        else
                            particleSystem.Stop();
                    }, 1)),
                })
            },
            {
                typeof(MeshFilter), new("Shape",
                new List<Delegate>
                {
                    (Func<MeshFilter, ComponentProperty>)(meshFilter => new ComponentProperty("mesh", () =>
                    {
                        Mesh m = meshFilter.sharedMesh ?? meshFilter.mesh;
                        if (m == null) return null;
                        // Tous les meshes sont obligatoirement dans Resources/Meshes, on cherche le mesh exact pour récupérer son nom de fichier (sans extension)
                        Mesh[] resources = Resources.LoadAll<Mesh>("Meshes");
                        foreach (var rm in resources)
                            if (rm == m)
                                return rm.name;
                        // fallback si non trouvé (mesh instancié à la volée)
                        return m.name.Replace(" Instance", "");
                    }, value => {
                        if (value == null) return;
                        // sharedMesh : on référence l'asset Resources en lecture seule au lieu d'instancier une copie
                        // (meshFilter.mesh) à chaque scrub du replay, ce qui fuyait un Mesh natif par changement.
                        if (meshFilter.sharedMesh != null && meshFilter.sharedMesh.name == (string)value) return;
                        Mesh mesh = Resources.Load<Mesh>($"Meshes/{(string)value}");
                        if (mesh == null) return;
                        meshFilter.sharedMesh = mesh;
                    }, 1)),
                    /*(Func<MeshFilter, PropertyDescription>)(meshFilter => new PropertyDescription("vertices", () => string.Join("|", meshFilter.mesh.vertices.Select(v => v.ToString())), value => {
                            try {
                                Vector3[] vertices = ((string)value).Split('|').Select(ParseVector3).ToArray();
                                if (vertices.Length == meshFilter.mesh.vertexCount) return;
                                meshFilter.mesh.Clear();
                                meshFilter.mesh.SetVertices(vertices);
                                _meshFilterUpdates[meshFilter] = new() { Vertices = true };
                            } catch {}
                        }, 1)),
                    (Func<MeshFilter, PropertyDescription>)(meshFilter => new PropertyDescription("triangles", () => string.Join("|", meshFilter.mesh.triangles.Select(t => t.ToString())), value => {
                            try {
                                if(!_meshFilterUpdates.TryGetValue(meshFilter, out MeshFilterUpdate update) || update.Triangles) return;

                                int[] triangles = ((string)value).Split('|').Select(int.Parse).ToArray();
                                meshFilter.mesh.SetTriangles(triangles, 0);
                                update.Triangles = true;
                                if(update.IsComplete) _meshFilterUpdates.Remove(meshFilter);
                            } catch {}
                        }, 2)),
                    (Func<MeshFilter, PropertyDescription>)(meshFilter => new PropertyDescription("normals", () => string.Join("|", meshFilter.mesh.normals.Select(n => n.ToString())), value => {
                            try {
                                if(!_meshFilterUpdates.TryGetValue(meshFilter, out MeshFilterUpdate update) || update.Normals) return;

                                Vector3[] normals = ((string)value).Split('|').Select(ParseVector3).ToArray();
                                if (normals.Length != meshFilter.mesh.vertexCount) return;
                                meshFilter.mesh.SetNormals(normals);
                                update.Normals = true;
                                if(update.IsComplete) _meshFilterUpdates.Remove(meshFilter);
                            } catch {}
                        }, 2)),
                    (Func<MeshFilter, PropertyDescription>)(meshFilter => new PropertyDescription("uvs", () => string.Join("|", meshFilter.mesh.uv.Select(uv => uv.ToString())), value => {
                            try {
                                if(!_meshFilterUpdates.TryGetValue(meshFilter, out MeshFilterUpdate update) || update.UVs) return;

                                if((string)value == "") {
                                    meshFilter.mesh.SetUVs(0, new List<Vector2>(new Vector2[meshFilter.mesh.vertexCount]));
                                } else {
                                    Vector2[] uvs = ((string)value).Split('|').Select(ParseVector2).ToArray();
                                    meshFilter.mesh.SetUVs(0, uvs);
                                }
                                update.UVs = true;
                                if(update.IsComplete) _meshFilterUpdates.Remove(meshFilter);
                            } catch {}
                        }, 2)),*/
                })
            },
            {typeof(ManipulableObject), new("ManipulableObject",
                new List<Delegate>
                {
                    (Func<ManipulableObject, ComponentProperty>)(manipulableObject => new ComponentProperty("enabled", () => manipulableObject.enabled, value => manipulableObject.enabled = value.ToString().ToLower() == "true", 1)),
                })},

            // Ignore the following components
            {typeof(SemantizationCore), null},
            {typeof(PointOfView), null},
            {typeof(GraspArea), new("GraspArea", new List<Delegate>
            {
                (Func<GraspArea, ComponentProperty>)(graspArea => new ComponentProperty("enabled", () => graspArea.enabled, value => graspArea.enabled = value.ToString().ToLower() == "true", 1)),
                (Func<GraspArea, ComponentProperty>)(graspArea => new ComponentProperty("graspDistance", () => graspArea.GraspDistance, value => graspArea.GraspDistance = (float)value, 1)),
                (Func<GraspArea, ComponentProperty>)(graspArea => new ComponentProperty("graspHeight", () => graspArea.GraspHeight, value => graspArea.GraspHeight = (float)value, 1)),
            })},
            {typeof(Pointer), new("Pointer", new List<Delegate>
            {
                (Func<Pointer, ComponentProperty>)(pointer => new ComponentProperty("enabled", () => pointer.enabled, value => pointer.enabled = value.ToString().ToLower() == "true", 1)),
                (Func<Pointer, ComponentProperty>)(pointer => new ComponentProperty("pointerIndex", () => pointer.PointerIndex, value => pointer.PointerIndex = (int)value, 1)),
                (Func<Pointer, ComponentProperty>)(pointer => new ComponentProperty("pointerDistance", () => pointer.PointerDistance, value => pointer.PointerDistance = (float)value, 1)),
                (Func<Pointer, ComponentProperty>)(pointer => new ComponentProperty("pointerPosition", () => pointer.PointerPosition, value => pointer.PointerPosition = (Vector3)value, 1)),
                (Func<Pointer, ComponentProperty>)(pointer => new ComponentProperty("pointerDirection", () => pointer.PointerDirection, value => pointer.PointerDirection = (Vector3)value, 1)),
                (Func<Pointer, ComponentProperty>)(pointer => new ComponentProperty("pointerHitPosition", () => pointer.PointerHitPosition, value => pointer.PointerHitPosition = (Vector3)value, 1)),
                (Func<Pointer, ComponentProperty>)(pointer => new ComponentProperty("pointerHitDistance", () => pointer.PointerHitDistance, value => pointer.PointerHitDistance = (float)value, 1)),
                (Func<Pointer, ComponentProperty>)(pointer => new ComponentProperty("pointerConeAngle", () => pointer.PointerConeAngle, value => pointer.PointerConeAngle = (float)value, 1)),
            })},
        };

        /// <summary>
        /// Adds a new component description to the dictionary.
        /// </summary>
        /// <param name="type">The type of the component.</param>
        /// <param name="description">The component description.</param>
        public static void AddComponentDescription(Type type, ComponentMapping description)
        {
            if (!Values.ContainsKey(type))
                Values[type] = description;
        }

        /// <summary>
        /// Update check for mesh filters.
        /// </summary>
        private static readonly Dictionary<MeshFilter, MeshFilterUpdate> _meshFilterUpdates = new();

        /// <summary>
        /// Mesh filter update.
        /// </summary>
        private class MeshFilterUpdate
        {
            /// <summary>
            /// Check if the vertices were updated.
            /// </summary>
            public bool Vertices { get; set; } = false;
            /// <summary>
            /// Check if the triangles were updated.
            /// </summary>
            public bool Triangles { get; set; } = false;
            /// <summary>
            /// Check if the normals were updated.
            /// </summary>
            public bool Normals { get; set; } = false;
            /// <summary>
            /// Check if the UVs were updated.
            /// </summary>
            public bool UVs { get; set; } = false;

            /// <summary>
            /// Check if the mesh filter update is complete.
            /// </summary>
            public bool IsComplete => Vertices && Triangles && Normals && UVs;
        }

        /// <summary>
        /// Parse a Vector3 from a string.
        /// </summary>
        /// <param name="value">String to parse. (0, 1, 2)
        /// <returns>Vector3 parsed.</returns> 
        private static Vector3 ParseVector3(string value)
        {
            string[] values = value.Replace("(", "").Replace(")", "").Replace(" ", "").Split(',');
            if (values.Length < 3)
                return Vector3.zero;
            return new Vector3(float.Parse(values[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(values[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(values[2], System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Parse a Vector2 from a string.
        /// </summary>
        /// <param name="value">String to parse. (0, 1)
        /// <returns>Vector2 parsed.</returns>
        private static Vector2 ParseVector2(string value)
        {
            string[] values = value.Replace("(", "").Replace(")", "").Replace(" ", "").Split(',');
            if (values.Length < 2)
                return Vector2.zero;
            return new Vector2(float.Parse(values[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(values[1], System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Get the properties of a component.
        /// </summary>
        /// <param name="component">Component to get the properties from.</param>
        /// <returns>Dictionary of properties of the component.</returns>
        public static Dictionary<string, Tuple<int, Action<object>>> GetSetters(Component component)
        {
            Dictionary<string, Tuple<int, Action<object>>> setters = new();
            if (Values.TryGetValue(component.GetType(), out var componentDescription))
            {
                foreach (Delegate del in componentDescription.Properties)
                    if (del.DynamicInvoke(component) is ComponentProperty propertyDescription)
                        setters.Add(propertyDescription.PredicateName, new(propertyDescription.Priority, propertyDescription.Setter));
            }
            return setters;
        }

        public static Dictionary<string, Tuple<int, Func<object>>> GetGetters(Component component)
        {
            Dictionary<string, Tuple<int, Func<object>>> getters = new();
            if (Values.TryGetValue(component.GetType(), out var componentDescription))
            {
                foreach (Delegate del in componentDescription.Properties)
                    if (del.DynamicInvoke(component) is ComponentProperty propertyDescription)
                        getters.Add(propertyDescription.PredicateName, new(propertyDescription.Priority, propertyDescription.Getter));
            }
            return getters;
        }

        /// <summary>
        /// Check if a component is mapped.
        /// </summary>
        /// <param name="type">Type of the component.</param>
        /// <returns>True if the component is mapped, false otherwise.</returns>
        public static bool ContainsKey(Type type)
        {
            if (Values.ContainsKey(type))
                return true;
            else foreach (var key in Values.Keys)
                if (key.IsAssignableFrom(type))
                    return true;
            return false;
        }

        /// <summary>
        /// Get the values of the properties of a component.
        /// </summary>
        /// <param name="type">Type of the component.</param>
        /// <returns>List of properties of the component.</returns>
        public static ComponentMapping GetValue(Type type)
        {
            if (Values.TryGetValue(type, out var value))
                return value;
            else foreach (Type key in Values.Keys)
                if (key.IsAssignableFrom(type))
                    return Values[key];
            return null;
        }

        /// <summary>
        /// Get the values of the component description.
        /// </summary>
        /// <param name="type">Type of the component.</param>
        /// <param name="componentDescription">Component description.</param>
        /// <returns>True if the component was found, false otherwise.</returns> 
        public static bool TryGetValue(Type type, out ComponentMapping componentDescription)
        {
            if (Values.TryGetValue(type, out componentDescription))
                return true;
            else foreach (Type key in Values.Keys)
                if (key.IsAssignableFrom(type))
                {
                    componentDescription = Values[key];
                    return true;
                }
            return false;
        }

        /// <summary>
        /// Check if a component has a property.
        /// </summary>
        /// <param name="type">Type of the component.</param>
        /// <param name="propertyName">Name of the property.</param>
        /// <returns>True if the component has the property, false otherwise.</returns>
        public static bool HasProperty(Type type, string propertyName)
        {
            if (Values.TryGetValue(type, out var componentDescription))
                return componentDescription.CachedProperties.ContainsKey(propertyName);
            else foreach (Type key in Values.Keys)
                if (key.IsAssignableFrom(type))
                    return Values[key].CachedProperties.ContainsKey(propertyName);
            return false;
        }

        /// <summary>
        /// Get the type of the component.
        /// </summary>
        /// <param name="typeName">Name of the component.</param>
        /// <returns>Type of the component.</returns>
        public static Tuple<Type, int> GetData(string typeName)
        {
            try
            {
                typeName = typeName.Contains(":") ? typeName.Split(':')[1] : typeName;
                foreach (var key in Values.Keys)
                {
                    //if (Values[key] == null) continue;
                    string typeNameKey = Values[key] != null ?
                        (Values[key].TypeName.Contains(":") ? Values[key].TypeName.Split(':')[1] : Values[key].TypeName) :
                        key.Name.Split()[^1];
                    if (typeNameKey == typeName)
                        return Values[key] != null ? new Tuple<Type, int>(key, Values[key].SortOrder) : null;
                }
                //Debug.LogWarning($"Type {typeName} not found in mapped components.");
                return null;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error getting type {typeName}: {e.Message}");
                return null;
            }
        }

        public static void AddMapping(Type type, int sortOrder = 0)
        {
            try
            {
                if (type.GetInterface(nameof(IComponentMapping)) == null) return;

                if (SvenSettings.Debug) Debug.Log($"Adding mapping for type {type.Name} with sort order {sortOrder}");

                var method = type.GetMethod("ComponentMapping", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    ?? throw new MissingMethodException($"Type {type.FullName} must implement a public static method 'ComponentMapping'.");
                ComponentMapping mapping = method.Invoke(null, null) as ComponentMapping
                    ?? throw new InvalidOperationException($"The 'ComponentMapping' method of {type.FullName} must return a valid ComponentMapping object.");
                AddComponentDescription(type, mapping);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error adding mapping for type {type.Name}: {e.Message}");
            }
        }

        public static void LoadAllMappedComponents()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.FullName.StartsWith("System.") && !a.FullName.StartsWith("Unity") && !a.IsDynamic);

            var types = new List<Type>();
            foreach (var assembly in assemblies)
            {
                try
                {
                    types.AddRange(
                        assembly.GetTypes()
                        .Where(type => typeof(IComponentMapping).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                    );
                }
                catch (System.Reflection.ReflectionTypeLoadException ex)
                {
                    types.AddRange(
                        ex.Types
                        .Where(type => type != null && typeof(IComponentMapping).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                    );
                }
                catch
                {
                    Debug.LogWarning($"Could not load types from assembly {assembly.FullName}. This might be due to a missing dependency or an unsupported type.");
                    continue;
                }
            }

            foreach (Type type in types)
                AddMapping(type);
        }
    }
}