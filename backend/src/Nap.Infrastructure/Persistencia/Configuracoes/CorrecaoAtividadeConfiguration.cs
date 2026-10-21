using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nap.Domain.Atividades;

namespace Nap.Infrastructure.Persistencia.Configuracoes;

public class CorrecaoAtividadeConfiguration : IEntityTypeConfiguration<CorrecaoAtividade>
{
    public void Configure(EntityTypeBuilder<CorrecaoAtividade> builder)
    {
        builder.ToTable("CorrecoesAtividade");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Justificativa).HasMaxLength(500);
        builder.Property(c => c.TipoAnterior).HasConversion<string>().HasMaxLength(30);
        builder.Property(c => c.DescricaoAnterior).HasMaxLength(2000);
    }
}
