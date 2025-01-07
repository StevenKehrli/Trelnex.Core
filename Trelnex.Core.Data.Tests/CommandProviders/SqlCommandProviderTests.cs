using Azure.Core;
using Azure.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Trelnex.Core.Data.Tests.CommandProviders;

[Ignore("Requires a SQL server.")]
public class SqlCommandProviderTests : CommandProviderTests
{
    private readonly string _scope = "https://database.windows.net/.default";

    private TokenCredential _tokenCredential = null!;
    private string _connectionString = null!;
    private string _tableName = null!;

    [OneTimeSetUp]
    public async Task TestFixtureSetup()
    {
        // This method is called once prior to executing any of the tests in the fixture.

        // create the test configuration
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.User.json", optional: true, reloadOnChange: true)
            .Build();

        var providerConfiguration = configuration.GetSection("SqlCommandProviders").Get<SqlCommandProviderConfiguration>()!;

        var scsBuilder = new SqlConnectionStringBuilder()
        {
            DataSource = providerConfiguration.DataSource,
            InitialCatalog = providerConfiguration.InitialCatalog,
            Encrypt = true,
        };

        _connectionString = scsBuilder.ConnectionString;
        _tableName = providerConfiguration.TableName;

        // create the command provider
        _tokenCredential = new DefaultAzureCredential();

        var sqlClientOptions = new SqlClientOptions(
            TokenCredential: _tokenCredential,
            Scope: _scope,
            DataSource: providerConfiguration.DataSource,
            InitialCatalog: providerConfiguration.InitialCatalog,
            TableNames: [ providerConfiguration.TableName ]
        );

        var factory = await SqlCommandProviderFactory.Create(
            sqlClientOptions);

        _commandProvider = factory.Create<ITestItem, TestItem>(
            providerConfiguration.TableName,
            "test-item",
            TestItem.Validator,
            CommandOperations.All);
    }

    [TearDown]
    public void TestCleanup()
    {
        // This method is called after each test has run.
        using var sqlConnection = new SqlConnection(_connectionString);

        var tokenRequestContext = new TokenRequestContext([ _scope ]);
        sqlConnection.AccessToken = _tokenCredential.GetToken(tokenRequestContext, default).Token;

        sqlConnection.Open();

        var cmdText = $"DELETE FROM [{_tableName}-events]; DELETE FROM [{_tableName}];";
        var sqlCommand = new SqlCommand(cmdText, sqlConnection);

        sqlCommand.ExecuteNonQuery();
    }

    /// <summary>
    /// Represents the configuration properties for SQL command providers.
    /// </summary>
    private record SqlCommandProviderConfiguration(
        string DataSource,
        string InitialCatalog,
        string TableName);
}
