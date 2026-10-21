using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nap.Domain.Agenda;
using Nap.Domain.Atividades;
using Nap.Domain.Cursos;
using Nap.Infrastructure.Identidade;

namespace Nap.Infrastructure.Persistencia.Configuracoes;

public class AtividadeCursoConfiguration : IEntityTypeConfiguration<AtividadeCurso>
{
    public void Configure(EntityTypeBuilder<AtividadeCurso> builder)
    {
        builder.ToTable("AtividadesCurso");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property<byte[]>("Versao").IsRowVersion();

        builder.ComplexProperty(a => a.Intervalo, i =>
        {
            i.Property(x => x.Inicio).HasColumnName("Inicio");
            i.Property(x => x.Fim).HasColumnName("Fim");
        });
        builder.Property(a => a.Tipo).HasConversion<string>().HasMaxLength(30);
        builder.Property(a => a.Descricao).HasMaxLength(2000);
        builder.Property(a => a.JustificativaAvulso).HasMaxLength(500);

        builder.HasOne<Curso>().WithMany().HasForeignKey(a => a.CursoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UsuarioIdentidade>().WithMany().HasForeignKey(a => a.ProfissionalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UsuarioIdentidade>().WithMany().HasForeignKey(a => a.CriadoPorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Compromisso>().WithMany().HasForeignKey(a => a.CompromissoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => a.CompromissoId).IsUnique().HasFilter("[CompromissoId] IS NOT NULL")
            .HasDatabaseName("IX_AtividadesCurso_CompromissoId");
        // Índices (ProfissionalId, Inicio) e (CursoId, Inicio): o EF não aceita índice com membro de tipo complexo,
        // então são criados à mão na migration.

        builder.HasMany(a => a.Correcoes).WithOne().HasForeignKey("AtividadeId").IsRequired().OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.Correcoes).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
