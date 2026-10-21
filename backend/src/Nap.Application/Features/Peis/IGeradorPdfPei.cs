namespace Nap.Application.Features.Peis;

public interface IGeradorPdfPei
{
    byte[] Gerar(ModeloPdfPei m);
}
