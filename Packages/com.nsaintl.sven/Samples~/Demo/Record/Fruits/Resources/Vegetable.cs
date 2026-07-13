using Sven.Content;

namespace Sven.Demo
{
    public abstract class Vegetable : Food, ISemanticAnnotation
    {
        public static new string SemanticTypeName => "sven:Vegetable";
    }
}
