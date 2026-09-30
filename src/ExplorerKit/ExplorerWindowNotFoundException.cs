namespace SSP.ExplorerKit;

/// <summary>Thrown when an explicitly requested Explorer window does not exist.</summary>
public sealed class ExplorerWindowNotFoundException(string message) : Exception(message);
