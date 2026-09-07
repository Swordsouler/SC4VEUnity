using Sven.Content;

namespace Sc4ve.Demonstration
{
    public abstract class Station : Container, ISemanticAnnotation
    {
        public static new string SemanticTypeName => "sven:Station";
    }
}
