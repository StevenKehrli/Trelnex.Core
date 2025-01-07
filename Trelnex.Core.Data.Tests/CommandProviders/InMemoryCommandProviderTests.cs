namespace Trelnex.Core.Data.Tests.CommandProviders;

public class InMemoryCommandProviderTests : CommandProviderTests
{
    [OneTimeSetUp]
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

    [TearDown]
    public void TestCleanup()
    {
        // This method is called after each test case is run.

        (_commandProvider as InMemoryCommandProvider<ITestItem, TestItem>)!.Clear();
    }
}
