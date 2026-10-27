import { useEffect, useId, useRef, useState, type FormEvent, type ReactNode } from "react";
import { useParams } from "react-router";
import { Alert } from "../../components/ui/Alert";
import { Button } from "../../components/ui/Button";
import { classeSecao, classeTituloPagina } from "../../components/ui/Card";
import { Field } from "../../components/ui/Field";
import { Input } from "../../components/ui/Input";
import { LogoUnivag } from "../../components/ui/LogoUnivag";
import { SecaoTexto as Secao } from "../../components/ui/Secao";
import { mensagemErro, normalizarErro } from "../../lib/api";
import { cn } from "../../lib/cn";
import { formatarDataCivil, formatarDataHora } from "../../lib/format";
import { CAMPOS_PEDAGOGICOS } from "../peis/rotulos";
import { assinaturaService, type VersaoParaAssinatura } from "./services/assinaturaService";

type Etapa = "codigo" | "conteudo" | "concluido" | "invalido";
type Erros = { codigo?: string; nomeCompleto?: string; aceite?: string; geral?: string };

// Mesmo h1 das páginas do sistema; sem anel de foco porque o foco aqui é só programático (fora da ordem do Tab).
const classeTitulo = cn(classeTituloPagina, "focus:outline-none");
const ACEITE = "Li e concordo com este Plano Educacional Individualizado";

/** Erros da API: 404 (qualquer link inválido) encerra o fluxo; 400 vai para o campo; o resto vira alerta. */
function tratar(e: unknown, aoInvalido: () => void): Erros {
  const erro = normalizarErro(e);
  if (erro.status === 404) {
    aoInvalido();
    return {};
  }
  const campo = (nome: string) => erro.errosCampo[nome]?.[0];
  const erros: Erros = { codigo: campo("codigo"), nomeCompleto: campo("nomeCompleto"), aceite: campo("aceite") };
  if (!erros.codigo && !erros.nomeCompleto && !erros.aceite) erros.geral = mensagemErro(e);
  return erros;
}

/**
 * Página anônima do aluno, fora do AppShell. O token vem só da URL e vai só no corpo dos POSTs; o código vive só no
 * estado. Nada é guardado em storage.
 */
export function AssinarPeiPage() {
  const { token = "" } = useParams();
  const [etapa, setEtapa] = useState<Etapa>("codigo");
  const [codigo, setCodigo] = useState("");
  const [versao, setVersao] = useState<VersaoParaAssinatura | null>(null);
  const [nome, setNome] = useState("");
  const [aceite, setAceite] = useState(false);
  const [erros, setErros] = useState<Erros>({});
  const [enviando, setEnviando] = useState(false);
  const titulo = useRef<HTMLHeadingElement>(null);
  const idErroAceite = useId();

  // A cada etapa, o foco vai para o título novo (leitor de tela anuncia onde está).
  useEffect(() => titulo.current?.focus(), [etapa]);

  const invalidar = () => {
    setVersao(null);
    setCodigo("");
    setNome("");
    setAceite(false);
    setEtapa("invalido");
  };

  async function abrir(e: FormEvent) {
    e.preventDefault();
    if (!/^\d{6}$/.test(codigo)) return setErros({ codigo: "Informe os 6 dígitos do código." });
    setErros({});
    setEnviando(true);
    try {
      setVersao(await assinaturaService.abrir(token, codigo));
      setEtapa("conteudo");
    } catch (erro) {
      setErros(tratar(erro, invalidar));
    } finally {
      setEnviando(false);
    }
  }

  async function assinar(e: FormEvent) {
    e.preventDefault();
    const faltando: Erros = {
      ...(nome.trim().length >= 3 ? {} : { nomeCompleto: "Informe seu nome completo." }),
      ...(aceite ? {} : { aceite: "Marque a caixa para concordar com o PEI." }),
    };
    setErros(faltando);
    if (faltando.nomeCompleto || faltando.aceite) return;
    setEnviando(true);
    try {
      await assinaturaService.assinar(token, codigo, nome.trim(), aceite);
      // Depois de assinar, nada do PEI fica na tela nem no estado.
      setVersao(null);
      setCodigo("");
      setNome("");
      setEtapa("concluido");
    } catch (erro) {
      // Nesta etapa não há campo de código: um erro nele vira alerta.
      const r = tratar(erro, invalidar);
      setErros(r.codigo ? { ...r, geral: r.codigo } : r);
    } finally {
      setEnviando(false);
    }
  }

  let corpo: ReactNode;
  if (etapa === "invalido")
    corpo = (
      <div className="flex flex-col gap-1.5">
        <h1 ref={titulo} tabIndex={-1} className={classeTitulo}>
          Link inválido ou expirado
        </h1>
        <p className="m-0 text-suave">Peça um novo link ao NAP.</p>
      </div>
    );
  else if (etapa === "concluido")
    corpo = (
      <div className="flex flex-col gap-1.5">
        <h1 ref={titulo} tabIndex={-1} className={classeTitulo}>
          Assinatura registrada
        </h1>
        <p className="m-0 text-suave">Você já pode fechar esta página.</p>
      </div>
    );
  else if (etapa === "conteudo" && versao) {
    const c = versao.conteudo;
    corpo = (
      <>
        <div className="flex flex-col gap-1">
          <h1 ref={titulo} tabIndex={-1} className={classeTitulo}>
            PEI de {versao.aluno}
          </h1>
          <p className="m-0 text-denso text-suave">
            Versão {versao.numero} · Vigência {formatarDataCivil(c.vigenciaInicio)} a {formatarDataCivil(c.vigenciaFim)} · Link válido até{" "}
            {formatarDataHora(versao.expiraEm)}
          </p>
        </div>
        {CAMPOS_PEDAGOGICOS.map(([campo, rotulo]) => (
          <Secao key={campo} titulo={rotulo}>
            {c[campo] || "Não preenchido."}
          </Secao>
        ))}
        <form noValidate onSubmit={assinar} className={classeSecao}>
          <Alert variante="aguardando">{versao.avisoAssinatura}</Alert>
          {erros.geral && (
            <Alert variante="urgente" className="animate-surgir">
              {erros.geral}
            </Alert>
          )}
          <Field rotulo="Nome completo" erro={erros.nomeCompleto} obrigatorio>
            <Input autoComplete="name" value={nome} onChange={(e) => setNome(e.target.value)} />
          </Field>
          <div className="flex flex-col gap-1">
            <label className="flex cursor-pointer items-start gap-2 text-sm pointer-coarse:min-h-11">
              <input
                type="checkbox"
                checked={aceite}
                onChange={(e) => setAceite(e.target.checked)}
                required
                aria-invalid={erros.aceite ? true : undefined}
                aria-describedby={erros.aceite ? idErroAceite : undefined}
                className="mt-0.5 size-4 accent-primaria"
              />
              {ACEITE}
            </label>
            {erros.aceite && (
              <p id={idErroAceite} role="alert" className="animate-surgir m-0 text-xs text-urgente">
                {erros.aceite}
              </p>
            )}
          </div>
          <p className="m-0 text-xs break-all text-suave">Identificador do conteúdo (hash): {versao.hashConteudo}</p>
          <Button type="submit" carregando={enviando} className="self-start">
            Assinar
          </Button>
        </form>
      </>
    );
  } else
    corpo = (
      <form noValidate onSubmit={abrir} className="flex flex-col gap-4">
        <div className="flex flex-col gap-1.5">
          <h1 ref={titulo} tabIndex={-1} className={classeTitulo}>
            Assinatura do PEI
          </h1>
          <p className="m-0 text-suave">Use o código que você recebeu do NAP junto com este link.</p>
        </div>
        {erros.geral && (
          <Alert variante="urgente" className="animate-surgir">
            {erros.geral}
          </Alert>
        )}
        <Field rotulo="Digite o código de 6 dígitos" erro={erros.codigo}>
          <Input
            inputMode="numeric"
            autoComplete="one-time-code"
            value={codigo}
            onChange={(e) => setCodigo(e.target.value.replace(/\D/g, "").slice(0, 6))}
            className="max-w-40 tracking-[0.3em]"
          />
        </Field>
        <Button type="submit" carregando={enviando} className="self-start">
          Continuar
        </Button>
      </form>
    );

  return (
    <div className="min-h-dvh bg-fundo text-texto">
      <title>Assinatura do PEI · NAP</title>
      <main id="conteudo" className="mx-auto flex w-full max-w-2xl flex-col gap-4 px-4 py-8 sm:px-8">
        <LogoUnivag />
        {/* Chave por etapa: cada passo (código, PEI, confirmação, link inválido) chega com a mesma entrada curta do app. */}
        <div key={etapa} className="animate-surgir flex flex-col gap-4">
          {corpo}
        </div>
      </main>
    </div>
  );
}
