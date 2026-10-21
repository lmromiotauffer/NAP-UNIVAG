using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nap.Domain.Notificacoes;
using Nap.Infrastructure.Identidade;

namespace Nap.Infrastructure.Persistencia.Configuracoes;

public class NotificacaoConfiguration : IEntityTypeConfiguration<Notificacao>
{
    public void Configure(EntityTypeBuilder<Notificacao> builder)
    {
        builder.ToTable("Notificacoes");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();
        builder.Property(n => n.Tipo).HasMaxLength(60).IsRequired();
        builder.Property(n => n.Titulo).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Texto).HasMaxLength(1000).IsRequired();
        builder.Property(n => n.Link).HasMaxLength(200).IsRequired();
        builder.HasOne<UsuarioIdentidade>().WithMany().HasForeignKey(n => n.DestinatarioId).OnDelete(DeleteBehavior.Cascade);
        // Reprocessar o mesmo evento da outbox não duplica a notificação.
        builder.HasIndex(n => new { n.EventoId, n.DestinatarioId }).IsUnique().HasDatabaseName("IX_Notificacoes_EventoId_DestinatarioId");
        builder.HasIndex(n => new { n.DestinatarioId, n.LidaEm, n.CriadaEm });
    }
}
