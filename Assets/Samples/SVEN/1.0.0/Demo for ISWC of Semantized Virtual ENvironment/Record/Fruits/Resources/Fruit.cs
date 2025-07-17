using Sven.Content;

namespace Sven.Demo
{
    public abstract class Fruit : Food, ISemanticAnnotation
    {
        public static new string SemanticTypeName => "sven:Fruit";
    }
}
