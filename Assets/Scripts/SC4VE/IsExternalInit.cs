// Polyfill required for C# 9 init-only setters on Unity's compiler target.
// Unity ships a .NET Standard 2.1 / C# 9 compiler but doesn't include this
// type in the BCL it bundles, so we define it ourselves.
// See: https://developercommunity.visualstudio.com/t/error-cs0518-isexternalinit/1244809
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
