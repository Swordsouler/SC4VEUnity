using Sven.Content;
using UnityEngine;

namespace Sc4ve.Demonstration
{
    public abstract class FoodState : MonoBehaviour, ISemanticAnnotation
    {
        public static string SemanticTypeName => "sven:FoodState";
    }
}
