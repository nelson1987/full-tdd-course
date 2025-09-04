namespace Pigbank.Tests;

public class SuccessCaseTests
{
    [Fact]
    public void Sucesso()
    {
        var soma = 1 + 1;
        Assert.Equal(2, soma);
    }
}