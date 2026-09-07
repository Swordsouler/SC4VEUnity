using Sven.Content;
using UnityEngine;

namespace Sc4ve.Demonstration
{
    public abstract class Furniture : MonoBehaviour, ISemanticAnnotation
    {
        public static string SemanticTypeName => "sven:Furniture";
    }
}
