using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nap.Domain.Configuracoes;

namespace Nap.Infrastructure.Persistencia.Configuracoes;

public class ConfiguracaoAlertasConfiguration : IEntityTypeConfiguration<ConfiguracaoAlertas>
{
    // Linha única, criada pela migração com os padrões.
    public static readonly Guid IdUnico = new("0198a000-0000-7000-8000-000000000001");

    public void Configure(EntityTypeBuilder<ConfiguracaoAlertas> builder)
    {
        builder.ToTable("ConfiguracaoAlertas");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property<byte[]>("Versao").IsRowVersion();
        var padrao = ConfiguracaoAlertas.Padrao();
        builder.HasData(new
        {
            Id = IdUnico,
            padrao.FaltasConsecutivas, padrao.FaltasJanela, padrao.JanelaDias,
            padrao.DiasSemAcolhimento, padrao.DiasSemAtualizacao, padrao.DiasPeiPendente,
        });
    }
}
