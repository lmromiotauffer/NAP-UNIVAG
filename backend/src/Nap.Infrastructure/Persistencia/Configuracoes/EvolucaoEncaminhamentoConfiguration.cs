using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nap.Domain.Encaminhamentos;

namespace Nap.Infrastructure.Persistencia.Configuracoes;

public class EvolucaoEncaminhamentoConfiguration : IEntityTypeConfiguration<EvolucaoEncaminhamento>
{
    public void Configure(EntityTypeBuilder<EvolucaoEncaminhamento> builder)
    {
        builder.ToTable("EvolucoesEncaminhamento");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Texto).HasMaxLength(4000);
    }
}
