namespace Trelnex.Core.Data.Tests.CommandProviders;

public class InMemoryCommandProviderTests : CommandProviderTests
{
    [SetUp]
    public async Task TestFixtureSetup()
    {
        // create our command provider
        var factory = await InMemoryCommandProviderFactory.Create();

        _commandProvider =
            factory.Create<ITestItem, TestItem>(
                typeName: "test-item",
                TestItem.Validator,
                CommandOperations.All);
    }
}
