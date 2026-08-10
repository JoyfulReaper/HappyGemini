namespace HappyGemini.Extensibility;

/// <summary>
/// Marks an <see cref="IGeminiPage"/> implementation for automatic
/// dependency-injection registration.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class AutoRegisterGeminiPageAttribute : Attribute { }
