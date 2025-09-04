using System.Diagnostics.CodeAnalysis;

namespace Pigbank.Tests;

[SuppressMessage("ReSharper", "UnusedTypeParameter")]
public interface ICommand<TResponse> where TResponse : IResponse
{
}

public class Command : ICommand<Response>
{
}

public interface IHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
    where TResponse : IResponse
{
    Task<TResponse> Handle(TCommand command);
}

public class Handler : IHandler<Command, Response>
{
    public Task<Response> Handle(Command command)
    {
        throw new NotImplementedException();
    }
}

public interface IResponse
{
}

public class Response : IResponse
{
}

public class CommandTests
{
    [Fact]
    public void Test1()
    {
    }
}

public class HandlerTests
{
    private readonly Handler _sut;

    public HandlerTests()
    {
        _sut = new Handler();
    }

    [Fact]
    public async Task Test1()
    {
        var expected = new Response();
        var command = new Command();
        var response = await _sut.Handle(command);
        Assert.Equal(expected, response);
    }
}

public class ResponseTests
{
    [Fact]
    public void Test1()
    {
    }
}