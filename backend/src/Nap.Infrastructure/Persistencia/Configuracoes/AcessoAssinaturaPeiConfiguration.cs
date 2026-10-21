using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nap.Domain.Peis;
using Nap.Infrastructure.Identidade;

namespace Nap.Infrastructure.Persistencia.Configuracoes;

public class AcessoAssinaturaPeiConfiguration : IEntityTypeConfiguration<AcessoAssinaturaPei>
{
    public void Configure(EntityTypeBuilder<AcessoAssinaturaPei> builder)
    {
        builder.ToTable("AcessosAssinaturaPei");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        // Tentativas erradas simultâneas não podem passar as duas: a segunda gravação falha por concorrência.
        builder.Property<byte[]>("Versao").IsRowVersion();

        builder.Property(a => a.HashConteudo).HasColumnType("char(64)").IsRequired();
        builder.Property(a => a.TokenHash).HasColumnType("char(64)").IsRequired();
        builder.Property(a => a.CodigoHash).HasColumnType("char(64)").IsRequired();

        builder.HasOne<VersaoPei>().WithMany().HasForeignKey(a => a.VersaoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<UsuarioIdentidade>().WithMany().HasForeignKey(a => a.CriadoPorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => a.TokenHash).IsUnique();
        builder.HasIndex(a => a.VersaoId);
        // Duas gerações simultâneas não podem deixar dois acessos abertos para a mesma versão.
        builder.HasIndex(a => a.VersaoId, "IX_AcessosAssinaturaPei_VersaoId_Aberto").IsUnique()
            .HasFilter("[UsadoEm] IS NULL AND [InvalidadoEm] IS NULL");
    }
}
