using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Snapshooter.NUnit;
using Trelnex.Core.Api.CommandProviders;
using Trelnex.Core.Api.Serilog;
using Trelnex.Core.Data;

namespace Trelnex.Core.Api.Tests.CommandProviders;

[Ignore("Requires a CosmosDB instance.")]
public class CosmosCommandProviderTests
{
    private ICommandProvider<ITestItem> _commandProvider = null!;

    [OneTimeSetUp]
    public void TestFixtureSetup()
    {
        // This method is called once prior to executing any of the tests in the fixture.

        // create the service collection
        var services = new ServiceCollection();

        // create the test configuration
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.User.json", optional: true, reloadOnChange: true)
            .Build();

        var bootstrapLogger = services.AddSerilog(
            configuration,
            "Trelnex.Integration.Tests");

        services.AddCosmosCommandProviders(
            configuration,
            bootstrapLogger,
            options => options.Add<ITestItem, TestItem>(
                typeName: "test-item",
                validator: TestItem.Validator,
                commandOperations: CommandOperations.All));

        var serviceProvider = services.BuildServiceProvider();

        // get the command provider
        _commandProvider = serviceProvider.GetRequiredService<ICommandProvider<ITestItem>>();
    }

    [Test]
    public async Task CreateCommand_SaveAsync()
    {
        var id = "6adaafd5-0e1a-4545-8255-2a8486151af5";
        var partitionKey = "c785e6d4-2868-417d-aeca-589f7c4ca07f";

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
        var id = "99456d7d-7587-4245-b35d-9b64cf35e899";
        var partitionKey = "7d64c23f-e97b-4144-81fe-c670c7da7a6f";

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
        var id = "dd678bb5-2484-4c92-a233-aea9606bcb14";
        var partitionKey = "3d3e01d3-46bf-4a58-972c-8ff7bfe1bc99";

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
        var id = "035dab54-75a7-4afd-98d9-c2eaf3116cbd";
        var partitionKey = "e15335d2-1091-404c-8492-92f3ca338e20";

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
        var id1 = "4ed21fda-4fae-4709-90ca-5c96d85dac78";
        var partitionKey1 = "a9b616d7-05e8-4b89-a206-dabf4f19d46a";

        var id2 = "2467d367-1a5d-4ac0-b760-01850b9ff4f4";
        var partitionKey2 = "13e5b99b-893c-4202-a27e-b4aef69f6174";

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
}
