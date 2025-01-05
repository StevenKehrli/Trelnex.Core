using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using FluentValidation;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.Mapping;
using Microsoft.Data.SqlClient;

namespace Trelnex.Core.Data;

/// <summary>
/// A builder for creating an instance of the <see cref="SqlCommandProvider"/>.
/// </summary>
public class SqlCommandProviderFactory
{
    private readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _connectionString;
    private readonly Action<DbConnection> _connectionInterceptor;
    private readonly Func<SqlCommandProviderFactoryStatus> _getStatus;

    private SqlCommandProviderFactory(
        string dataSource,
        string initialCatalog,
        Action<DbConnection> connectionInterceptor)
    {
        // build the connection string
        var scsBuilder = new SqlConnectionStringBuilder()
        {
            DataSource = dataSource,
            InitialCatalog = initialCatalog,
            Encrypt = true,
        };

        _connectionString = scsBuilder.ConnectionString;
        _connectionInterceptor = connectionInterceptor;

        // build the health check
        var dataOptions = new DataOptions()
            .UseSqlServer(_connectionString)
            .UseBeforeConnectionOpened(connectionInterceptor);

        _getStatus = () =>
        {
            try
            {
                using var dataConnection = new DataConnection(dataOptions);

                // get the multi-line version string
                var version = dataConnection.Query<string>("SELECT @@VERSION");

                // split the version into each line
                char[] delimiterChars = [ '\r', '\n', '\t' ];

                var versionArray = version
                    .FirstOrDefault()?
                    .Split(delimiterChars, StringSplitOptions.RemoveEmptyEntries);

                return new SqlCommandProviderFactoryStatus(
                    DataSource: dataSource,
                    InitialCatalog: initialCatalog,
                    IsHealthy: true,
                    Version: versionArray,
                    Error: null);
            }
            catch (Exception ex)
            {
                return new SqlCommandProviderFactoryStatus(
                    DataSource: dataSource,
                    InitialCatalog: initialCatalog,
                    IsHealthy: false,
                    Version: null,
                    Error: ex.Message);
            }
        };
    }

    /// <summary>
    /// Create an instance of the <see cref="SqlCommandProviderFactory"/>.
    /// </summary>
    /// <param name="sqlClientOptions">The <see cref="SqlClientOptions"/> options.</param>
    /// <returns>The <see cref="SqlCommandProviderFactory"/>.</returns>
    public static async Task<SqlCommandProviderFactory> Create(
        SqlClientOptions sqlClientOptions)
    {
        // build the connection interceptor
        var connectionInterceptor = new Action<DbConnection>(dbConnection =>
        {
            if (dbConnection is not SqlConnection sqlConnection) return;

            // get the access token
            var tokenRequestContext = new TokenRequestContext([ sqlClientOptions.Scope ]);
            var accessToken = sqlClientOptions.TokenCredential.GetToken(tokenRequestContext, default).Token;

            sqlConnection.AccessToken = accessToken;
        });

        // build the factory
        var factory = new SqlCommandProviderFactory(
            dataSource: sqlClientOptions.DataSource,
            initialCatalog: sqlClientOptions.InitialCatalog,
            connectionInterceptor);

        // warm-up the connection
        var status = factory.GetStatus();

        return await Task.FromResult(factory);
    }

    /// <summary>
    /// Create an instance of the <see cref="SqlCommandProvider"/>.
    /// </summary>
    /// <param name="tableName">The SQL table as the backing data store.</param>
    /// <param name="typeName">The type name of the item - used for <see cref="BaseItem.TypeName"/>.</param>
    /// <param name="validator">The fluent validator for the item.</param>
    /// <param name="commandOperations">The value indicating if update and delete commands are allowed. By default, update is allowed; delete is not allowed.</param>
    /// <typeparam name="TInterface">The specified interface type.</typeparam>
    /// <typeparam name="TItem">The specified item type that implements the specified interface type.</typeparam>
    /// <returns>The <see cref="SqlCommandProvider"/>.</returns>
    public ICommandProvider<TInterface> Create<TInterface, TItem>(
        string tableName,
        string typeName,
        AbstractValidator<TItem>? validator = null,
        CommandOperations? commandOperations = null)
        where TInterface : class, IBaseItem
        where TItem : BaseItem, TInterface, new()
    {
        // build the mapping schema
        var mappingSchema = new MappingSchema();

        // add the metadata reader
        mappingSchema.AddMetadataReader(new JsonPropertyNameAttributeReader());

        mappingSchema.SetConverter<DateTime, DateTimeOffset>(dt => new DateTimeOffset(dt));
        mappingSchema.SetConverter<DateTimeOffset, DateTime>(dto => dto.UtcDateTime);

        var fmBuilder = new FluentMappingBuilder(mappingSchema);

        // map the item to its table ("<tableName>")
        fmBuilder.Entity<TItem>()
            .HasTableName(tableName)
            .Property(e => e.Id).IsPrimaryKey()
            .Property(e => e.PartitionKey).IsPrimaryKey();

        // map the event to its table ("<tableName>-events")
        fmBuilder.Entity<ItemEvent<TItem>>()
            .HasTableName($"{tableName}-events")
            .Property(e => e.Id).IsPrimaryKey()
            .Property(e => e.PartitionKey).IsPrimaryKey()
            .Property(e => e.Changes).HasConversion(
                changes => JsonSerializer.Serialize(changes, _jsonSerializerOptions),
                s => JsonSerializer.Deserialize<PropertyChange[]>(s, _jsonSerializerOptions))
            .Property(e => e.Context).HasConversion(
                context => JsonSerializer.Serialize(context, _jsonSerializerOptions),
                s => JsonSerializer.Deserialize<ItemEventContext>(s, _jsonSerializerOptions) ?? new ItemEventContext());

        fmBuilder.Build();

        // build the data options
        var dataOptions = new DataOptions()
            .UseSqlServer(_connectionString)
            .UseBeforeConnectionOpened(_connectionInterceptor)
            .UseMappingSchema(mappingSchema);

        return new SqlCommandProvider<TInterface, TItem>(
            dataOptions,
            typeName,
            validator,
            commandOperations);
    }

    public SqlCommandProviderFactoryStatus GetStatus() => _getStatus();
}

public record SqlClientOptions(
    TokenCredential TokenCredential,
    string Scope,
    string DataSource,
    string InitialCatalog);

public record SqlCommandProviderFactoryStatus(
    string DataSource,
    string InitialCatalog,
    bool IsHealthy,
    string[]? Version,
    string? Error);
