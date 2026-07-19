using System.Net;

namespace Sonata.Desktop.Services;

public sealed class ApiException(HttpStatusCode statusCode, string? errorCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } =  statusCode;
    public string? ErrorCode { get; } = errorCode;
}