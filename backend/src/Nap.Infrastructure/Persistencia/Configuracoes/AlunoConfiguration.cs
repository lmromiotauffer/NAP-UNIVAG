using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nap.Domain.Alunos;
using Nap.Domain.Cursos;
using Nap.Infrastructure.Identidade;

namespace Nap.Infrastructure.Persistencia.Configuracoes;

public class AlunoConfiguration : IEntityTypeConfiguration<Aluno>
{
    public void Configure(EntityTypeBuilder<Aluno> builder)
    {
        builder.ToTable("Alunos");
        // PK clusterizada em Id: Guid v7 é crescente; a ordenação de leitura é sempre explícita nas consultas.
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property<byte[]>("Versao").IsRowVersion();

        builder.Property(a => a.NomeCompleto).HasMaxLength(200).IsRequired();
        builder.OwnsOne(a => a.Matricula, m =>
        {
            m.Property(x => x.Valor).HasColumnName("Matricula").HasMaxLength(20).IsRequired();
            m.HasIndex(x => x.Valor).IsUnique().HasDatabaseName("IX_Alunos_Matricula");
        });
        builder.Navigation(a => a.Matricula).IsRequired();
        builder.Property(a => a.NecessidadesEducacionais).HasColumnType("nvarchar(max)");
        builder.Property(a => a.ObservacoesAcademicas).HasColumnType("nvarchar(max)");

        builder.HasOne<Curso>().WithMany().HasForeignKey(a => a.CursoAtualId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UsuarioIdentidade>().WithMany().HasForeignKey(a => a.ProfissionalResponsavelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => a.CursoAtualId);

        builder.HasMany(a => a.HistoricoSituacao).WithOne().HasForeignKey("AlunoId").IsRequired().OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.HistoricoSituacao).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(a => a.HistoricoCursos).WithOne().HasForeignKey("AlunoId").IsRequired().OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.HistoricoCursos).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
