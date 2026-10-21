using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nap.Domain.Peis;
using Nap.Infrastructure.Identidade;

namespace Nap.Infrastructure.Persistencia.Configuracoes;

public class CienciaPeiConfiguration : IEntityTypeConfiguration<CienciaPei>
{
    public void Configure(EntityTypeBuilder<CienciaPei> builder)
    {
        builder.ToTable("CienciasPei");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.HasOne<UsuarioIdentidade>().WithMany().HasForeignKey(c => c.CoordenadorId).OnDelete(DeleteBehavior.Restrict);
        builder.Property<Guid>("VersaoId");
        builder.HasIndex("VersaoId", nameof(CienciaPei.CoordenadorId)).IsUnique();
    }
}
