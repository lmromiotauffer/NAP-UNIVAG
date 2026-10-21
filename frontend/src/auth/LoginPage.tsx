import { zodResolver } from "@hookform/resolvers/zod";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { Navigate, useSearchParams } from "react-router";
import { z } from "zod";
import { Alert } from "../components/ui/Alert";
import { Button } from "../components/ui/Button";
import { Field } from "../components/ui/Field";
import { Input } from "../components/ui/Input";
import { LogoUnivag } from "../components/ui/LogoUnivag";
import { mensagemErro, normalizarErro } from "../lib/api";
import { useSessao } from "./useSessao";

const esquema = z.object({
  email: z.email("Informe um e-mail válido."),
  senha: z.string().min(1, "Informe a senha."),
});
type Credenciais = z.infer<typeof esquema>;

/** Só caminhos relativos da própria origem: bloqueia "//host", "/\host" e caracteres de controle (open redirect). */
export function destinoSeguro(voltar: string | null): string {
  if (!voltar || !/^\/(?![/\\])/.test(voltar) || /[\u0000-\u001f\\]/.test(voltar) || voltar.startsWith("/entrar")) return "/";
  return voltar;
}

export function LoginPage() {
  const { estado, entrar } = useSessao();
  const [parametros] = useSearchParams();
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  // A foto só "assenta" quando carregou: a página é lazy e sem preload, então ela chegaria pipocando no meio da animação.
  const [fotoPronta, setFotoPronta] = useState(false);
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<Credenciais>({ resolver: zodResolver(esquema), defaultValues: { email: "", senha: "" } });

  if (estado === "autenticado") return <Navigate to={destinoSeguro(parametros.get("voltar"))} replace />;

  async function enviar({ email, senha }: Credenciais) {
    setErroGeral(null);
    try {
      await entrar(email, senha);
    } catch (e) {
      // 400/401: texto fixo, sem apontar o campo. Rede, 429 e 5xx: a mensagem real ajuda a agir.
      const { status } = normalizarErro(e);
      setErroGeral(status === 0 || status === 429 || status >= 500 ? mensagemErro(e) : "E-mail ou senha inválidos.");
    }
  }

  return (
    <div className="grid min-h-dvh bg-fundo text-texto md:grid-cols-[minmax(0,1.1fr)_minmax(0,1fr)]">
      <title>Entrar · NAP</title>
      <div className="relative hidden overflow-hidden md:block">
        {/* Fontes só a partir de md: no celular o img fica em "data:," e nada é baixado. */}
        <picture>
          <source media="(min-width: 768px)" srcSet="/visao-faculdade.webp" type="image/webp" />
          <source media="(min-width: 768px)" srcSet="/visao-faculdade.jpg" />
          <img src="data:," alt="" width={2048} height={1364} fetchPriority="high" decoding="async" onLoad={() => setFotoPronta(true)} className={`absolute inset-0 size-full object-cover ${fotoPronta ? "animate-aproximar" : "opacity-0"}`} />
        </picture>
        {/* Escurecimento só embaixo, onde ficam logo e título: mantém as cores da foto e deixa o céu livre. */}
        <div aria-hidden="true" className="absolute inset-x-0 bottom-0 h-3/5 bg-linear-to-t from-bloco/90 via-bloco/45 to-transparent" />
        <div className="relative flex h-full flex-col justify-end p-10 text-bloco-texto">
          {/* Marca e nome do núcleo num só bloco: a instituição assina o serviço. O logo branco é derivado do PNG oficial. */}
          <div className="animate-entrada flex flex-col gap-5">
            <img src="/logo-univag-branco.png" alt="UNIVAG" width={176} height={73} className="h-auto w-36 lg:w-44" />
            <p className="max-w-xl font-titulo text-[clamp(2rem,4vw,3.75rem)] leading-[0.98] font-black tracking-[-0.035em] text-balance">Núcleo de Apoio Psicopedagógico</p>
          </div>
        </div>
      </div>

      {/* Ancorado no terço superior, não centralizado: as mensagens de erro crescem para baixo sem mover o título. */}
      <main id="conteudo" className="flex items-start justify-center px-4 pt-[28vh] pb-10 sm:px-8">
        <div className="flex w-full max-w-sm flex-col gap-6">
          <LogoUnivag className="animate-entrada md:hidden" />
          <div className="animate-entrada flex flex-col gap-1.5">
            <h1 className="font-titulo text-[1.75rem] leading-[1.05] font-black tracking-[-0.02em]">Entrar no NAP</h1>
            <p className="text-suave">Use seu e-mail institucional e a senha cadastrada.</p>
          </div>

          <form noValidate onSubmit={handleSubmit(enviar)} className="animate-entrada flex flex-col gap-4 [animation-delay:90ms]">
            {erroGeral && (
              <Alert variante="urgente" className="animate-surgir">
                {erroGeral}
              </Alert>
            )}
            <Field rotulo="E-mail" erro={errors.email?.message}>
              <Input type="email" autoComplete="username" inputMode="email" autoFocus {...register("email")} />
            </Field>
            <Field rotulo="Senha" erro={errors.senha?.message}>
              <Input type="password" autoComplete="current-password" {...register("senha")} />
            </Field>
            <Button type="submit" carregando={isSubmitting} disabled={estado === "carregando"} className="mt-1 w-full">
              {isSubmitting ? "Entrando…" : "Entrar"}
            </Button>
          </form>
          {/* Não há "esqueci a senha": a coordenação do NAP emite uma senha temporária pela Administração. */}
          <p className="animate-entrada text-center text-sm text-balance text-suave [animation-delay:180ms]">Sem acesso ou esqueceu a senha? Fale com a coordenação do NAP.</p>
        </div>
      </main>
    </div>
  );
}
