using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Security.KeyVault.Keys.Cryptography;
using FluentValidation;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Encryption;
using Microsoft.Azure.Cosmos.Fluent;

namespace Trelnex.Core.Data;

/// <summary>
/// A builder for creating an instance of the <see cref="CosmosCommandProvider"/>.
/// </summary>
public class CosmosCommandProviderFactory
{
    private readonly CosmosClient _cosmosClient;
    private readonly string _databaseId;

    private CosmosCommandProviderFactory(
        CosmosClient cosmosClient,
        string databaseId)
    {
        _cosmosClient = cosmosClient;
        _databaseId = databaseId;
    }

    /// <summary>
    /// Create an instance of the <see cref="CosmosCommandProviderFactory"/>.
    /// </summary>
    /// <param name="cosmosClientOptions">The <see cref="CosmosClient"/> options.</param>
    /// <param name="keyResolverOptions">The <see cref="KeyResolver"/> options.</param>
    /// <returns>The <see cref="CosmosCommandProviderFactory"/>.</returns>
    public static async Task<CosmosCommandProviderFactory> Create(
        CosmosClientOptions cosmosClientOptions,
        KeyResolverOptions keyResolverOptions)
    {
        var jsonSerializerOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        // build the list of ( database, container ) tuples 
        var containers = cosmosClientOptions.ContainerIds
            .Select(container => (cosmosClientOptions.DatabaseId, container))
            .ToList()
            .AsReadOnly();

        // create the cosmos client
        var cosmosClient = await
            new CosmosClientBuilder(
                cosmosClientOptions.AccountEndpoint,
                cosmosClientOptions.TokenCredential)
            .WithCustomSerializer(new SystemTextJsonSerializer(jsonSerializerOptions))
            .WithHttpClientFactory(() => new HttpClient(new SocketsHttpHandler(), disposeHandler: false))
            .BuildAndInitializeAsync(
                containers,
                CancellationToken.None
            );

        // add encryption
        cosmosClient = cosmosClient.WithEncryption(
            new KeyResolver(keyResolverOptions.TokenCredential),
            KeyEncryptionKeyResolverName.AzureKeyVault);

        return new CosmosCommandProviderFactory(
            cosmosClient,
            cosmosClientOptions.DatabaseId);
    }

    /// <summary>
    /// Create an instance of the <see cref="CosmosCommandProvider"/>.
    /// </summary>
    /// <param name="containerId">The Cosmos container as the backing data store.</param>
    /// <param name="typeName">The type name of the item - used for <see cref="BaseItem.TypeName"/>.</param>
    /// <param name="validator">The fluent validator for the item.</param>
    /// <param name="commandOperations">The value indicating if update and delete commands are allowed. By default, update is allowed; delete is not allowed.</param>
    /// <typeparam name="TInterface">The specified interface type.</typeparam>
    /// <typeparam name="TItem">The specified item type that implements the specified interface type.</typeparam>
    /// <returns>The <see cref="CosmosCommandProvider"/>.</returns>
    public ICommandProvider<TInterface> Create<TInterface, TItem>(
        string containerId,
        string typeName,
        AbstractValidator<TItem>? validator = null,
        CommandOperations? commandOperations = null)
        where TInterface : class, IBaseItem
        where TItem : BaseItem, TInterface, new()
    {
        var container = _cosmosClient.GetContainer(
            _databaseId,
            containerId);

        return new CosmosCommandProvider<TInterface, TItem>(
            container,
            typeName,
            validator,
            commandOperations);
    }
}

public record CosmosClientOptions(
    TokenCredential TokenCredential,
    string AccountEndpoint,
    string DatabaseId,
    string[] ContainerIds);

public record KeyResolverOptions(
    TokenCredential TokenCredential);
