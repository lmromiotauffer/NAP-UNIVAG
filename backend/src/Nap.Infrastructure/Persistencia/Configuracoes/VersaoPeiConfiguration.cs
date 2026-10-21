using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nap.Domain.Cursos;
using Nap.Domain.Peis;
using Nap.Infrastructure.Identidade;

namespace Nap.Infrastructure.Persistencia.Configuracoes;

public class VersaoPeiConfiguration : IEntityTypeConfiguration<VersaoPei>
{
    public void Configure(EntityTypeBuilder<VersaoPei> builder)
    {
        builder.ToTable("VersoesPei");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.Property<byte[]>("Versao").IsRowVersion();

        builder.ComplexProperty(v => v.Conteudo, c =>
        {
            c.Property(x => x.DemandaInterna).HasMaxLength(4000);
            c.Property(x => x.NecessidadesEducacionais).HasMaxLength(4000);
            c.Property(x => x.Adaptacoes).HasMaxLength(4000);
            c.Property(x => x.OrientacoesProfessores).HasMaxLength(4000);
            c.Property(x => x.RecursosApoio).HasMaxLength(4000);
            c.Property(x => x.CuidadosPedagogicos).HasMaxLength(4000);
            c.ComplexProperty(x => x.Vigencia, p =>
            {
                p.Property(x => x.Inicio).HasColumnName("VigenciaInicio");
                p.Property(x => x.Fim).HasColumnName("VigenciaFim");
            });
        });
        builder.Property(v => v.HashConteudo).HasColumnType("char(64)");
        builder.Property(v => v.JustificativaDevolucao).HasMaxLength(500);
        builder.Property(v => v.MotivoArquivamento).HasMaxLength(500);

        builder.HasOne<UsuarioIdentidade>().WithMany().HasForeignKey(v => v.CriadaPorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UsuarioIdentidade>().WithMany().HasForeignKey(v => v.AprovadaPorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UsuarioIdentidade>().WithMany().HasForeignKey(v => v.PublicadaPorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Curso>().WithMany().HasForeignKey(v => v.CursoPublicacaoId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(v => new { v.PeiId, v.Numero }).IsUnique();
        // Enums são texto: no máximo uma versão aberta por PEI.
        builder.HasIndex(v => v.PeiId).IsUnique()
            .HasFilter("[Estado] IN (N'Rascunho', N'EmRevisao', N'AguardandoAssinaturas')")
            .HasDatabaseName("IX_VersoesPei_PeiId_Aberta");
        builder.HasIndex(v => new { v.CursoPublicacaoId, v.Estado });

        builder.HasMany(v => v.Assinaturas).WithOne().HasForeignKey("VersaoId").IsRequired().OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(v => v.Assinaturas).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(v => v.Ciencias).WithOne().HasForeignKey("VersaoId").IsRequired().OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(v => v.Ciencias).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
