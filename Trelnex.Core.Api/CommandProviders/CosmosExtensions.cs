using System.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Security.KeyVault.Keys.Cryptography;
using FluentValidation;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Encryption;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Trelnex.Core.Client.Identity;
using Trelnex.Core.Data;

namespace Trelnex.Core.Api.CommandProviders;

/// <summary>
/// Extension method to add the necessary command providers to the <see cref="IServiceCollection"/>.
/// </summary>
public static class CosmosExtensions
{
    /// <summary>
    /// Add the necessary command providers as a <see cref="ICommandProvider{TInterface}"/> to the <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="configuration">Represents a set of key/value application configuration properties.</param>
    /// <param name="bootstrapLogger">The <see cref="ILogger"/> to write the CommandProvider bootstrap logs.</param>
    /// <param name="configureCommandProviders">The action to configure the command providers.</param>
    /// <returns>The <see cref="IServiceCollection"/>.</returns>
    public static IServiceCollection AddCosmosCommandProviders(
        this IServiceCollection services,
        IConfiguration configuration,
        ILogger bootstrapLogger,
        Action<ICommandProviderOptions> configureCommandProviders)
    {
        var cosmosConfiguration = configuration.GetSection("CosmosDB").Get<CosmosConfiguration>();
        if (cosmosConfiguration is null) return services;

        // parse the cosmos options
        var cosmosOptions = CosmosOptions.Parse(cosmosConfiguration);

        var jsonSerializerOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        // build our cosmos client
        var tokenCredentialForCosmosClient =
            GetTokenCredentialForCosmosClient(
                bootstrapLogger,
                cosmosOptions.EndpointUri);

        var cosmosClientTask =
            new CosmosClientBuilder(
                    cosmosOptions.EndpointUri,
                    tokenCredentialForCosmosClient)
            .WithCustomSerializer(new SystemTextJsonSerializer(jsonSerializerOptions))
            .WithHttpClientFactory(() => new HttpClient(new SocketsHttpHandler(), disposeHandler: false))
            .BuildAndInitializeAsync(
                cosmosOptions.GetContainers(),
                CancellationToken.None
            );

        cosmosClientTask.Wait();

        var tokenCredentialForKeyResolver =
            GetTokenCredentialForKeyResolver(
                bootstrapLogger,
                cosmosOptions.TenantId);

        var keyResolver = new KeyResolver(tokenCredentialForCosmosClient);

        var cosmosClient = cosmosClientTask.Result!
            .WithEncryption(keyResolver, KeyEncryptionKeyResolverName.AzureKeyVault);

        // get the container, create the command provider, and inject
        var commandProviderOptions = new CommandProviderOptions(
            services: services,
            bootstrapLogger: bootstrapLogger,
            getContainer: typeName =>
                cosmosClient.GetContainer(
                    cosmosOptions.Database,
                    cosmosOptions.GetContainer(typeName)));

        // inject any needed command providers
        configureCommandProviders(commandProviderOptions);

        return services;
    }

    /// <summary>
    /// Gets a <see cref="TokenCredential"/> to be used by <see cref="CosmosClient"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Initializes an <see cref="AccessToken"/> with the necessary <see cref="CosmosClient"/> scopes.
    /// </para>
    /// </remarks>
    /// <param name="bootstrapLogger">The <see cref="ILogger"/> to write the CommandProvider bootstrap logs.</param>
    /// <param name="endpointUri">The Uri to the Cosmos Account.</param>
    /// <returns>A valid <see cref="TokenCredential"/>.</returns>
    private static TokenCredential GetTokenCredentialForCosmosClient(
        ILogger bootstrapLogger,
        string endpointUri)
    {
        // get the token credential and initialize
        var tokenCredential = CredentialFactory.Get(bootstrapLogger, "CosmosClient");

        // format the scope
        var uri = new Uri(endpointUri);

        var scope = new UriBuilder(
            scheme: uri.Scheme,
            host: uri.Host,
            port: uri.Port,
            path: ".default",
            extraValue: uri.Query).Uri.ToString();

        var tokenRequestContext = new TokenRequestContext(
            scopes: [scope]);

        tokenCredential.GetToken(tokenRequestContext, default);

        return tokenCredential;
    }

    /// <summary>
    /// Gets a <see cref="TokenCredential"/> to be used by <see cref="KeyResolver"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Initializes an <see cref="AccessToken"/> with the necessary <see cref="KeyResolver"/> scopes.
    /// </para>
    /// </remarks>
    /// <param name="bootstrapLogger">The <see cref="ILogger"/> to write the CommandProvider bootstrap logs.</param>
    /// <param name="tenantId">The id of the Azure tenant (represents the organization).</param>
    /// <returns>A valid <see cref="TokenCredential"/>.</returns>
    private static TokenCredential GetTokenCredentialForKeyResolver(
        ILogger bootstrapLogger,
        string tenantId)
    {
        // get the token credential and initialize
        var tokenCredential = CredentialFactory.Get(bootstrapLogger, "KeyResolver");

        var tokenRequestContext = new TokenRequestContext(
            scopes: ["https://vault.azure.net/.default"],
            tenantId: tenantId);

        tokenCredential.GetToken(tokenRequestContext, default);

        return tokenCredential;
    }

    /// <summary>
    /// Represents the Cosmos options: the collection containers by item type.
    /// </summary>
    private class CosmosOptions(
        string tenantId,
        string endpointUri,
        string database)
    {
        /// <summary>
        /// The collection of containers by item type.
        /// </summary>
        private readonly Dictionary<string, string> _containerByTypeName = [];

        /// <summary>
        /// Initialize an instance of <see cref="CosmosOptions"/>.
        /// </summary>
        /// <param name="cosmosConfiguration">The cosmos configuration.</param>
        /// <returns>The <see cref="CosmosOptions"/>.</returns>
        /// <exception cref="AggregateException">Represents one or more configuration errors.</exception>
        public static CosmosOptions Parse(
            CosmosConfiguration cosmosConfiguration)
        {
            // get the defaults
            var containerOverrides = new CosmosOptions(
                tenantId: cosmosConfiguration.TenantId,
                endpointUri: cosmosConfiguration.EndpointUri,
                database: cosmosConfiguration.Database);

            // group the containers by item type
            var groups = cosmosConfiguration
                .Containers
                .GroupBy(o => o.TypeName)
                .ToArray();

            // any exceptions
            var exs = new List<ConfigurationErrorsException>();

            // enumerate each group - should be one
            Array.ForEach(groups, group =>
            {
                if (group.Count() <= 1) return;

                exs.Add(new ConfigurationErrorsException($"A Container for TypeName '{group.Key} is specified more than once."));
            });

            // if there are any exceptions, then throw an aggregate exception of all exceptions
            if (exs.Count > 0)
            {
                throw new AggregateException(exs);
            }

            // enumerate each group and set the container (value) for each item type (key)
            Array.ForEach(groups, group =>
            {
                containerOverrides._containerByTypeName[group.Key] = group.Single().Container;
            });

            return containerOverrides;
        }

        /// <summary>
        /// Get the database.
        /// </summary>
        public string Database => database;

        /// <summary>
        /// Get the endpoint.
        /// </summary>
        public string EndpointUri => endpointUri;

        /// <summary>
        /// Get the tenant id.
        /// </summary>
        public string TenantId => tenantId;

        /// <summary>
        /// Get the container for the specified item type.
        /// </summary>
        /// <param name="typeName">The specified item type.</param>
        /// <returns>The container for the specified item type.</returns>
        public string? GetContainer(
            string typeName)
        {
            return _containerByTypeName.TryGetValue(typeName, out var container)
                ? container
                : null;
        }

        /// <summary>
        /// Get the database - container pairs.
        /// </summary>
        /// <returns>The database - container pairs.</returns>
        public IReadOnlyList<(string, string)> GetContainers()
        {
            // initialize with the default container
            List<(string, string)> containers = [];

            // add the overrides - the value is the container
            containers.AddRange(_containerByTypeName.Select(kvp => (database, kvp.Value)));

            return containers;
        }
    }

    /// <summary>
    /// Represents the configuration properties for Cosmos command providers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// https://github.com/dotnet/runtime/issues/83803
    /// </para>
    /// </remarks>
    private record CosmosConfiguration
    {
        /// <summary>
        /// The id of the Azure tenant (represents the organization).
        /// </summary>
        public required string TenantId { get; init; }

        /// <summary>
        /// The Uri to the Cosmos Account.
        /// </summary>
        public required string EndpointUri { get; init; }

        /// <summary>
        /// The database name to initialize.
        /// </summary>
        public required string Database { get; init; }

        /// <summary>
        /// The collection of containers by item type
        /// </summary>
        public required ContainerConfiguration[] Containers { get; init; }
    }

    /// <summary>
    /// Represents the container for the specified item type.
    /// </summary>
    /// <param name="TypeName">The specified item type name.</param>
    /// <param name="Container">The container for the specified item type.</param>
    private record ContainerConfiguration(
        string TypeName,
        string Container);

    private class CommandProviderOptions(
        IServiceCollection services,
        ILogger bootstrapLogger,
        Func<string, Container> getContainer)
        : ICommandProviderOptions
    {
        public ICommandProviderOptions Add<TInterface, TItem>(
            string typeName,
            AbstractValidator<TItem>? itemValidator = null,
            CommandOperations? commandOperations = null)
            where TInterface : class, IBaseItem
            where TItem : BaseItem, TInterface, new()
        {
            // get the container for the specified item type
            var container = getContainer(typeName);

            // create the command provider and inject it
            var commandProvider = CosmosCommandProvider.Create<TInterface, TItem>(
                container,
                typeName,
                itemValidator,
                commandOperations);

            object[] args =
            [
                typeof(TInterface), // TInterface,
                typeof(TItem), // TItem,
                container.Database.Id, // database,
                container.Id, // container
            ];

            // log - the :l format parameter (l = literal) to avoid the quotes
            bootstrapLogger.LogInformation(
                message: "Added CommandProvider<{TInterface:l}, {TItem:l}> using Database '{database:l}' and Container '{container:l}'.",
                args: args);

            services.AddSingleton(commandProvider);

            return this;
        }
    }
}
