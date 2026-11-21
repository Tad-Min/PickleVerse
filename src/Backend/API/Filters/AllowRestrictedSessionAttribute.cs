namespace API.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowRestrictedSessionAttribute : Attribute { }
