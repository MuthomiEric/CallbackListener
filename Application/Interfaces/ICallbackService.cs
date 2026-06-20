using CallbackListener.Domain;

namespace CallbackListener.Application.Interfaces;

public interface ICallbackService
{
    Task<CallbackEntry> ProcessAsync(CallbackContext context, CancellationToken ct = default);
    Task<CallbackEntry?> ResendAsync(Guid id, string userId, CancellationToken ct = default);
}

public sealed record CallbackContext(
    string Slug,
    string SubPath,
    string Method,
    string SourceIp,
    string ContentType,
    Dictionary<string, string> Headers,
    Dictionary<string, string> Query,
    string RawBody,
    bool IsJsonBody
);
