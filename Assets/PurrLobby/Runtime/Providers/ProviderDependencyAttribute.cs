using System;

namespace PurrNet.Lobby
{
    /// <summary>
    /// Declares a UPM package that a provider needs in order to function.
    /// Provider classes may declare more than one dependency; editor tooling uses
    /// this metadata to surface missing packages at the orchestration entry point.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public sealed class ProviderDependencyAttribute : Attribute
    {
        public ProviderDependencyAttribute(string packageName, string displayName)
        {
            this.packageName = packageName;
            this.displayName = displayName;
        }

        public string packageName { get; }

        public string displayName { get; }
    }
}
