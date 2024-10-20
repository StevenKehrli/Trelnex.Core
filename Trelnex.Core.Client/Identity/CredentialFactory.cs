using System.Collections.Concurrent;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;

namespace Trelnex.Core.Client.Identity;

/// <summary>
/// A Factory pattern to create a <see cref="TokenCredential"/>.
/// </summary>
public static class CredentialFactory
{
    /// <summary>
    /// The underlying <see cref="TokenCredential"/>.
    /// </summary>
    private static readonly TokenCredential _credential = GetCredential();

    /// <summary>
    /// A thread-safe collection of <see cref="string"/> to <see cref="NamedCredential"/>.
    /// </summary>
    private static readonly ConcurrentDictionary<string, Lazy<NamedCredential>> _namedCredentialsByName = new();

    public static TokenCredential Get(
        ILogger logger,
        string credentialName)
    {
        // https://andrewlock.net/making-getoradd-on-concurrentdictionary-thread-safe-using-lazy/
        var lazyNamedCredential =
            _namedCredentialsByName.GetOrAdd(
                key: credentialName,
                value: new Lazy<NamedCredential>(
                    () => new NamedCredential(logger, credentialName, _credential)));

        return lazyNamedCredential.Value;
    }

    /// <summary>
    /// Gets the <see cref="CredentialStatus"/> for the specified credential.
    /// </summary>
    /// <param name="credentialName">The name of the specified credential.</param>
    /// <returns>The <see cref="CredentialStatus"/>.</returns>
    /// <exception cref="KeyNotFoundException">.</exception>
    public static CredentialStatus GetStatus(
        string credentialName)
    {
        if (_namedCredentialsByName.TryGetValue(credentialName, out var lazyCredential))
        {
            return lazyCredential.Value.GetStatus();
        }

        throw new KeyNotFoundException();
    }

    /// <summary>
    /// Gets the array of known credential names.
    /// </summary>
    public static string[] CredentialNames => _namedCredentialsByName.Keys.ToArray();

    /// <summary>
    /// Create a new <see cref="ChainedTokenCredential"/> combining <see cref="WorkloadIdentityCredential"/> and <see cref="AzureCliCredential"/>.
    /// </summary>
    /// <returns>The <see cref="TokenCredential"/>.</returns>
    private static TokenCredential GetCredential()
    {
        return new ChainedTokenCredential(
            new WorkloadIdentityCredential(),
            new AzureCliCredential());
    }
}
