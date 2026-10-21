using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nap.Domain.Peis;
using Nap.Infrastructure.Identidade;

namespace Nap.Infrastructure.Persistencia.Configuracoes;

public class AssinaturaPeiConfiguration : IEntityTypeConfiguration<AssinaturaPei>
{
    public void Configure(EntityTypeBuilder<AssinaturaPei> builder)
    {
        builder.ToTable("AssinaturasPei");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.NomeSignatario).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Metodo).HasMaxLength(40).IsRequired();
        builder.Property(a => a.HashConteudo).HasColumnType("char(64)").IsRequired();
        builder.Property(a => a.Evidencia).HasMaxLength(200).IsRequired();

        builder.HasOne<UsuarioIdentidade>().WithMany().HasForeignKey(a => a.SignatarioId).OnDelete(DeleteBehavior.Restrict);
        builder.Property<Guid>("VersaoId");
        builder.HasIndex("VersaoId", nameof(AssinaturaPei.Papel)).IsUnique();
    }
}
