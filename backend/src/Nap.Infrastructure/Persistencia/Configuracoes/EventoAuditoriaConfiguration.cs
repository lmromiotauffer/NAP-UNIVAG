using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Nap.Infrastructure.Persistencia.Configuracoes;

public class EventoAuditoriaConfiguration : IEntityTypeConfiguration<EventoAuditoria>
{
    public void Configure(EntityTypeBuilder<EventoAuditoria> builder)
    {
        builder.ToTable("EventosAuditoria");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Acao).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Recurso).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Resultado).HasMaxLength(40).IsRequired();
        builder.HasIndex(e => e.Em);
        builder.HasIndex(e => new { e.UsuarioId, e.Em });
        builder.HasIndex(e => new { e.Acao, e.Em });
    }
}
