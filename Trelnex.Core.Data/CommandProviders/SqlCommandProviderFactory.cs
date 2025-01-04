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

    private string _connectionString;
    private Action<DbConnection> _connectionInterceptor;

    private SqlCommandProviderFactory(
        string connectionString,
        Action<DbConnection> connectionInterceptor)
    {
        _connectionString = connectionString;
        _connectionInterceptor = connectionInterceptor;
    }

    /// <summary>
    /// Create an instance of the <see cref="SqlCommandProviderFactory"/>.
    /// </summary>
    /// <param name="sqlClientOptions">The <see cref="SqlClientOptions"/> options.</param>
    /// <returns>The <see cref="SqlCommandProviderFactory"/>.</returns>
    public static async Task<SqlCommandProviderFactory> Create(
        SqlClientOptions sqlClientOptions)
    {
        // build the data options
        var scsBuilder = new SqlConnectionStringBuilder()
        {
            DataSource = sqlClientOptions.DataSource,
            InitialCatalog = sqlClientOptions.InitialCatalog,
            Encrypt = true,
        };

        // build the connection interceptor
        var connectionInterceptor = new Action<DbConnection>(dbConnection =>
        {
            if (dbConnection is not SqlConnection sqlConnection) return;

            // get the access token
            var tokenRequestContext = new TokenRequestContext([ sqlClientOptions.Scope ]);
            var accessToken = sqlClientOptions.TokenCredential.GetToken(tokenRequestContext, default).Token;

            sqlConnection.AccessToken = accessToken;
        });

        // warm-up the connection
        var dataOptions = new DataOptions()
            .UseSqlServer(scsBuilder.ConnectionString)
            .UseBeforeConnectionOpened(connectionInterceptor);

        using var dataConnection = new DataConnection(dataOptions);

        var version = dataConnection.Query<string>("SELECT @@VERSION");

        var factory = new SqlCommandProviderFactory(
            scsBuilder.ConnectionString,
            connectionInterceptor);

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
}

public record SqlClientOptions(
    TokenCredential TokenCredential,
    string Scope,
    string DataSource,
    string InitialCatalog);
