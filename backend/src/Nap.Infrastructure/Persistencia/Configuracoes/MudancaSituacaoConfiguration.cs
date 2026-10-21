using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nap.Domain.Alunos;

namespace Nap.Infrastructure.Persistencia.Configuracoes;

public class MudancaSituacaoConfiguration : IEntityTypeConfiguration<MudancaSituacao>
{
    public void Configure(EntityTypeBuilder<MudancaSituacao> builder)
    {
        builder.ToTable("MudancasSituacao");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Motivo).HasMaxLength(500).IsRequired();
    }
}
