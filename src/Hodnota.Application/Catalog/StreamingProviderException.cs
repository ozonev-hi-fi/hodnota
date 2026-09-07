namespace Hodnota.Application.Catalog;

public sealed class StreamingProviderException(string message, Exception innerException) : Exception(message, innerException);
