using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nap.Infrastructure.Identidade;

namespace Nap.Infrastructure.Persistencia.Configuracoes;

public class UsuarioIdentidadeConfiguration : IEntityTypeConfiguration<UsuarioIdentidade>
{
    public void Configure(EntityTypeBuilder<UsuarioIdentidade> builder)
    {
        builder.Property(u => u.NomeCompleto).HasMaxLength(200).IsRequired();
        builder.Property(u => u.Perfil).HasMaxLength(40).IsRequired();
    }
}
