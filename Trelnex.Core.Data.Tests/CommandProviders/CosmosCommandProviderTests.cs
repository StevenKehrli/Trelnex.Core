using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Configuration;

namespace Trelnex.Core.Data.Tests.CommandProviders;

[Ignore("Requires a CosmosDB instance.")]
public class CosmosCommandProviderTests : CommandProviderTests
{
    private Container _container = null!;

    [OneTimeSetUp]
    public async Task TestFixtureSetup()
    {
        // This method is called once prior to executing any of the tests in the fixture.

        // create the test configuration
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.User.json", optional: true, reloadOnChange: true)
            .Build();

        var providerConfiguration = configuration.GetSection("CosmosCommandProviders").Get<CosmosCommandProviderConfiguration>()!;

        // create a cosmos client for cleanup
        var tokenCredential = new DefaultAzureCredential();

        var cosmosClient = new CosmosClient(
            accountEndpoint: providerConfiguration.EndpointUri,
            tokenCredential: tokenCredential);

        _container = cosmosClient.GetContainer(
            databaseId: providerConfiguration.DatabaseId,
            containerId: providerConfiguration.ContainerId);

        // create the command provider
        var cosmosClientOptions = new CosmosClientOptions(
            TokenCredential: tokenCredential,
            AccountEndpoint: providerConfiguration.EndpointUri,
            DatabaseId: providerConfiguration.DatabaseId,
            ContainerIds: [ providerConfiguration.ContainerId ]
        );

        var keyResolverOptions = new KeyResolverOptions(
            TokenCredential: tokenCredential);

        var factory = await CosmosCommandProviderFactory.Create(
            cosmosClientOptions,
            keyResolverOptions);

        _commandProvider = factory.Create<ITestItem, TestItem>(
            providerConfiguration.ContainerId,
            "test-item",
            TestItem.Validator,
            CommandOperations.All);
    }

    [TearDown]
    public async Task Cleanup()
    {
        // This method is called after each test case is run.

        var feedIterator = _container
            .GetItemLinqQueryable<CosmosItem>()
            .ToFeedIterator();

        while (feedIterator.HasMoreResults)
        {
            var feedResponse = await feedIterator.ReadNextAsync();

            foreach (var item in feedResponse)
            {
                await _container.DeleteItemAsync<CosmosItem>(
                    id: item.id,
                    partitionKey: new PartitionKey(item.partitionKey));
            }
        }
    }

    /// <summary>
    /// Represents the configuration properties for Cosmos command providers.
    /// </summary>
    private record CosmosCommandProviderConfiguration(
        string EndpointUri,
        string DatabaseId,
        string ContainerId);

    private record CosmosItem(
        string id,
        string partitionKey);
}
