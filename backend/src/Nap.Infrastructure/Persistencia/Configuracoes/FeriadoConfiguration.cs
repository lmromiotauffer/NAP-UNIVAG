using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nap.Domain.Agenda;

namespace Nap.Infrastructure.Persistencia.Configuracoes;

public class FeriadoConfiguration : IEntityTypeConfiguration<Feriado>
{
    public void Configure(EntityTypeBuilder<Feriado> builder)
    {
        builder.ToTable("Feriados");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();
        builder.Property(f => f.Descricao).HasMaxLength(120).IsRequired();
        // Feriado inativado não ocupa a data: dá para cadastrá-la de novo.
        builder.HasIndex(f => f.Data).IsUnique().HasFilter("[Ativo] = 1");
    }
}
