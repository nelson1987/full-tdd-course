// using NSubstitute;
// using NSubstitute.ReturnsExtensions;
//
// namespace Pigbank.Tests;
//
// public class HandlerUnitTests
// {
//     private readonly HandlerUnit _sut;
//     private readonly ICacheServiceUnit cacheService;
//
//     public HandlerUnitTests()
//     {
//         cacheService = Substitute.For<ICacheServiceUnit>();
//         cacheService.Get(Arg.Any<Guid>())
//             .ReturnsNull();
//         _sut = new HandlerUnit(cacheService);
//     }
//
//     [Fact]
//     public void Fluxo_Sucesso()
//     {
//         cacheService.Get(Arg.Any<Guid>())
//             .ReturnsNull();
//
//         var arrange = (Guid.NewGuid(), "Pigbank");
//         string act = _sut.Handle(arrange);
//         Assert.Equal(arrange.Item2, act);
//         cacheService
//             .Received(1)
//             .Set(arrange.Item2, arrange.Item1);
//     }
//
//     [Fact]
//     public void Dado_Handle_Quando_CacheServiceReturnsEntity_Entao_Return_CachedEntity()
//     {
//         cacheService.Get(Arg.Any<Guid>())
//             .Returns(new CacheServiceResponse("PigBank_v2"));
//
//         var arrange = (Guid.NewGuid(), "Pigbank");
//         string act = _sut.Handle(arrange);
//         Assert.Equal("PigBank_v2", act);
//         cacheService
//             .DidNotReceive()
//             .Set(Arg.Any<string>(), Arg.Any<Guid>());
//     }
// }
//
// public class CacheServiceUnitTests
// {
//     [Fact]
//     public void Get_Test()
//     {
//         CacheServiceUnit _sut = new CacheServiceUnit();
//         var arrange = Guid.NewGuid();
//         CacheServiceResponse act = _sut.Get(arrange);
//         Assert.Equal("Pigbank", act.Message);
//     }
// }
//
// public record CacheServiceResponse(string Message);
//
// public interface ICacheServiceUnit
// {
//     CacheServiceResponse? Get(Guid key);
//     void Set(string inputName, Guid key);
// }
//
// public interface IPublisher
// {
//     void Send(string input);
// }
//
// public interface ILoggerService
// {
//     void LogStart(string message);
//     void LogEnd(string message);
//     void LogError(string message);
// }
//
// public class CacheServiceUnit : ICacheServiceUnit
// {
//     public CacheServiceResponse? Get(Guid key)
//     {
//         throw new NotImplementedException();
//     }
//
//     public void Set(string inputName, Guid key)
//     {
//         throw new NotImplementedException();
//     }
// }
//
// /*
//  * Dado_Quando_Entao
//  * Dado_Handle_Quando_CacheServiceReturnsEntity_Entao_Return_CachedEntity
//  * Dado_Handle_Quando_CacheServiceNotReturnsEntity_Entao_SegueFluxo
//  * Dado_Handle_Quando_CacheServiceRaisedException_Entao_SegueFluxo
//  */
// public interface IHandlerUnit
// {
//     string Handle((Guid idempotencyId, string name) input);
// }
//
// public class HandlerUnit(
//     ICacheServiceUnit cacheService,
//     IPublisher publisher
// ) : IHandlerUnit
// {
//     public string Handle((Guid idempotencyId, string name) input)
//     {
//         var cachedEntity = cacheService.Get(input.idempotencyId);
//         if (cachedEntity != null)
//         {
//             return cachedEntity.Message;
//         }
//
//         publisher.Send(input.name);
//         cacheService.Set(input.name, input.idempotencyId);
//         return input.name;
//     }
// }
//
// public class HandlerUnitLogs(
//     IHandlerUnit handler,
//     ILoggerService loggerService
// ) : IHandlerUnit
// {
//     public string Handle((Guid idempotencyId, string name) input)
//     {
//         try
//         {
//             loggerService.LogStart(input.name);
//             var response = handler.Handle(input);
//             loggerService.LogEnd(input.name);
//             return response;
//         }
//         catch (Exception ex)
//         {
//             loggerService.LogError(input.name);
//             throw ex;
//         }
//     }
// }