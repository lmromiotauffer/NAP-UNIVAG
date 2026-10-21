using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Nap.Application.Comum;

public static class DependencyInjection
{
    // Convenção: um arquivo por caso de uso (Command/Query + Validator + Handler). Todo tipo concreto
    // cujo nome termina em "Handler" é registrado automaticamente (scoped) por varredura do assembly.
    public static IServiceCollection AdicionarApplication(this IServiceCollection servicos)
    {
        var assembly = typeof(DependencyInjection).Assembly;
        foreach (var tipo in assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Handler")))
            servicos.AddScoped(tipo);
        servicos.AddValidatorsFromAssembly(assembly);
        servicos.AddScoped<Features.Agenda.ServicoEscala>();
        servicos.AddScoped<Features.Producao.ServicoFechamento>();
        return servicos;
    }
}
