using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nap.Api.Controllers;

namespace Nap.Api.Tests;

/// <summary>Regras de §16.1 e §16.7 conferidas por reflexão sobre os controllers da Api.</summary>
public class ArquiteturaTests
{
    private static readonly Assembly Infrastructure = typeof(Nap.Infrastructure.DependencyInjection).Assembly;

    /// <summary>Públicos por spec (§16.7: "verificação restrita de assinatura"); o login fica com <c>[AllowAnonymous]</c> na ação.</summary>
    private static readonly Type[] PublicosDocumentados = [typeof(AssinaturaPeiController)];

    private static IEnumerable<Type> Controllers() =>
        typeof(AuthController).Assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false } && t.IsSubclassOf(typeof(ControllerBase)));

    private static IEnumerable<Type> Desdobrar(Type tipo)
    {
        var elemento = tipo.HasElementType ? tipo.GetElementType()! : tipo;
        yield return elemento;
        if (elemento.IsGenericType)
            foreach (var argumento in elemento.GetGenericArguments().SelectMany(Desdobrar))
                yield return argumento;
    }

    [Fact]
    public void Controllers_nao_dependem_de_tipos_da_infrastructure()
    {
        var violacoes = (
            from controller in Controllers()
            let parametros = controller.GetConstructors().SelectMany(c => c.GetParameters())
                .Concat(controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .SelectMany(m => m.GetParameters()))
            from parametro in parametros
            from tipo in Desdobrar(parametro.ParameterType)
            where tipo.Assembly == Infrastructure
            select $"{controller.Name}.{parametro.Member.Name}({parametro.Name}: {tipo.Name})").ToList();

        Assert.Empty(violacoes);
    }

    [Fact]
    public void Todos_os_controllers_tem_authorize_ou_allowanonymous_documentado()
    {
        var semAuthorize = Controllers()
            .Where(c => !PublicosDocumentados.Contains(c) && c.GetCustomAttribute<AuthorizeAttribute>() is null)
            .Select(c => c.Name).ToList();
        var anonimosNaoDocumentados = Controllers()
            .Where(c => !PublicosDocumentados.Contains(c) && c.GetCustomAttribute<AllowAnonymousAttribute>() is not null)
            .Select(c => c.Name).ToList();

        Assert.Empty(semAuthorize);
        Assert.Empty(anonimosNaoDocumentados);
    }
}
