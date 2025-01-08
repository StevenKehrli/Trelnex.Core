using FluentValidation;
using Snapshooter.NUnit;

namespace Trelnex.Core.Data.Tests.Commands;

public class BatchCommandValidateTests
{
    private readonly string _typeName = "test-item";

    [Test]
    public async Task BatchCommandValidate_ValidateAsync()
    {
        var validator = new InlineValidator<TestItem>();
        validator.RuleFor(k => k.PublicMessage).NotEmpty();
        validator.RuleFor(k => k.PrivateMessage).NotEmpty();

        var id = Guid.NewGuid().ToString();
        var partitionKey = Guid.NewGuid().ToString();

        var requestContext = TestRequestContext.Create();

        // create our command provider
        var factory = await InMemoryCommandProviderFactory.Create();

        var commandProvider = factory.Create<ITestItem, TestItem>(
                typeName: _typeName,
                validator: validator);

        var batchCommand = commandProvider.Batch();

        // batch a create command
        var createCommand1 = commandProvider.Create(
            id: id,
            partitionKey: partitionKey);

        createCommand1.Item.PrivateMessage = "Private #1";

        batchCommand.Add(createCommand1);

        // batch a create command
        var createCommand2 = commandProvider.Create(
            id: id,
            partitionKey: partitionKey);

        createCommand2.Item.PublicMessage = "Public #2";

        batchCommand.Add(createCommand2);

        // batch a create command
        var createCommand3 = commandProvider.Create(
            id: id,
            partitionKey: partitionKey);

        createCommand3.Item.PrivateMessage = "Private #3";
        createCommand3.Item.PublicMessage = "Public #3";

        batchCommand.Add(createCommand3);

        // validate
        var validationResults = await batchCommand.ValidateAsync(default);

        Snapshot.Match(validationResults);
    }
}
