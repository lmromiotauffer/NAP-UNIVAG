using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nap.Domain.Alunos;
using Nap.Domain.Comunicacoes;
using Nap.Domain.Cursos;
using Nap.Domain.Encaminhamentos;
using Nap.Domain.Peis;
using Nap.Infrastructure.Identidade;

namespace Nap.Infrastructure.Persistencia.Configuracoes;

public class MensagemConfiguration : IEntityTypeConfiguration<Mensagem>
{
    public void Configure(EntityTypeBuilder<Mensagem> builder)
    {
        builder.ToTable("Mensagens");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property<byte[]>("Versao").IsRowVersion();
        // Eventos vão para a outbox no SaveChanges; não são navegação.
        builder.Ignore(m => m.Eventos);

        builder.Property(m => m.Assunto).HasMaxLength(200).IsRequired();
        // Até 10000 caracteres: passa de 4000, então nvarchar(max).
        builder.Property(m => m.Corpo).IsRequired();

        builder.HasOne<UsuarioIdentidade>().WithMany().HasForeignKey(m => m.AutorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UsuarioIdentidade>().WithMany().HasForeignKey(m => m.DestinatarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Aluno>().WithMany().HasForeignKey(m => m.AlunoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Curso>().WithMany().HasForeignKey(m => m.CursoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Encaminhamento>().WithMany().HasForeignKey(m => m.EncaminhamentoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<VersaoPei>().WithMany().HasForeignKey(m => m.PeiVersaoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(m => new { m.DestinatarioId, m.Estado, m.LidaEm });
        builder.HasIndex(m => new { m.AutorId, m.Estado });
        builder.HasIndex(m => m.FioId);
        builder.HasIndex(m => m.AlunoId);
    }
}
