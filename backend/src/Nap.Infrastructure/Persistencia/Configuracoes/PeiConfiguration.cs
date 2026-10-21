using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nap.Domain.Alunos;
using Nap.Domain.Peis;
using Nap.Infrastructure.Identidade;

namespace Nap.Infrastructure.Persistencia.Configuracoes;

public class PeiConfiguration : IEntityTypeConfiguration<Pei>
{
    public void Configure(EntityTypeBuilder<Pei> builder)
    {
        builder.ToTable("Peis");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property<byte[]>("Versao").IsRowVersion();

        builder.HasOne<Aluno>().WithMany().HasForeignKey(p => p.AlunoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UsuarioIdentidade>().WithMany().HasForeignKey(p => p.ProfissionalResponsavelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UsuarioIdentidade>().WithMany().HasForeignKey(p => p.CriadoPorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => p.AlunoId);
        builder.HasIndex(p => p.ProfissionalResponsavelId);

        builder.HasMany(p => p.Versoes).WithOne().HasForeignKey(v => v.PeiId).IsRequired().OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Versoes).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(p => p.TrocasResponsavel, t =>
        {
            t.ToTable("TrocasResponsavelPei");
            t.WithOwner().HasForeignKey("PeiId");
            t.Property<int>("Id");
            t.HasKey("Id");
            // Propriedades só com get: mapeadas à mão para o EF as ligar ao construtor.
            t.Property(x => x.ResponsavelAnteriorId);
            t.Property(x => x.NovoResponsavelId);
            t.Property(x => x.Justificativa).HasMaxLength(500).IsRequired();
            t.Property(x => x.AutorId);
            t.Property(x => x.Em);
            t.HasOne<UsuarioIdentidade>().WithMany().HasForeignKey(x => x.ResponsavelAnteriorId).OnDelete(DeleteBehavior.Restrict);
            t.HasOne<UsuarioIdentidade>().WithMany().HasForeignKey(x => x.NovoResponsavelId).OnDelete(DeleteBehavior.Restrict);
            t.HasOne<UsuarioIdentidade>().WithMany().HasForeignKey(x => x.AutorId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Navigation(p => p.TrocasResponsavel).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
