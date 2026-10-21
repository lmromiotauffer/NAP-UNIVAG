using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nap.Domain.Alunos;
using Nap.Domain.Cursos;
using Nap.Domain.Documentos;
using Nap.Infrastructure.Identidade;

namespace Nap.Infrastructure.Persistencia.Configuracoes;

public class DocumentoConfiguration : IEntityTypeConfiguration<Documento>
{
    public void Configure(EntityTypeBuilder<Documento> builder)
    {
        builder.ToTable("Documentos");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property<byte[]>("Versao").IsRowVersion();

        builder.Property(d => d.Descricao).HasMaxLength(500).IsRequired();
        builder.Property(d => d.NomeArquivo).HasMaxLength(200).IsRequired();
        builder.Property(d => d.TipoConteudo).HasMaxLength(100).IsRequired();
        builder.Property(d => d.Sha256).HasMaxLength(64).IsRequired();
        builder.Property(d => d.ChaveArmazenamento).HasMaxLength(32).IsRequired();
        builder.Property(d => d.CompartilhadoJustificativa).HasMaxLength(500);
        builder.Property(d => d.RevogadoJustificativa).HasMaxLength(500);
        builder.Property(d => d.ArquivadoMotivo).HasMaxLength(500);

        builder.HasOne<Aluno>().WithMany().HasForeignKey(d => d.AlunoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Curso>().WithMany().HasForeignKey(d => d.CursoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UsuarioIdentidade>().WithMany().HasForeignKey(d => d.CriadoPorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(d => new { d.AlunoId, d.ArquivadoEm });
        builder.HasIndex(d => new { d.CursoId, d.Visibilidade });
        builder.HasIndex(d => d.ChaveArmazenamento).IsUnique();
    }
}
