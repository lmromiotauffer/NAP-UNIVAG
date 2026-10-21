using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nap.Domain.Cursos;

namespace Nap.Infrastructure.Persistencia.Configuracoes;

public class CursoConfiguration : IEntityTypeConfiguration<Curso>
{
    public void Configure(EntityTypeBuilder<Curso> builder)
    {
        builder.ToTable("Cursos");
        // PK clusterizada em Id: Guid v7 é crescente; a ordenação de leitura é sempre explícita nas consultas.
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property<byte[]>("Versao").IsRowVersion();
        builder.Property(c => c.Nome).HasMaxLength(120).IsRequired();
        builder.HasIndex(c => c.Nome).IsUnique();
    }
}
