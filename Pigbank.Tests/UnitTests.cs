// using System.Diagnostics.CodeAnalysis;
// using Microsoft.Extensions.DependencyInjection;
//
// namespace Pigbank.Tests;
//
// [SuppressMessage("ReSharper", "UnusedTypeParameter")]
// public interface ICommand<TResponse> where TResponse : IResponse
// {
// }
//
// public class Command : ICommand<Response>
// {
// }
//
// public interface IHandler<in TCommand, TResponse>
//     where TCommand : ICommand<TResponse>
//     where TResponse : IResponse
// {
//     Task<TResponse> Handle(TCommand command);
// }
//
// public class Handler(
//     ICacheService cacheService,
//     IRepository<Entity> repository,
//     IPublisher publisher) : IHandler<Command, Response>
// {
// /*Criação de conta
//  O usuário enviará um formulário com os dados básicos do cliente
//  Verificar se o processo de criação de conta já foi iniciado em outro momento ( Idempotencia com Redis)
// Se tiver sido criado(Com resposta da Receita Federal)
//     Cria e envia o evento ReOpenedAccount(send Sqs)
// Se não tiver sido criado
//     Buscar na Receita Federal
//     busca no NoSql, todas as informações do cliente(get DynamoDb)
//     Cria e envia o evento OpenedAccount(Send Sqs)
// Consumer ReOpenedAccountConsumer
//     Persiste no Postgres
//     Set no DynamoDb
//     Cria e envia o evento CreatedAccount(Send Sqs)
// Consumer OpenedAccountConsumer
//     Busca resultado da Receita Federal
//     Persiste no Postgres
//     Set no DynamoDb
//     Cria no Redis
//     Cria e envia o evento CreatedAccount(Send Sqs)
//  */
//     public async Task<Response> Handle(Command command)
//     {
//         try
//         {
//             Guid idempotencyKey = Guid.NewGuid();
//             var resultadoArmazenado = await cacheService.Get<Response>(idempotencyKey);
//
//             if (resultadoArmazenado != null)
//             {
//                 return resultadoArmazenado;
//             }
//
//             await repository.Add();
//             await publisher.Send();
//             await cacheService.Set(); // Exemplo de expiração
//
//             // var getExternalResponse = await externalService.GetExternalService(Guid.NewGuid());
//             // var getEntity = await repository.Get(Guid.NewGuid());
//             //cacheService.Set(idempotencyKey, resultadoDaOperacao, TimeSpan.FromMinutes(30)); // Exemplo de expiração
//
//             //return resultadoDaOperacao;
//             // return new Response();
//         }
//         catch (Exception e)
//         {
//             //return Task.FromException<Response>(e);
//             Console.WriteLine(e);
//             throw;
//         }
//     }
// }
//
// public interface IResponse
// {
// }
//
// public class Response : IResponse
// {
// }
//
// public interface IRepository<TEntity> where TEntity : IEntity
// {
//     Task<TEntity> Get(Guid id);
//     Task Add();
// }
//
// public class Repository : IRepository<Entity>
// {
//     public Task<Entity> Get(Guid id)
//     {
//         throw new NotImplementedException();
//     }
//
//     public Task Add()
//     {
//         throw new NotImplementedException();
//     }
// }
//
// public interface IEntity
// {
// }
//
// public class Entity : IEntity
// {
// }
//
// public interface ICacheService
// {
//     Task<TResponse?> Get<TResponse>(Guid id) where TResponse : IResponse;
//     Task Set<TResponse>(Guid id, TResponse response) where TResponse : IResponse;
// }
//
// public interface IPublisher
// {
//     Task Send(string queueName);
// }
//
// public class ExternalServiceGetContractResponse
// {
// }
//
// public interface IExternalService
// {
//     Task<ExternalServiceGetContractResponse> GetExternalService(Guid externalServiceId);
// }
//
// public static class DepencencyInjection
// {
//     public static void AddDependencies(IServiceCollection services)
//     {
//         services.AddTransient<ICommand<Response>, Command>()
//             .AddTransient<IHandler<Command, Response>, Handler>()
//             .AddTransient<IRepository<Entity>, Repository>();
//     }
// }
//
// public class CommandTests
// {
//     [Fact]
//     public void Test1()
//     {
//     }
// }
//
// public class HandlerTests
// {
//     private readonly Handler _sut;
//
//     public HandlerTests()
//     {
//         _sut = new Handler();
//     }
//
//     /* Dado_DadosCliente_Quando_ContaIniciada_Entao_BuscarNoDynamoDb
//  Dado_DadosCliente_Quando_ContaIniciada_Entao_CriarEventoReOpenedAccount
//  Dado_DadosCliente_Quando_ContaIniciada_Entao_EnviarEventoReOpenedAccount
//  Dado_DadosCliente_Quando_ContaNaoIniciada_Entao_CriarEventoOpenedAccount
//  Dado_DadosCliente_Quando_ContaNaoIniciada_Entao_EnviarEventoOpenedAccount*/
//     [Fact]
//     public async Task Test1()
//     {
//         var expected = new Response();
//         var command = new Command();
//         var response = await _sut.Handle(command);
//         Assert.Equal(expected, response);
//     }
// }
//
// public class ResponseTests
// {
//     [Fact]
//     public void Test1()
//     {
//     }
// }
//
// public class RepositoryTests
// {
//     [Fact]
//     public void Test1()
//     {
//     }
// }
//
// public class EntityTests
// {
//     [Fact]
//     public void Test1()
//     {
//     }
// }