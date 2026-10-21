using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nap.Domain.Agenda;
using Nap.Infrastructure.Identidade;

namespace Nap.Infrastructure.Persistencia.Configuracoes;

public class AutorizacaoForaEscalaConfiguration : IEntityTypeConfiguration<AutorizacaoForaEscala>
{
    public void Configure(EntityTypeBuilder<AutorizacaoForaEscala> builder)
    {
        builder.ToTable("AutorizacoesForaEscala");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Justificativa).HasMaxLength(500).IsRequired();
        builder.ComplexProperty(a => a.Intervalo, i =>
        {
            i.Property(x => x.Inicio).HasColumnName("Inicio");
            i.Property(x => x.Fim).HasColumnName("Fim");
        });
        builder.HasOne<UsuarioIdentidade>().WithMany().HasForeignKey(a => a.ProfissionalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UsuarioIdentidade>().WithMany().HasForeignKey(a => a.AutorizadoPorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => new { a.ProfissionalId, a.RevogadaEm });
    }
}
