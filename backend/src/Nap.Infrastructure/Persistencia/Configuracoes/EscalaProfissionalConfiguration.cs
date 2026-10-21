using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nap.Domain.Agenda;
using Nap.Infrastructure.Identidade;

namespace Nap.Infrastructure.Persistencia.Configuracoes;

public class EscalaProfissionalConfiguration : IEntityTypeConfiguration<EscalaProfissional>
{
    public void Configure(EntityTypeBuilder<EscalaProfissional> builder)
    {
        builder.ToTable("EscalasProfissional");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property<byte[]>("Versao").IsRowVersion();
        builder.HasOne<UsuarioIdentidade>().WithMany().HasForeignKey(e => e.ProfissionalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.ProfissionalId, e.VigenciaInicio });
        builder.OwnsMany(e => e.Faixas, f =>
        {
            f.ToTable("FaixasEscala");
            f.WithOwner().HasForeignKey("EscalaProfissionalId");
            f.Property<int>("Id");
            f.HasKey("Id");
            // Propriedades só com get: mapeadas à mão para o EF as ligar ao construtor.
            f.Property(x => x.Dia);
            f.Property(x => x.Inicio);
            f.Property(x => x.Fim);
            f.Property(x => x.SemAtendimento);
        });
        builder.Navigation(e => e.Faixas).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
