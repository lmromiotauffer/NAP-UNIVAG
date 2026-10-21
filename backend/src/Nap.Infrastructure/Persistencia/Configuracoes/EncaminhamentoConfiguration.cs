using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nap.Domain.Alunos;
using Nap.Domain.Cursos;
using Nap.Domain.Encaminhamentos;
using Nap.Infrastructure.Identidade;

namespace Nap.Infrastructure.Persistencia.Configuracoes;

public class EncaminhamentoConfiguration : IEntityTypeConfiguration<Encaminhamento>
{
    public void Configure(EntityTypeBuilder<Encaminhamento> builder)
    {
        builder.ToTable("Encaminhamentos");
        // PK clusterizada em Id: Guid v7 é crescente; a ordenação de leitura é sempre explícita nas consultas.
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property<byte[]>("Versao").IsRowVersion();
        // Eventos vão para a outbox no SaveChanges; não são navegação.
        builder.Ignore(e => e.Eventos);

        builder.Property(e => e.MotivoRestrito).HasMaxLength(4000).IsRequired();
        builder.Property(e => e.ResumoCompartilhavel).HasMaxLength(2000).IsRequired();
        builder.Property(e => e.Observacoes).HasMaxLength(4000);

        builder.HasOne<Aluno>().WithMany().HasForeignKey(e => e.AlunoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Curso>().WithMany().HasForeignKey(e => e.CursoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UsuarioIdentidade>().WithMany().HasForeignKey(e => e.SolicitanteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => e.AlunoId);
        builder.HasIndex(e => new { e.CursoId, e.Estado });
        builder.HasIndex(e => new { e.Destino, e.Estado });
        builder.HasIndex(e => new { e.SolicitanteId, e.Estado });

        builder.HasMany(e => e.Evolucoes).WithOne().HasForeignKey("EncaminhamentoId").IsRequired().OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(e => e.Evolucoes).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
