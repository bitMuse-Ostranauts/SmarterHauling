// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// This attribute is required for extension methods to work in .NET Framework 4.7.2 
    /// when compiled with modern SDK. Unity/Mono sometimes can't find it at runtime.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly)]
    internal sealed class ExtensionAttribute : Attribute
    {
    }
}

