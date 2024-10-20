namespace Trelnex.Core.Client.Identity;

/// <summary>
/// Represents the status of a <see cref="TokenCredential"/>.
/// </summary>
/// <param name="CredentialName">Gets the name associated with the TokenCredential.</param>
/// <param name="Statuses">Gets the array of <see cref="AccessTokenStatus"/> within the <see cref="TokenCredential"/>.</param>
public record CredentialStatus(
    string CredentialName,
    AccessTokenStatus[] Statuses);
