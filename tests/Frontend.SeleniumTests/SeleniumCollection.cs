namespace Frontend.SeleniumTests;

public class SeededDriverFixture : IAsyncLifetime
{
    public string Username { get; private set; } = string.Empty;
    public string Password { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        (Username, Password) = await TestDataSeeder.EnsureTestDriverExistsAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

[CollectionDefinition("Selenium collection")]
public class SeleniumCollection : ICollectionFixture<SeededDriverFixture>
{
    // This class has no code; it just ties SeededDriverFixture to the
    // "Selenium collection" name so every test class below shares one
    // seeded account instead of re-creating it per class.
}
