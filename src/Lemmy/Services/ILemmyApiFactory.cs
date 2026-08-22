using Lemmy.Api;
using Lemmy.Domain;

namespace Lemmy.Services;

/// <summary>
/// Creates a read client for a given instance. Exists so that switching servers is a view-model
/// concern rather than an HTTP one, and so tests can hand the app a stub without a socket in sight.
/// </summary>
public interface ILemmyApiFactory
{
    /// <summary>
    /// Creates a client pointed at <paramref name="instance"/>, acting as <paramref name="session"/>
    /// when one is supplied. A client is bound to its session for its lifetime, which is what keeps
    /// a signed-out read from accidentally carrying someone's token.
    /// </summary>
    ILemmyApi Create(InstanceAddress instance, SessionToken session = default);
}
