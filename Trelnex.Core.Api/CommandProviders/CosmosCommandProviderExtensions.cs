using System.Configuration;
using Azure.Core;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Trelnex.Core.Client.Identity;
using Trelnex.Core.Data;

namespace Trelnex.Core.Api.CommandProviders;

/// <summary>
/// Extension method to add the necessary command providers to the <see cref="IServiceCollection"/>.
/// </summary>
public static class CosmosCommandProvidersExtensions
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
        var providerConfiguration = configuration.GetSection("CosmosCommandProviders").Get<CosmosCommandProviderConfiguration>();
        if (providerConfiguration is null) return services;

        // parse the cosmos options
        var options = CosmosCommandProviderOptions.Parse(providerConfiguration);

        // create our factory
        var cosmosClientOptions = GetCosmosClientOptions(bootstrapLogger, options);
        var keyResolverOptions = GetKeyResolverOptions(bootstrapLogger, options);

        var factoryTask = CosmosCommandProviderFactory.Create(
            cosmosClientOptions,
            keyResolverOptions);

        // create the command providers and inject
        var commandProviderOptions = new CommandProviderOptions(
            services: services,
            bootstrapLogger: bootstrapLogger,
            factory: factoryTask.Result!,
            options: options);

        configureCommandProviders(commandProviderOptions);

        return services;
    }

    /// <summary>
    /// Gets the <see cref="CosmosClientOptions"/> to be used by <see cref="CosmosClient"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Initializes an <see cref="AccessToken"/> with the necessary <see cref="CosmosClient"/> scopes.
    /// </para>
    /// </remarks>
    /// <param name="bootstrapLogger">The <see cref="ILogger"/> to write the CommandProvider bootstrap logs.</param>
    /// <param name="options">The <see cref="CosmosCommandProviderOptions"/>.</param>
    /// <returns>A valid <see cref="CosmosClientOptions"/>.</returns>
    private static CosmosClientOptions GetCosmosClientOptions(
        ILogger bootstrapLogger,
        CosmosCommandProviderOptions options)
    {
        // get the token credential and initialize
        var tokenCredential = CredentialFactory.Get(bootstrapLogger, "CosmosClient");

        // format the scope
        var uri = new Uri(options.EndpointUri);

        var scope = new UriBuilder(
            scheme: uri.Scheme,
            host: uri.Host,
            port: uri.Port,
            path: ".default",
            extraValue: uri.Query).Uri.ToString();

        var tokenRequestContext = new TokenRequestContext(
            scopes: [scope]);

        tokenCredential.GetToken(tokenRequestContext, default);

        return new CosmosClientOptions(
            AccountEndpoint: options.EndpointUri,
            TokenCredential: tokenCredential,
            DatabaseId: options.DatabaseId,
            ContainerIds: options.GetContainerIds());
    }

    /// <summary>
    /// Gets a <see cref="KeyResolverOptions"/> to be used by <see cref="KeyResolver"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Initializes an <see cref="AccessToken"/> with the necessary <see cref="KeyResolver"/> scopes.
    /// </para>
    /// </remarks>
    /// <param name="bootstrapLogger">The <see cref="ILogger"/> to write the CommandProvider bootstrap logs.</param>
    /// <param name="options">The <see cref="CosmosCommandProviderOptions"/>.</param>
    /// <returns>A valid <see cref="KeyResolverOptions"/>.</returns>
    private static KeyResolverOptions GetKeyResolverOptions(
        ILogger bootstrapLogger,
        CosmosCommandProviderOptions options)
    {
        // get the token credential and initialize
        var tokenCredential = CredentialFactory.Get(bootstrapLogger, "KeyResolver");

        var tokenRequestContext = new TokenRequestContext(
            scopes: ["https://vault.azure.net/.default"],
            tenantId: options.TenantId);

        tokenCredential.GetToken(tokenRequestContext, default);

        return new KeyResolverOptions(
            TokenCredential: tokenCredential);
    }

    private class CommandProviderOptions(
        IServiceCollection services,
        ILogger bootstrapLogger,
        CosmosCommandProviderFactory factory,
        CosmosCommandProviderOptions options)
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
            var containerId = options.GetContainerId(typeName);

            if (containerId is null)
            {
                throw new ArgumentException(
                    $"The Container for TypeName '{typeName}' is not found.",
                    nameof(typeName));
            }

            // create the command provider and inject
            var commandProvider = factory.Create<TInterface, TItem>(
                containerId: containerId,
                typeName: typeName,
                validator: itemValidator,
                commandOperations: commandOperations);

            services.AddSingleton(commandProvider);

            object[] args =
            [
                typeof(TInterface), // TInterface,
                typeof(TItem), // TItem,
                options.EndpointUri, // account
                options.DatabaseId, // database,
                containerId, // container
            ];

            // log - the :l format parameter (l = literal) to avoid the quotes
            bootstrapLogger.LogInformation(
                message: "Added CommandProvider<{TInterface:l}, {TItem:l}> using EndpointUri '{endpointUri:l}', DatabaseId '{databaseId:l}', and ContainerId '{containerId:l}'.",
                args: args);

            return this;
        }
    }

    /// <summary>
    /// Represents the container for the specified item type.
    /// </summary>
    /// <param name="TypeName">The specified item type name.</param>
    /// <param name="ContainerId">The container for the specified item type.</param>
    private record ContainerConfiguration(
        string TypeName,
        string ContainerId);

    /// <summary>
    /// Represents the configuration properties for Cosmos command providers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// https://github.com/dotnet/runtime/issues/83803
    /// </para>
    /// </remarks>
    private record CosmosCommandProviderConfiguration
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
        public required string DatabaseId { get; init; }

        /// <summary>
        /// The collection of containers by item type
        /// </summary>
        public required ContainerConfiguration[] Containers { get; init; }
    }

    /// <summary>
    /// Represents the Cosmos command provider options: the collection of containers by item type.
    /// </summary>
    private class CosmosCommandProviderOptions(
        string tenantId,
        string endpointUri,
        string databaseId)
    {
        /// <summary>
        /// The collection of containers by item type.
        /// </summary>
        private readonly Dictionary<string, string> _containerIdsByTypeName = [];

        /// <summary>
        /// Initialize an instance of <see cref="CosmosCommandProviderOptions"/>.
        /// </summary>
        /// <param name="providerConfiguration">The cosmos command providers configuration.</param>
        /// <returns>The <see cref="CosmosCommandProviderOptions"/>.</returns>
        /// <exception cref="AggregateException">Represents one or more configuration errors.</exception>
        public static CosmosCommandProviderOptions Parse(
            CosmosCommandProviderConfiguration providerConfiguration)
        {
            // get the tenant, endpoint, and database
            var options = new CosmosCommandProviderOptions(
                tenantId: providerConfiguration.TenantId,
                endpointUri: providerConfiguration.EndpointUri,
                databaseId: providerConfiguration.DatabaseId);

            // group the containers by item type
            var groups = providerConfiguration
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
                options._containerIdsByTypeName[group.Key] = group.Single().ContainerId;
            });

            return options;
        }

        /// <summary>
        /// Get the database.
        /// </summary>
        public string DatabaseId => databaseId;

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
        public string? GetContainerId(
            string typeName)
        {
            return _containerIdsByTypeName.TryGetValue(typeName, out var containerId)
                ? containerId
                : null;
        }

        /// <summary>
        /// Get the containers.
        /// </summary>
        /// <returns>The array of containers.</returns>
        public string[] GetContainerIds()
        {
            return _containerIdsByTypeName
                .Values
                .OrderBy(c => c)
                .ToArray();
        }
    }
}
