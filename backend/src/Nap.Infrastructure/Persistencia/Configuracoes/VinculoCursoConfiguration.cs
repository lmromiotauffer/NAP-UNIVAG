using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nap.Domain.Alunos;
using Nap.Domain.Cursos;

namespace Nap.Infrastructure.Persistencia.Configuracoes;

public class VinculoCursoConfiguration : IEntityTypeConfiguration<VinculoCurso>
{
    public void Configure(EntityTypeBuilder<VinculoCurso> builder)
    {
        builder.ToTable("VinculosCurso");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.Property(v => v.Motivo).HasMaxLength(500).IsRequired();
        builder.HasOne<Curso>().WithMany().HasForeignKey(v => v.CursoId).OnDelete(DeleteBehavior.Restrict);
    }
}
