using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nap.Domain.Agenda;
using Nap.Domain.Alunos;
using Nap.Domain.Atendimentos;
using Nap.Infrastructure.Identidade;

namespace Nap.Infrastructure.Persistencia.Configuracoes;

public class AtendimentoConfiguration : IEntityTypeConfiguration<Atendimento>
{
    public void Configure(EntityTypeBuilder<Atendimento> builder)
    {
        builder.ToTable("Atendimentos");
        // PK clusterizada em Id: Guid v7 é crescente; a ordenação de leitura é sempre explícita nas consultas.
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property<byte[]>("Versao").IsRowVersion();

        builder.ComplexProperty(a => a.Intervalo, i =>
        {
            i.Property(x => x.Inicio).HasColumnName("Inicio");
            i.Property(x => x.Fim).HasColumnName("Fim");
        });
        builder.ComplexProperty(a => a.Dados, DadosClinicosMapeamento.Configurar(""));
        builder.Property(a => a.JustificativaAvulso).HasMaxLength(500);

        builder.HasOne<Aluno>().WithMany().HasForeignKey(a => a.AlunoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UsuarioIdentidade>().WithMany().HasForeignKey(a => a.ProfissionalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Compromisso>().WithMany().HasForeignKey(a => a.CompromissoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => a.CompromissoId).IsUnique().HasFilter("[CompromissoId] IS NOT NULL")
            .HasDatabaseName("IX_Atendimentos_CompromissoId");
        // Índices (AlunoId, Inicio) e (ProfissionalId, Inicio): o EF não aceita índice com membro de tipo complexo,
        // então são criados à mão na migration Inicial.

        builder.HasMany(a => a.Correcoes).WithOne().HasForeignKey("AtendimentoId").IsRequired().OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.Correcoes).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>Dados clínicos (opcionais) em colunas anuláveis <c>nvarchar(max)</c> da própria tabela. Como todos os
/// campos são anuláveis, um discriminador oculto distingue "sem dados" (null) de dados com todos os campos vazios.</summary>
internal static class DadosClinicosMapeamento
{
    public static Action<ComplexPropertyBuilder<DadosClinicos>> Configurar(string prefixo) => d =>
    {
        d.HasDiscriminator();
        d.Property<string>("Discriminator").HasMaxLength(40);
        d.Property(x => x.DemandaPrincipal).HasColumnName(prefixo + "DemandaPrincipal").HasColumnType("nvarchar(max)");
        d.Property(x => x.ObservacoesPrivadas).HasColumnName(prefixo + "ObservacoesPrivadas").HasColumnType("nvarchar(max)");
        d.Property(x => x.RecomendacoesEncaminhamento).HasColumnName(prefixo + "RecomendacoesEncaminhamento").HasColumnType("nvarchar(max)");
        d.Property(x => x.ProximosPassos).HasColumnName(prefixo + "ProximosPassos").HasColumnType("nvarchar(max)");
    };
}
