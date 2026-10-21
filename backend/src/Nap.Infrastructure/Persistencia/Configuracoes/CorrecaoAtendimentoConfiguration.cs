using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nap.Domain.Atendimentos;

namespace Nap.Infrastructure.Persistencia.Configuracoes;

public class CorrecaoAtendimentoConfiguration : IEntityTypeConfiguration<CorrecaoAtendimento>
{
    public void Configure(EntityTypeBuilder<CorrecaoAtendimento> builder)
    {
        builder.ToTable("CorrecoesAtendimento");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Justificativa).HasMaxLength(500);
        builder.ComplexProperty(c => c.DadosAnteriores, DadosClinicosMapeamento.Configurar("Anterior"));
    }
}
