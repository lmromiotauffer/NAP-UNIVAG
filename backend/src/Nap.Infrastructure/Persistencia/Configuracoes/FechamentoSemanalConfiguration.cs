using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nap.Domain.Producao;
using Nap.Infrastructure.Identidade;

namespace Nap.Infrastructure.Persistencia.Configuracoes;

public class FechamentoSemanalConfiguration : IEntityTypeConfiguration<FechamentoSemanal>
{
    public void Configure(EntityTypeBuilder<FechamentoSemanal> builder)
    {
        builder.ToTable("FechamentosSemanais");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();
        builder.Property<byte[]>("Versao").IsRowVersion();

        builder.HasOne<UsuarioIdentidade>().WithMany().HasForeignKey(f => f.ProfissionalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(f => new { f.ProfissionalId, f.Semana }).IsUnique();

        builder.HasMany(f => f.Eventos).WithOne().HasForeignKey("FechamentoId").IsRequired().OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(f => f.Eventos).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class EventoFechamentoConfiguration : IEntityTypeConfiguration<EventoFechamento>
{
    public void Configure(EntityTypeBuilder<EventoFechamento> builder)
    {
        builder.ToTable("EventosFechamento");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Tipo).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Justificativa).HasMaxLength(500);
        builder.HasOne<UsuarioIdentidade>().WithMany().HasForeignKey(e => e.AutorId).OnDelete(DeleteBehavior.Restrict);
    }
}
