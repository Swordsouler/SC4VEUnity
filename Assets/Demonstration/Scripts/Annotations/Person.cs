using Sven.Content;
using UnityEngine;

namespace Sc4ve.Demonstration
{
    public abstract class Person : MonoBehaviour, ISemanticAnnotation
    {
        public static string SemanticTypeName => "sven:Person";
    }
}
