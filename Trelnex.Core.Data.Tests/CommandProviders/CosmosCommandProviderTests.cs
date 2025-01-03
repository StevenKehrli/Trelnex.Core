using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Snapshooter.NUnit;

namespace Trelnex.Core.Data.Tests.CommandProviders;

[Ignore("Requires a CosmosDB instance.")]
public class CosmosCommandProviderTests
{
    private readonly string _typeName = "test-item";

    private ICommandProvider<ITestItem> _commandProvider = null!;

    [OneTimeSetUp]
    public async Task TestFixtureSetup()
    {
        // This method is called once prior to executing any of the tests in the fixture.

        // create the test configuration
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.User.json", optional: true, reloadOnChange: true)
            .Build();

        var cosmosConfiguration = configuration.GetSection("CosmosDB").Get<CosmosConfiguration>()!;

        // create the command provider
        var tokenCredential = new DefaultAzureCredential();

        var cosmosClientOptions = new CosmosClientOptions(
            TokenCredential: tokenCredential,
            AccountEndpoint: cosmosConfiguration.EndpointUri,
            DatabaseId: cosmosConfiguration.Database,
            ContainerIds: [ cosmosConfiguration.Container ]
        );

        var keyResolverOptions = new KeyResolverOptions(
            TokenCredential: tokenCredential);

        var factory = await CosmosCommandProviderFactory.Create(
            cosmosClientOptions,
            keyResolverOptions);

        _commandProvider = factory.Create<ITestItem, TestItem>(
            cosmosConfiguration.Container,
            _typeName,
            TestItem.Validator,
            CommandOperations.All);
    }

    [Test]
    public async Task CreateCommand_SaveAsync()
    {
        var id = "2a4cb3ec-6624-4fc6-abc4-6a5db019f8f9";
        var partitionKey = "b297ff5b-2ab5-4b8d-9dfd-57d2e1d8c3d2";

        var startDateTime = DateTime.UtcNow;

        var requestContext = TestRequestContext.Create();

        var createCommand = _commandProvider.Create(
            id: id,
            partitionKey: partitionKey);

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
        var partitionKey = "fbc8502a-38ee-4edb-8a2d-485888af5bd3";

        var startDateTime = DateTime.UtcNow;

        var requestContext = TestRequestContext.Create();

        var createCommand = _commandProvider.Create(
            id: id,
            partitionKey: partitionKey);

        createCommand.Item.Message = "Message #1";

        // save it
        await createCommand.SaveAsync(
            requestContext: requestContext,
            cancellationToken: default);

        var deleteCommand = await _commandProvider.DeleteAsync(
            id: id,
            partitionKey: partitionKey);

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
        var partitionKey = "2b541bfd-4605-48f9-b1bc-5aba5f64cd24";

        var startDateTime = DateTime.UtcNow;

        var requestContext = TestRequestContext.Create();

        var createCommand = _commandProvider.Create(
            id: id,
            partitionKey: partitionKey);

        createCommand.Item.Message = "Message #1";

        // save it
        await createCommand.SaveAsync(
            requestContext: requestContext,
            cancellationToken: default);

        var read = await _commandProvider.ReadAsync(
            id: id,
            partitionKey: partitionKey);

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
        var partitionKey = "48953713-d269-42c1-b803-593f8c027aef";

        var startDateTime = DateTime.UtcNow;

        var requestContext = TestRequestContext.Create();

        var createCommand = _commandProvider.Create(
            id: id,
            partitionKey: partitionKey);

        createCommand.Item.Message = "Message #1";

        // save it
        await createCommand.SaveAsync(
            requestContext: requestContext,
            cancellationToken: default);

        var updateCommand = await _commandProvider.UpdateAsync(
            id: id,
            partitionKey: partitionKey);

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
        var partitionKey1 = "81dc4acd-dcbe-4d5f-a36f-21a35f158b2c";

        var id2 = "648de92a-b7e8-41c5-a5d2-bdf0cc65d67c";
        var partitionKey2 = "e36f287e-188d-4a74-9db7-dab74282b5dd";

        var requestContext = TestRequestContext.Create();

        var createCommand1 = _commandProvider.Create(
            id: id1,
            partitionKey: partitionKey1);

        createCommand1.Item.Message = "Message #1";

        // save it
        await createCommand1.SaveAsync(
            requestContext: requestContext,
            cancellationToken: default);

        var createCommand2 = _commandProvider.Create(
            id: id2,
            partitionKey: partitionKey2);

        createCommand2.Item.Message = "Message #2";

        // save it
        await createCommand2.SaveAsync(
            requestContext: requestContext,
            cancellationToken: default);

        // query
        var queryCommand = _commandProvider.Query()
            .Where(item => item.PartitionKey == partitionKey1 || item.PartitionKey == partitionKey2);

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
