using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Configuration;
using Snapshooter.NUnit;
using Trelnex.Core.Data.CommandProviders;

namespace Trelnex.Core.Data.Tests.CommandProviders;

[Ignore("Requires a CosmosDB instance.")]
public class CosmosCommandProviderTests
{
    private readonly string _typeName = "test-item";

    private readonly string _partitionKey = "a3e68ca8-2555-4f03-b58f-c0e80bcd38f1";

    private Container _container = null!;

    private ICommandProvider<ITestItem> _commandProvider = null!;

    [OneTimeSetUp]
    public void TestFixtureSetup()
    {
        // This method is called once prior to executing any of the tests in the fixture.

        // create the test configuration
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.User.json", optional: true, reloadOnChange: true)
            .Build();

        var cosmosConfiguration = configuration.GetSection("CosmosDB").Get<CosmosConfiguration>()!;

        var jsonSerializerOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        // build our cosmos client
        var cosmosClientOptions = new CosmosClientOptions
        {
            Serializer = new SystemTextJsonSerializer(jsonSerializerOptions),
        };

        var cosmosClient = new CosmosClient(
            accountEndpoint: cosmosConfiguration.EndpointUri,
            tokenCredential: new DefaultAzureCredential(),
            clientOptions: cosmosClientOptions);

        // get the container
        _container = cosmosClient.GetContainer(cosmosConfiguration.Database, cosmosConfiguration.Container);

        // create the command provider
        _commandProvider = CosmosCommandProvider.Create<ITestItem, TestItem>(
            _container,
            _typeName,
            TestItem.Validator,
            CommandOperations.All);
    }

    private async Task DeleteItemsAsync<T>(
        string typeName)
    {
        // delete items from the container
        var feedIterator = _container
            .GetItemLinqQueryable<TestItem>()
            .Where(item => item.TypeName == typeName)
            .ToFeedIterator();

        while (feedIterator.HasMoreResults)
        {
            var response = await feedIterator.ReadNextAsync();

            foreach (var item in response)
            {
                await _container.DeleteItemAsync<TestItem>(
                    id: item.Id,
                    partitionKey: new PartitionKey(item.PartitionKey));
            }
        }
    }

    [TearDown]
    public async Task TestCleanup()
    {
        // delete items from the container
        await DeleteItemsAsync<ItemEvent<TestItem>>(ReservedTypeNames.Event);
        await DeleteItemsAsync<TestItem>(_typeName);
    }

    [Test]
    public async Task CreateCommand_SaveAsync()
    {
        var id = "2a4cb3ec-6624-4fc6-abc4-6a5db019f8f9";

        var startDateTime = DateTime.UtcNow;

        var requestContext = TestRequestContext.Create();

        var createCommand = _commandProvider.Create(
            id: id,
            partitionKey: _partitionKey);

        createCommand.Item.Message = "Message #1";

        // save it and read it back
        var created = await createCommand.SaveAsync(
            requestContext: requestContext,
            cancellationToken: default);

        Assert.That(created, Is.Not.Null);

        Snapshot.Match(
            created,
            matchOptions => matchOptions
                .Assert(fieldOption =>
                {
                    Assert.Multiple(() =>
                    {
                        var currentDateTime = DateTime.UtcNow;

                        // createdDate
                        Assert.That(
                            fieldOption.Field<DateTime>("Item.CreatedDate"),
                            Is.InRange(startDateTime, currentDateTime));

                        // updatedDate
                        Assert.That(
                            fieldOption.Field<DateTime>("Item.UpdatedDate"),
                            Is.InRange(startDateTime, currentDateTime));

                        // createdDate == updatedDate
                        Assert.That(
                            fieldOption.Field<DateTime>("Item.CreatedDate"),
                            Is.EqualTo(fieldOption.Field<DateTime>("Item.UpdatedDate")));

                        // _eTag
                        Assert.That(
                            fieldOption.Field<string>("Item.ETag"),
                            Is.Not.Default);
                    });
                }));
    }

    [Test]
    public async Task DeleteCommand_SaveAsync()
    {
        var id = "f8829dac-56f6-4448-829a-fac886aefb1b";

        var startDateTime = DateTime.UtcNow;

        var requestContext = TestRequestContext.Create();

        var createCommand = _commandProvider.Create(
            id: id,
            partitionKey: _partitionKey);

        createCommand.Item.Message = "Message #1";

        // save it
        await createCommand.SaveAsync(
            requestContext: requestContext,
            cancellationToken: default);

        var deleteCommand = await _commandProvider.DeleteAsync(
            id: id,
            partitionKey: _partitionKey);

        Assert.That(deleteCommand, Is.Not.Null);
        Assert.That(deleteCommand!.Item, Is.Not.Null);

        // save it and read it back
        var deleted = await deleteCommand.SaveAsync(
            requestContext: requestContext,
            cancellationToken: default);

        Assert.That(deleted, Is.Not.Null);

        Snapshot.Match(
            deleted!,
            matchOptions => matchOptions
                .Assert(fieldOption =>
                {
                    Assert.Multiple(() =>
                    {
                        var currentDateTime = DateTime.UtcNow;

                        // createdDate
                        Assert.That(
                            fieldOption.Field<DateTime>("Item.CreatedDate"),
                            Is.InRange(startDateTime, currentDateTime));

                        // updatedDate
                        Assert.That(
                            fieldOption.Field<DateTime>("Item.UpdatedDate"),
                            Is.InRange(startDateTime, currentDateTime));

                        // createdDate == updatedDate
                        Assert.That(
                            fieldOption.Field<DateTime>("Item.CreatedDate"),
                            Is.EqualTo(fieldOption.Field<DateTime>("Item.UpdatedDate")));

                        // deletedDate
                        Assert.That(
                            fieldOption.Field<DateTime?>("Item.DeletedDate"),
                            Is.InRange(startDateTime, currentDateTime));

                        // _eTag
                        Assert.That(
                            fieldOption.Field<string>("Item.ETag"),
                            Is.Not.Default);
                    });
                }));
    }

    [Test]
    public async Task ReadCommand_ReadAsync()
    {
        var id = "a8cf4bc4-745a-471c-8fb1-5d4e124bbde2";

        var startDateTime = DateTime.UtcNow;

        var requestContext = TestRequestContext.Create();

        var createCommand = _commandProvider.Create(
            id: id,
            partitionKey: _partitionKey);

        createCommand.Item.Message = "Message #1";

        // save it
        await createCommand.SaveAsync(
            requestContext: requestContext,
            cancellationToken: default);

        var read = await _commandProvider.ReadAsync(
            id: id,
            partitionKey: _partitionKey);

        Assert.That(read, Is.Not.Null);

        Snapshot.Match(
            read!,
            matchOptions => matchOptions
                .Assert(fieldOption =>
                {
                    Assert.Multiple(() =>
                    {
                        var currentDateTime = DateTime.UtcNow;

                        // createdDate
                        Assert.That(
                            fieldOption.Field<DateTime>("Item.CreatedDate"),
                            Is.InRange(startDateTime, currentDateTime));

                        // updatedDate
                        Assert.That(
                            fieldOption.Field<DateTime>("Item.UpdatedDate"),
                            Is.InRange(startDateTime, currentDateTime));

                        // createdDate == updatedDate
                        Assert.That(
                            fieldOption.Field<DateTime>("Item.CreatedDate"),
                            Is.EqualTo(fieldOption.Field<DateTime>("Item.UpdatedDate")));

                        // _eTag
                        Assert.That(
                            fieldOption.Field<string>("Item.ETag"),
                            Is.Not.Default);
                    });
                }));
    }

    [Test]
    public async Task UpdateCommandSave_SaveAsync()
    {
        var id = "7dded065-d204-4913-97ad-591e382baba5";

        var startDateTime = DateTime.UtcNow;

        var requestContext = TestRequestContext.Create();

        // create our command provider
        var commandProvider =
            InMemoryCommandProvider.Create<ITestItem, TestItem>(
                typeName: _typeName);

        var createCommand = commandProvider.Create(
            id: id,
            partitionKey: _partitionKey);

        createCommand.Item.Message = "Message #1";

        // save it
        await createCommand.SaveAsync(
            requestContext: requestContext,
            cancellationToken: default);

        var updateCommand = await commandProvider.UpdateAsync(
            id: id,
            partitionKey: _partitionKey);

        Assert.That(updateCommand, Is.Not.Null);
        Assert.That(updateCommand!.Item, Is.Not.Null);

        updateCommand.Item.Message = "Message #2";

        // save it and read it back
        var updated = await updateCommand.SaveAsync(
            requestContext: requestContext,
            cancellationToken: default);

        Assert.That(updated, Is.Not.Null);

        Snapshot.Match(
            updated!,
            matchOptions => matchOptions
                .Assert(fieldOption =>
                {
                    Assert.Multiple(() =>
                    {
                        var currentDateTime = DateTime.UtcNow;

                        // createdDate
                        Assert.That(
                            fieldOption.Field<DateTime>("Item.CreatedDate"),
                            Is.InRange(startDateTime, currentDateTime));

                        // updatedDate
                        Assert.That(
                            fieldOption.Field<DateTime>("Item.UpdatedDate"),
                            Is.InRange(startDateTime, currentDateTime));

                        // createdDate != updatedDate
                        Assert.That(
                            fieldOption.Field<DateTime>("Item.CreatedDate"),
                            Is.Not.EqualTo(fieldOption.Field<DateTime>("Item.UpdatedDate")));

                        // _eTag
                        Assert.That(
                            fieldOption.Field<string>("Item.ETag"),
                            Is.Not.Default);
                    });
                }));
    }

    [Test]
    public async Task QueryCommand_ToAsyncEnumerable()
    {
        var id1 = "3fca6d8a-75c1-491a-9178-90343551364a";
        var id2 = "648de92a-b7e8-41c5-a5d2-bdf0cc65d67c";

        var requestContext = TestRequestContext.Create();

        // create our command provider
        var commandProvider =
            InMemoryCommandProvider.Create<ITestItem, TestItem>(
                typeName: _typeName);

        var createCommand1 = commandProvider.Create(
            id: id1,
            partitionKey: _partitionKey);

        createCommand1.Item.Message = "Message #1";

        // save it
        await createCommand1.SaveAsync(
            requestContext: requestContext,
            cancellationToken: default);

        var createCommand2 = commandProvider.Create(
            id: id2,
            partitionKey: _partitionKey);

        createCommand2.Item.Message = "Message #2";

        // save it
        await createCommand2.SaveAsync(
            requestContext: requestContext,
            cancellationToken: default);

        // query
        var queryCommand = commandProvider.Query();

        // should return both items
        var read = await queryCommand.ToAsyncEnumerable().ToArrayAsync();

        Snapshot.Match(
            read,
            matchOptions => matchOptions
                .IgnoreField("**.CreatedDate")
                .IgnoreField("**.UpdatedDate")
                .IgnoreField("**.ETag"));
    }

    /// <summary>
    /// Represents the configuration properties for Cosmos command providers.
    /// </summary>
    private record CosmosConfiguration(
        string EndpointUri,
        string Database,
        string Container);
}
