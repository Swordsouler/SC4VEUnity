using Sven.Content;
using UnityEngine;

namespace Sc4ve.Demonstration
{
    public abstract class Container : MonoBehaviour, ISemanticAnnotation
    {
        public static string SemanticTypeName => "sven:Container";
    }
}
