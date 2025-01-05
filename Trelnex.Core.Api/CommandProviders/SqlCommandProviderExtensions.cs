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
public static class SqlCommandProvidersExtensions
{
    /// <summary>
    /// Add the necessary command providers as a <see cref="ICommandProvider{TInterface}"/> to the <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="configuration">Represents a set of key/value application configuration properties.</param>
    /// <param name="bootstrapLogger">The <see cref="ILogger"/> to write the CommandProvider bootstrap logs.</param>
    /// <param name="configureCommandProviders">The action to configure the command providers.</param>
    /// <returns>The <see cref="IServiceCollection"/>.</returns>
    public static IServiceCollection AddSqlCommandProviders(
        this IServiceCollection services,
        IConfiguration configuration,
        ILogger bootstrapLogger,
        Action<ICommandProviderOptions> configureCommandProviders)
    {
        var providerConfiguration = configuration.GetSection("SqlCommandProviders").Get<SqlCommandProviderConfiguration>();
        if (providerConfiguration is null) return services;

        // parse the sql options
        var options = SqlCommandProviderOptions.Parse(providerConfiguration);

        // create our factory
        var sqlClientOptions = GetSqlClientOptions(bootstrapLogger, options);

        var factoryTask = SqlCommandProviderFactory.Create(
            sqlClientOptions);

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
    /// Gets the <see cref="SqlClientOptions"/> to be used by <see cref="SqlClient"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Initializes an <see cref="AccessToken"/> with the necessary <see cref="SqlClient"/> scopes.
    /// </para>
    /// </remarks>
    /// <param name="bootstrapLogger">The <see cref="ILogger"/> to write the CommandProvider bootstrap logs.</param>
    /// <param name="options">The <see cref="SqlCommandProviderOptions"/>.</param>
    /// <returns>A valid <see cref="SqlClientOptions"/>.</returns>
    private static SqlClientOptions GetSqlClientOptions(
        ILogger bootstrapLogger,
        SqlCommandProviderOptions options)
    {
        // get the token credential and initialize
        var tokenCredential = CredentialFactory.Get(bootstrapLogger, "SqlClient");

        // format the scope
        var scope = "https://database.windows.net/.default";

        var tokenRequestContext = new TokenRequestContext(
            scopes: [scope]);

        tokenCredential.GetToken(tokenRequestContext, default);

        return new SqlClientOptions(
            TokenCredential: tokenCredential,
            Scope: scope,
            DataSource: options.DataSource,
            InitialCatalog: options.InitialCatalog
        );
    }

    private class CommandProviderOptions(
        IServiceCollection services,
        ILogger bootstrapLogger,
        SqlCommandProviderFactory factory,
        SqlCommandProviderOptions options)
        : ICommandProviderOptions
    {
        public ICommandProviderOptions Add<TInterface, TItem>(
            string typeName,
            AbstractValidator<TItem>? itemValidator = null,
            CommandOperations? commandOperations = null)
            where TInterface : class, IBaseItem
            where TItem : BaseItem, TInterface, new()
        {
            // get the table for the specified item type
            var tableName = options.GetTableName(typeName);

            if (tableName is null)
            {
                throw new ArgumentException(
                    $"The Table for TypeName '{typeName}' is not found.",
                    nameof(typeName));
            }

            // create the command provider and inject
            var commandProvider = factory.Create<TInterface, TItem>(
                tableName: tableName,
                typeName: typeName,
                validator: itemValidator,
                commandOperations: commandOperations);

            services.AddSingleton(commandProvider);

            object[] args =
            [
                typeof(TInterface), // TInterface,
                typeof(TItem), // TItem,
                options.DataSource, // server
                options.InitialCatalog, // database,
                tableName, // table
            ];

            // log - the :l format parameter (l = literal) to avoid the quotes
            bootstrapLogger.LogInformation(
                message: "Added CommandProvider<{TInterface:l}, {TItem:l}> using DataSource '{dataSource:l}', InitialCatalog '{initialCatalog:l}', and TableName '{tableName:l}'.",
                args: args);

            return this;
        }
    }

    /// <summary>
    /// Represents the table for the specified item type.
    /// </summary>
    /// <param name="TypeName">The specified item type name.</param>
    /// <param name="TableId">The table for the specified item type.</param>
    private record TableConfiguration(
        string TypeName,
        string TableName);

    /// <summary>
    /// Represents the configuration properties for SQL command providers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// https://github.com/dotnet/runtime/issues/83803
    /// </para>
    /// </remarks>
    private record SqlCommandProviderConfiguration
    {
        /// <summary>
        /// The name/network address to the SQL Server.
        /// </summary>
        public required string DataSource { get; init; }

        /// <summary>
        /// The database name to initialize.
        /// </summary>
        public required string InitialCatalog { get; init; }

        /// <summary>
        /// The collection of tables by item type
        /// </summary>
        public required TableConfiguration[] Tables { get; init; }
    }

    /// <summary>
    /// Represents the SQL command provider options: the collection of tables by item type.
    /// </summary>
    private class SqlCommandProviderOptions(
        string dataSource,
        string initialCatalog)
    {
        /// <summary>
        /// The collection of tables by item type.
        /// </summary>
        private readonly Dictionary<string, string> _tableNamesByTypeName = [];

        /// <summary>
        /// Initialize an instance of <see cref="SqlCommandProviderOptions"/>.
        /// </summary>
        /// <param name="providerConfiguration">The sql command providers configuration.</param>
        /// <returns>The <see cref="SqlCommandProviderOptions"/>.</returns>
        /// <exception cref="AggregateException">Represents one or more configuration errors.</exception>
        public static SqlCommandProviderOptions Parse(
            SqlCommandProviderConfiguration providerConfiguration)
        {
            // get the server and database
            var options = new SqlCommandProviderOptions(
                dataSource: providerConfiguration.DataSource,
                initialCatalog: providerConfiguration.InitialCatalog);

            // group the tables by item type
            var groups = providerConfiguration
                .Tables
                .GroupBy(o => o.TypeName)
                .ToArray();

            // any exceptions
            var exs = new List<ConfigurationErrorsException>();

            // enumerate each group - should be one
            Array.ForEach(groups, group =>
            {
                if (group.Count() <= 1) return;

                exs.Add(new ConfigurationErrorsException($"A Table for TypeName '{group.Key} is specified more than once."));
            });

            // if there are any exceptions, then throw an aggregate exception of all exceptions
            if (exs.Count > 0)
            {
                throw new AggregateException(exs);
            }

            // enumerate each group and set the table (value) for each item type (key)
            Array.ForEach(groups, group =>
            {
                options._tableNamesByTypeName[group.Key] = group.Single().TableName;
            });

            return options;
        }

        /// <summary>
        /// Get the server.
        /// </summary>
        public string DataSource => dataSource;

        /// <summary>
        /// Get the database.
        /// </summary>
        public string InitialCatalog => initialCatalog;

        /// <summary>
        /// Get the table for the specified item type.
        /// </summary>
        /// <param name="typeName">The specified item type.</param>
        /// <returns>The table for the specified item type.</returns>
        public string? GetTableName(
            string typeName)
        {
            return _tableNamesByTypeName.TryGetValue(typeName, out var tableName)
                ? tableName
                : null;
        }

        /// <summary>
        /// Get the tables.
        /// </summary>
        /// <returns>The array of tables.</returns>
        public string[] GetTableNames()
        {
            return _tableNamesByTypeName
                .Values
                .OrderBy(tn => tn)
                .ToArray();
        }
    }
}
