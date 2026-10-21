using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nap.Domain.Producao;
using Nap.Infrastructure.Identidade;

namespace Nap.Infrastructure.Persistencia.Configuracoes;

public class AjusteProducaoConfiguration : IEntityTypeConfiguration<AjusteProducao>
{
    public void Configure(EntityTypeBuilder<AjusteProducao> builder)
    {
        builder.ToTable("AjustesProducao");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Categoria).HasConversion<string>().HasMaxLength(30);
        builder.Property(a => a.Justificativa).HasMaxLength(500).IsRequired();

        builder.HasOne<UsuarioIdentidade>().WithMany().HasForeignKey(a => a.ProfissionalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UsuarioIdentidade>().WithMany().HasForeignKey(a => a.AutorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => new { a.ProfissionalId, a.Data });
    }
}
