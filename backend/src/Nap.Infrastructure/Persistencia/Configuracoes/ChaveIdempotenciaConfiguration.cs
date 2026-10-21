using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Nap.Infrastructure.Persistencia.Configuracoes;

public class ChaveIdempotenciaConfiguration : IEntityTypeConfiguration<ChaveIdempotencia>
{
    public void Configure(EntityTypeBuilder<ChaveIdempotencia> builder)
    {
        builder.ToTable("ChavesIdempotencia");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Chave).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Rota).HasMaxLength(200).IsRequired();
        builder.HasIndex(c => new { c.UsuarioId, c.Chave }).IsUnique();
    }
}
