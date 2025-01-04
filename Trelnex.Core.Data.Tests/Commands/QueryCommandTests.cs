using Snapshooter.NUnit;

namespace Trelnex.Core.Data.Tests.Commands;

public class QueryCommandTests
{
    private readonly string _typeName = "test-item";

    [Test]
    public async Task QueryCommand_ToAsyncEnumerable()
    {
        var id1 = "400a3743-97fe-4e77-98da-0b232dbedf89";
        var partitionKey1 = "f5c150a6-0f95-47ee-83f7-68dd9b8d1ff1";

        var id2 = "46046bff-12ff-42bc-850a-50da59e264c3";
        var partitionKey2 = "0ab56811-b146-4465-948e-3c0816021954";

        var requestContext = TestRequestContext.Create();

        // create our command provider
        var commandProvider =
            InMemoryCommandProvider.Create<ITestItem, TestItem>(
                typeName: _typeName);

        var createCommand1 = commandProvider.Create(
            id: id1,
            partitionKey: partitionKey1);

        createCommand1.Item.PublicMessage = "Public #1";
        createCommand1.Item.PrivateMessage = "Private #1";

        // save it
        await createCommand1.SaveAsync(
            requestContext: requestContext,
            cancellationToken: default);

        var createCommand2 = commandProvider.Create(
            id: id2,
            partitionKey: partitionKey2);

        createCommand2.Item.PublicMessage = "Public #2";
        createCommand2.Item.PrivateMessage = "Private #2";

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

    [Test]
    public async Task QueryCommand_ToAsyncEnumerable_Cancel()
    {
        var id = Guid.NewGuid().ToString();
        var partitionKey = Guid.NewGuid().ToString();

        var requestContext = TestRequestContext.Create();

        // create our command provider
        var commandProvider =
            InMemoryCommandProvider.Create<ITestItem, TestItem>(
                typeName: _typeName);

        var createCommand = commandProvider.Create(
            id: id,
            partitionKey: partitionKey);

        createCommand.Item.PublicMessage = "Public #1";
        createCommand.Item.PrivateMessage = "Private #1";

        // save it
        await createCommand.SaveAsync(
            requestContext: requestContext,
            cancellationToken: default);

        // query
        var queryCommand = commandProvider.Query();
        queryCommand.Where(i => i.PublicMessage == "Public #1");

        var cts = new CancellationTokenSource();

        // create the async enumerable
        var enumerable = queryCommand.ToAsyncEnumerable().WithCancellation(cts.Token);

        // but cancel
        await cts.CancelAsync();

        async Task enumerate()
        {
            await foreach (var _ in enumerable) { }
        }

        Assert.ThrowsAsync<OperationCanceledException>(
            enumerate);
    }

    [Test]
    public async Task QueryCommand_ToAsyncEnumerable_ItemIsDeleted()
    {
        var id = Guid.NewGuid().ToString();
        var partitionKey = Guid.NewGuid().ToString();

        var requestContext = TestRequestContext.Create();

        // create our command provider
        var commandProvider =
            InMemoryCommandProvider.Create<ITestItem, TestItem>(
                typeName: _typeName,
                commandOperations: CommandOperations.Delete);

        var createCommand = commandProvider.Create(
            id: id,
            partitionKey: partitionKey);

        createCommand.Item.PublicMessage = "Public #1";
        createCommand.Item.PrivateMessage = "Private #1";

        // save it and read it back
        var created = (await createCommand.SaveAsync(
            requestContext: requestContext,
            cancellationToken: default))!;

        var deleteCommand = await commandProvider.DeleteAsync(
            id: id,
            partitionKey: partitionKey);

        // save it
        await deleteCommand!.SaveAsync(
            requestContext: requestContext,
            cancellationToken: default);

        // query
        var queryCommand = commandProvider.Query();

        // should return no items
        var read = await queryCommand.ToAsyncEnumerable().ToArrayAsync();

        Snapshot.Match(read);
    }

    [Test]
    public async Task QueryCommand_ToAsyncEnumerable_OrderBy()
    {
        var id1 = Guid.NewGuid().ToString();
        var partitionKey1 = Guid.NewGuid().ToString();

        var id2 = Guid.NewGuid().ToString();
        var partitionKey2 = Guid.NewGuid().ToString();

        var requestContext = TestRequestContext.Create();

        // create our command provider
        var commandProvider =
            InMemoryCommandProvider.Create<ITestItem, TestItem>(
                typeName: _typeName);

        var createCommand1 = commandProvider.Create(
            id: id1,
            partitionKey: partitionKey1);

        createCommand1.Item.PublicMessage = "Public #1";
        createCommand1.Item.PrivateMessage = "Private #1";

        // save it
        await createCommand1.SaveAsync(
            requestContext: requestContext,
            cancellationToken: default);

        var createCommand2 = commandProvider.Create(
            id: id2,
            partitionKey: partitionKey2);

        createCommand2.Item.PublicMessage = "Public #2";
        createCommand2.Item.PrivateMessage = "Private #2";

        // save it
        await createCommand2.SaveAsync(
            requestContext: requestContext,
            cancellationToken: default);

        // query
        var queryCommand = commandProvider.Query();
        queryCommand.OrderBy(i => i.PublicMessage);

        // should return first item first
        var read = await queryCommand.ToAsyncEnumerable().ToArrayAsync();

        Snapshot.Match(
            read,
            matchOptions => matchOptions
                .IgnoreField("**.Id")
                .IgnoreField("**.PartitionKey")
                .IgnoreField("**.CreatedDate")
                .IgnoreField("**.UpdatedDate")
                .IgnoreField("**.ETag"));
    }

    [Test]
    public async Task QueryCommand_ToAsyncEnumerable_OrderByDescending()
    {
        var id1 = Guid.NewGuid().ToString();
        var partitionKey1 = Guid.NewGuid().ToString();

        var id2 = Guid.NewGuid().ToString();
        var partitionKey2 = Guid.NewGuid().ToString();

        var requestContext = TestRequestContext.Create();

        // create our command provider
        var commandProvider =
            InMemoryCommandProvider.Create<ITestItem, TestItem>(
                typeName: _typeName);

        var createCommand1 = commandProvider.Create(
            id: id1,
            partitionKey: partitionKey1);

        createCommand1.Item.PublicMessage = "Public #1";
        createCommand1.Item.PrivateMessage = "Private #1";

        // save it
        await createCommand1.SaveAsync(
            requestContext: requestContext,
            cancellationToken: default);

        var createCommand2 = commandProvider.Create(
            id: id2,
            partitionKey: partitionKey2);

        createCommand2.Item.PublicMessage = "Public #2";
        createCommand2.Item.PrivateMessage = "Private #2";

        // save it
        await createCommand2.SaveAsync(
            requestContext: requestContext,
            cancellationToken: default);

        // query
        var queryCommand = commandProvider.Query();
        queryCommand.OrderByDescending(i => i.PublicMessage);

        // should return second item first
        var read = await queryCommand.ToAsyncEnumerable().ToArrayAsync();

        Snapshot.Match(
            read,
            matchOptions => matchOptions
                .IgnoreField("**.Id")
                .IgnoreField("**.PartitionKey")
                .IgnoreField("**.CreatedDate")
                .IgnoreField("**.UpdatedDate")
                .IgnoreField("**.ETag"));
    }

    [Test]
    public async Task QueryCommand_ToAsyncEnumerable_ResultIsReadOnly()
    {
        var id = Guid.NewGuid().ToString();
        var partitionKey = Guid.NewGuid().ToString();

        var requestContext = TestRequestContext.Create();

        // create our command provider
        var commandProvider =
            InMemoryCommandProvider.Create<ITestItem, TestItem>(
                typeName: _typeName);

        var createCommand1 = commandProvider.Create(
            id: id,
            partitionKey: partitionKey);

        createCommand1.Item.PublicMessage = "Public #1";
        createCommand1.Item.PrivateMessage = "Private #1";

        // save it
        await createCommand1.SaveAsync(
            requestContext: requestContext,
            cancellationToken: default);

        // query
        var queryCommand = commandProvider.Query();

        // should return item
        var reads = await queryCommand.ToAsyncEnumerable().ToArrayAsync();

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(
                () => reads[0].Item.PublicMessage = "Public #2",
                $"The '{typeof(ITestItem)}' is read-only");

            Assert.Throws<InvalidOperationException>(
                () => reads[0].Item.PrivateMessage = "Private #2",
                $"The '{typeof(ITestItem)}' is read-only");
        });
    }

    [Test]
    public async Task QueryCommand_ToAsyncEnumerable_Skip()
    {
        var id1 = Guid.NewGuid().ToString();
        var partitionKey1 = Guid.NewGuid().ToString();

        var id2 = Guid.NewGuid().ToString();
        var partitionKey2 = Guid.NewGuid().ToString();

        var requestContext = TestRequestContext.Create();

        // create our command provider
        var commandProvider =
            InMemoryCommandProvider.Create<ITestItem, TestItem>(
                typeName: _typeName);

        var createCommand1 = commandProvider.Create(
            id: id1,
            partitionKey: partitionKey1);

        createCommand1.Item.PublicMessage = "Public #1";
        createCommand1.Item.PrivateMessage = "Private #1";

        // save it
        await createCommand1.SaveAsync(
            requestContext: requestContext,
            cancellationToken: default);

        var createCommand2 = commandProvider.Create(
            id: id2,
            partitionKey: partitionKey2);

        createCommand2.Item.PublicMessage = "Public #2";
        createCommand2.Item.PrivateMessage = "Private #2";

        // save it
        await createCommand2.SaveAsync(
            requestContext: requestContext,
            cancellationToken: default);

        // query
        var queryCommand = commandProvider.Query();
        queryCommand.OrderBy(i => i.PublicMessage).Skip(1);

        // should return second item
        var read = await queryCommand.ToAsyncEnumerable().ToArrayAsync();

        Snapshot.Match(
            read,
            matchOptions => matchOptions
                .IgnoreField("**.Id")
                .IgnoreField("**.PartitionKey")
                .IgnoreField("**.CreatedDate")
                .IgnoreField("**.UpdatedDate")
                .IgnoreField("**.ETag"));
    }


    [Test]
    public async Task QueryCommand_ToAsyncEnumerable_Take()
    {
        var id1 = Guid.NewGuid().ToString();
        var partitionKey1 = Guid.NewGuid().ToString();

        var id2 = Guid.NewGuid().ToString();
        var partitionKey2 = Guid.NewGuid().ToString();

        var requestContext = TestRequestContext.Create();

        // create our command provider
        var commandProvider =
            InMemoryCommandProvider.Create<ITestItem, TestItem>(
                typeName: _typeName);

        var createCommand1 = commandProvider.Create(
            id: id1,
            partitionKey: partitionKey1);

        createCommand1.Item.PublicMessage = "Public #1";
        createCommand1.Item.PrivateMessage = "Private #1";

        // save it
        await createCommand1.SaveAsync(
            requestContext: requestContext,
            cancellationToken: default);

        var createCommand2 = commandProvider.Create(
            id: id2,
            partitionKey: partitionKey2);

        createCommand2.Item.PublicMessage = "Public #2";
        createCommand2.Item.PrivateMessage = "Private #2";

        // save it
        await createCommand2.SaveAsync(
            requestContext: requestContext,
            cancellationToken: default);

        // query
        var queryCommand = commandProvider.Query();
        queryCommand.OrderBy(i => i.PublicMessage).Take(1);

        // should return first item
        var read = await queryCommand.ToAsyncEnumerable().ToArrayAsync();

        Snapshot.Match(
            read,
            matchOptions => matchOptions
                .IgnoreField("**.Id")
                .IgnoreField("**.PartitionKey")
                .IgnoreField("**.CreatedDate")
                .IgnoreField("**.UpdatedDate")
                .IgnoreField("**.ETag"));
    }

    [Test]
    public async Task QueryCommand_ToAsyncEnumerable_Where()
    {
        var id1 = Guid.NewGuid().ToString();
        var partitionKey1 = Guid.NewGuid().ToString();

        var id2 = Guid.NewGuid().ToString();
        var partitionKey2 = Guid.NewGuid().ToString();

        var requestContext = TestRequestContext.Create();

        // create our command provider
        var commandProvider =
            InMemoryCommandProvider.Create<ITestItem, TestItem>(
                typeName: _typeName);

        var createCommand1 = commandProvider.Create(
            id: id1,
            partitionKey: partitionKey1);

        createCommand1.Item.PublicMessage = "Public #1";
        createCommand1.Item.PrivateMessage = "Private #1";

        // save it
        await createCommand1.SaveAsync(
            requestContext: requestContext,
            cancellationToken: default);

        var createCommand2 = commandProvider.Create(
            id: id2,
            partitionKey: partitionKey2);

        createCommand2.Item.PublicMessage = "Public #2";
        createCommand2.Item.PrivateMessage = "Private #2";

        // save it
        await createCommand2.SaveAsync(
            requestContext: requestContext,
            cancellationToken: default);

        // query
        var queryCommand = commandProvider.Query();
        queryCommand.Where(i => i.PublicMessage == "Public #1");

        // should return first item
        var read = await queryCommand.ToAsyncEnumerable().ToArrayAsync();

        Snapshot.Match(
            read,
            matchOptions => matchOptions
                .IgnoreField("**.Id")
                .IgnoreField("**.PartitionKey")
                .IgnoreField("**.CreatedDate")
                .IgnoreField("**.UpdatedDate")
                .IgnoreField("**.ETag"));
    }
}
