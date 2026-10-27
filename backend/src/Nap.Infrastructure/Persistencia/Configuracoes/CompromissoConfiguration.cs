using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nap.Domain.Agenda;
using Nap.Domain.Alunos;
using Nap.Infrastructure.Identidade;

namespace Nap.Infrastructure.Persistencia.Configuracoes;

public class CompromissoConfiguration : IEntityTypeConfiguration<Compromisso>
{
    public void Configure(EntityTypeBuilder<Compromisso> builder)
    {
        builder.ToTable("Compromissos");
        // PK clusterizada em Id: Guid v7 é crescente; a ordenação de leitura é sempre explícita nas consultas.
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property<byte[]>("Versao").IsRowVersion();

        builder.Property(c => c.Titulo).HasMaxLength(120).IsRequired();
        builder.Property(c => c.Descricao).HasColumnType("nvarchar(max)");
        builder.Property(c => c.MotivoCancelamento).HasMaxLength(500);
        builder.ComplexProperty(c => c.Intervalo, i =>
        {
            i.Property(x => x.Inicio).HasColumnName("Inicio");
            i.Property(x => x.Fim).HasColumnName("Fim");
        });

        builder.HasOne<UsuarioIdentidade>().WithMany().HasForeignKey(c => c.ProfissionalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Aluno>().WithMany().HasForeignKey(c => c.AlunoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UsuarioIdentidade>().WithMany().HasForeignKey(c => c.RealizadoPorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => c.GrupoId);
        // Agenda da equipe inteira (sem profissional) filtra por RealizacaoInicio e por Inicio; o de Inicio é manual, abaixo.
        builder.HasIndex(c => c.RealizacaoInicio);
        builder.HasMany(c => c.CorrecoesRealizacao).WithOne().HasForeignKey("CompromissoId").IsRequired().OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.CorrecoesRealizacao).UsePropertyAccessMode(PropertyAccessMode.Field);
        // Índices (ProfissionalId, Inicio) e (AlunoId, Inicio) filtrado: o EF não aceita índice com membro de tipo
        // complexo, então são criados à mão na migration Inicial; o de (Inicio) na IndicesCompromissosInicio.
    }
}

public class CorrecaoRealizacaoConfiguration : IEntityTypeConfiguration<CorrecaoRealizacao>
{
    public void Configure(EntityTypeBuilder<CorrecaoRealizacao> builder)
    {
        builder.ToTable("CorrecoesRealizacao");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Justificativa).HasMaxLength(500).IsRequired();
        builder.HasOne<UsuarioIdentidade>().WithMany().HasForeignKey(c => c.AutorId).OnDelete(DeleteBehavior.Restrict);
    }
}
