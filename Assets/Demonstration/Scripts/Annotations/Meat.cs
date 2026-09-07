using Sven.Content;
using Sven.Demo;

namespace Sc4ve.Demonstration
{
    public abstract class Meat : Food, ISemanticAnnotation
    {
        public static new string SemanticTypeName => "sven:Meat";
    }
}
