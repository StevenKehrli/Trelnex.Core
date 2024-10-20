using Snapshooter.NUnit;

namespace Trelnex.Core.Data.Tests.Commands;

public class DeleteCommandTests
{
    private readonly string _typeName = "item-test";

    [Test]
    public async Task DeleteCommand_SaveAsync()
    {
        var id = "d2c6b6ce-6467-435d-9779-8f72c67376f3";
        var partitionKey = "c3c0e7cc-98f9-4289-bca1-72f0b1e6e3ec";

        var startDateTime = DateTime.UtcNow;

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

        // save it
        await createCommand.SaveAsync(
            requestContext: requestContext,
            cancellationToken: default);

        var deleteCommand = await commandProvider.DeleteAsync(
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
                            fieldOption.Field<Guid>("Item.ETag"),
                            Is.Not.Default);
                    });
                }));
    }

    [Test]
    public async Task DeleteCommand_SaveAsync_IsReadOnly()
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

        // save it
        await createCommand.SaveAsync(
            requestContext: requestContext,
            cancellationToken: default);

        var deleteCommand = await commandProvider.DeleteAsync(
            id: id,
            partitionKey: partitionKey);

        Assert.That(deleteCommand, Is.Not.Null);
        Assert.That(deleteCommand!.Item, Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(
                () => deleteCommand.Item.PublicMessage = "Public #2",
                $"The '{typeof(ITestItem)}' is read-only");

            Assert.Throws<InvalidOperationException>(
                () => deleteCommand.Item.PrivateMessage = "Private #2",
                $"The '{typeof(ITestItem)}' is read-only");
        });
    }

    [Test]
    public void DeleteCommand_SaveAsync_NotSupported()
    {
        var id = Guid.NewGuid().ToString();
        var partitionKey = Guid.NewGuid().ToString();

        // create our command provider
        var commandProvider =
            InMemoryCommandProvider.Create<ITestItem, TestItem>(
                typeName: _typeName,
                commandOperations: CommandOperations.None);

        Assert.ThrowsAsync<NotSupportedException>(
            async () => await commandProvider.DeleteAsync(id: id, partitionKey: partitionKey),
            "The requested Delete operation is not supported.");
    }

    [Test]
    public async Task DeleteCommand_SaveAsync_ResultIsReadOnly()
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

        // save it
        await createCommand.SaveAsync(
            requestContext: requestContext,
            cancellationToken: default);

        var deleteCommand = await commandProvider.DeleteAsync(
            id: id,
            partitionKey: partitionKey);

        Assert.That(deleteCommand, Is.Not.Null);
        Assert.That(deleteCommand!.Item, Is.Not.Null);

        // save it and read it back
        var deleted = await deleteCommand.SaveAsync(
            requestContext: requestContext,
            cancellationToken: default);

        Assert.That(deleted, Is.Not.Null);
        Assert.That(deleted.Item, Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(
                () => deleted.Item.PublicMessage = "Public #3",
                $"The '{typeof(ITestItem)}' is read-only");

            Assert.Throws<InvalidOperationException>(
                () => deleted.Item.PrivateMessage = "Private #3",
                $"The '{typeof(ITestItem)}' is read-only");
        });
    }

    [Test]
    public async Task DeleteCommand_SaveAsync_WhenAlreadySaved()
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

        // save it
        await createCommand.SaveAsync(
            requestContext: requestContext,
            cancellationToken: default);

        var deleteCommand = await commandProvider.DeleteAsync(
            id: id,
            partitionKey: partitionKey);

        Assert.That(deleteCommand, Is.Not.Null);
        Assert.That(deleteCommand!.Item, Is.Not.Null);

        // save it
        await deleteCommand.SaveAsync(
            requestContext: requestContext,
            cancellationToken: default);

        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await deleteCommand.SaveAsync(
                requestContext: requestContext,
                cancellationToken: default),
            "The Command is no longer valid because its SaveAsync method has already been called.");
    }

}
