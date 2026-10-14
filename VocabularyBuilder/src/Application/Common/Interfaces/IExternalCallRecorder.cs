using VocabularyBuilder.Domain.Entities.History;

namespace VocabularyBuilder.Application.Common.Interfaces;

/// <summary>
/// Writes a record of a request made to a model or a dictionary site.
/// </summary>
/// <remarks>
/// Saved at once and on a context of its own, not added to the caller's. A call is worth
/// recording whether or not the work around it succeeds - a model call made during an import
/// that then fails was still made and still paid for - and it often happens inside a query,
/// which never saves at all. Saving through the caller's context instead would either lose it
/// or push the caller's half-finished changes out early.
///
/// Never throws. A log that cannot be written is reported and dropped, since failing the
/// lookup over it would be the tail wagging the dog.
/// </remarks>
public interface IExternalCallRecorder
{
    /// <summary>
    /// Records the call, filling in the owner and anything the current
    /// <see cref="History.ExternalCallScope"/> knows that the call does not.
    /// </summary>
    Task RecordAsync(ExternalCallLog call);
}
