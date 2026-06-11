namespace Mabuntle.IntegrationTests;

public class FoundationTests
{
    [Fact]
    public void ApiAssembly_IsReachableFromIntegrationTests()
    {
        Assert.Equal("Mabuntle.Api", typeof(Program).Assembly.GetName().Name);
    }
}
