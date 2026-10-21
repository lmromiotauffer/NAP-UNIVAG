using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nap.Infrastructure.Identidade;

namespace Nap.Infrastructure.Persistencia.Configuracoes;

public class TokenRefreshConfiguration : IEntityTypeConfiguration<TokenRefresh>
{
    public void Configure(EntityTypeBuilder<TokenRefresh> builder)
    {
        builder.ToTable("TokensRefresh");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.HashSha256).HasMaxLength(64).IsUnicode(false).IsRequired();
        builder.HasIndex(t => t.HashSha256).IsUnique();
        builder.Property(t => t.CarimboSeguranca).HasMaxLength(256).IsRequired();
        builder.HasIndex(t => t.FamiliaId);
        builder.HasOne<UsuarioIdentidade>().WithMany().HasForeignKey(t => t.UsuarioId).OnDelete(DeleteBehavior.Cascade);
    }
}
