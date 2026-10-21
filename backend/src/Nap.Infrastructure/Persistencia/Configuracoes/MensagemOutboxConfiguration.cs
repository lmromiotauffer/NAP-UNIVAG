using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Nap.Infrastructure.Persistencia.Configuracoes;

public class MensagemOutboxConfiguration : IEntityTypeConfiguration<MensagemOutbox>
{
    public void Configure(EntityTypeBuilder<MensagemOutbox> builder)
    {
        builder.ToTable("Outbox");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Tipo).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Conteudo).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(m => m.UltimoErro).HasMaxLength(2000);
        builder.HasIndex(m => new { m.ProximaTentativaEm, m.OcorridoEm }).HasFilter("[ProcessadoEm] IS NULL");
    }
}
