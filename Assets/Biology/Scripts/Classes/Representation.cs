using System.Collections.Generic;
using UnityEngine;

// This script is used to manage differents visual representations of the molecules
public class Representation
{
    // Representations of the molecule (atoms + bonds)
    public enum Type { Normal, Peppytide, Stick, Tangible, OldPeppytide };
    public const float realWorldAtomSize = 0.0466f;
    // List all the representations and their properties
    public static Dictionary<Type, Data> data = new Dictionary<Type, Data>()
    {
        {
            // Default representation
            Type.Normal,
            new Data {
                type = Type.Normal,
                scale = Data.InitScale(new Dictionary<string, float> {{"atom", realWorldAtomSize}, {"bond", realWorldAtomSize}}),
                collision = false
            }
        }, {
            // Peppytide representation
            Type.Peppytide,
            new Data {
                type = Type.Peppytide,
                scale = Data.InitScale(new Dictionary<string, float> {{"atom", 2.3f * realWorldAtomSize}, {"bond", 0.0f * realWorldAtomSize}}),
                collision = false
            }
        }, {
            // Stick representation
            Type.Stick,
            new Data {
                type = Type.Stick,
                scale = Data.InitScale(new Dictionary<string, float> {{"atom", 0.3f * realWorldAtomSize}, {"bond", 1.0f * realWorldAtomSize}, {"C", 1.0f}, {"N", 1.0f}, {"O", 1.0f}, {"H", 1.0f}}),
                collision = false
            }
        }, {
            // Tangible representation (used for the tangible mode)
            Type.Tangible,
            new Data {
                type = Type.Tangible,
                scale = Data.InitScale(new Dictionary<string, float> {{"atom", realWorldAtomSize}, {"bond", realWorldAtomSize*2f}, {"C", 1.0f}, {"N", 1.0f}, {"O", 1.0f}, {"H", 1.0f}}),
                collision = true
            }
        }, {
            // OldPeppytide representation (used for the old peppytide mode)
            Type.OldPeppytide,
            new Data {
                type = Type.Tangible,
                scale = Data.InitScale(new Dictionary<string, float> {{"atom", realWorldAtomSize * 1f}, {"bond", 0.0f * realWorldAtomSize}, {"C", 1.0f}, {"N", 0.9f}, {"O", 0.9f}, {"H", 0.8f}}),
                collision = false
            }
        }
    };

    // Load the meshes of the representations
    public static void LoadMeshes()
    {
        // Add the default mesh to all the types that use it
        data[Type.Normal].meshes = new Dictionary<string, Mesh>() { };
        data[Type.Peppytide].meshes = new Dictionary<string, Mesh>() { };
        data[Type.Stick].meshes = new Dictionary<string, Mesh>() { };
        data[Type.OldPeppytide].meshes = new Dictionary<string, Mesh>() { };

        // Load all the meshes in the Tangible folder
        data[Type.Tangible].meshes = new Dictionary<string, Mesh>() { };
        foreach (Mesh mesh in Resources.LoadAll<Mesh>("Models/Atoms/Tangible"))
        {
            data[Type.Tangible].meshes.Add(mesh.name, mesh);
        }
    }

    // Load materials of the representations
    public static void LoadMaterials()
    {
        // Load the default mesh
        Material defaultMaterial = Resources.Load<Material>("Materials/Default");
        // Add the default mesh to all the types that use it
        data[Type.Normal].material = defaultMaterial;
        data[Type.Peppytide].material = defaultMaterial;
        data[Type.Stick].material = defaultMaterial;
        data[Type.Tangible].material = defaultMaterial;
        data[Type.OldPeppytide].material = defaultMaterial;
    }

    public class Data
    {
        // Type of the representation
        public Type type { get; set; }

        // Different scales for different types of atoms
        public Dictionary<string, float> scale = defaultScale;

        // If true, the atoms will collide with each other
        public bool collision { get; set; } = true;

        // Meshes of the atoms (used for the tangible mode)
        public Dictionary<string, Mesh> meshes { get; set; }

        // Material of the atom and his bonds
        public Material material { get; set; }

        // Colors of the atoms
        public Dictionary<string, Color> colors = new Dictionary<string, Color>() {
            { "C", new Color(0.0f, 0.0f, 0.0f) },
            { "N", new Color(0.0f, 1.0f, 1.0f) },
            { "O", new Color(1.0f, 0.0f, 0.0f) },
            { "H", new Color(1.0f, 1.0f, 1.0f) },
            { "default", new Color(0.5f, 0.5f, 0.5f) }
        };


        // Default scale for different types of atoms
        public static Dictionary<string, float> defaultScale = new Dictionary<string, float>() { { "bond", 1.0f }, { "atom", 1.0f }, { "C", 1.0f }, { "N", 0.8f }, { "O", 0.8f }, { "H", 0.6f } };

        // Initializes the scale of the object. If the object already has a scale, it will be replaced by the new scale.
        public static Dictionary<string, float> InitScale(Dictionary<string, float> scale)
        {
            Dictionary<string, float> newScale = new Dictionary<string, float>();
            foreach (KeyValuePair<string, float> entry in defaultScale)
            {
                newScale.Add(entry.Key, entry.Value);
            }
            foreach (KeyValuePair<string, float> entry in scale)
            {
                newScale[entry.Key] = entry.Value;
            }
            return newScale;
        }
    }
}
