using Sven.Content;
using UnityEngine;

namespace Sven.Demo
{
    public abstract class Food : MonoBehaviour, ISemanticAnnotation
    {
        public static string SemanticTypeName => "sven:Food";
    }
}
