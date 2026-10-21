import { zodResolver } from "@hookform/resolvers/zod";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { rotuloPerfil } from "../../../auth/capacidades";
import { Alert } from "../../../components/ui/Alert";
import { Button } from "../../../components/ui/Button";
import { Field } from "../../../components/ui/Field";
import { Input } from "../../../components/ui/Input";
import { Select } from "../../../components/ui/Select";
import { tratarErro } from "../../../lib/api";
import type { UsuarioAdmin } from "../../../lib/types";
import { useCursos } from "../../compartilhado/catalogos";
import { useAtualizarUsuario } from "../hooks/useAdministracao";
import type { CriarUsuarioBody } from "../services/administracaoService";

export const PERFIS = ["Administrador", "CoordenadorNap", "ProfissionalNap", "CoordenadorCurso", "Sip", "Gestao"] as const;

// Na edição o e-mail não é exibido nem validado.
const esquemaDe = (criando: boolean) =>
  z
    .object({
      email: criando ? z.email("Informe um e-mail válido.") : z.string(),
      nomeCompleto: z.string().trim().min(3, "Informe o nome completo (mínimo 3 caracteres).").max(200, "Use no máximo 200 caracteres."),
      perfil: z.enum(PERFIS, "Selecione o perfil."),
      cursoId: z.string(),
    })
    .refine((d) => d.perfil !== "CoordenadorCurso" || d.cursoId !== "", { path: ["cursoId"], message: "Selecione o curso." });
type Dados = z.infer<ReturnType<typeof esquemaDe>>;
const CAMPOS = ["email", "nomeCompleto", "perfil", "cursoId"] as const;

export function UsuarioForm({
  usuario,
  ehAdministrador,
  ehProprio,
  aoCriar,
  aoConcluir,
  aoCancelar,
}: {
  usuario?: UsuarioAdmin;
  /** Só o Administrador atribui o perfil Administrador. */
  ehAdministrador: boolean;
  /** Ninguém altera o próprio perfil ou curso: os campos viram texto. */
  ehProprio?: boolean;
  /** A criação mora no dono do diálogo da senha temporária (a senha não pode ficar no estado do formulário). */
  aoCriar: (body: CriarUsuarioBody) => Promise<unknown>;
  aoConcluir: () => void;
  aoCancelar: () => void;
}) {
  // Snapshot do mount: após um 409 a lista refetchada traz versão nova, mas os campos continuam velhos.
  const [versao] = useState(usuario?.versao);
  const [esquema] = useState(() => esquemaDe(!usuario));
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const cursos = useCursos();
  const atualizar = useAtualizarUsuario();
  const {
    register,
    handleSubmit,
    setError,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<Dados>({
    resolver: zodResolver(esquema),
    defaultValues: {
      email: usuario?.email ?? "",
      nomeCompleto: usuario?.nome ?? "",
      perfil: (usuario?.perfil as Dados["perfil"] | undefined) ?? ("" as Dados["perfil"]),
      cursoId: usuario?.cursoId ?? "",
    },
  });
  const perfil = watch("perfil");
  const perfisPermitidos = PERFIS.filter((p) => ehAdministrador || p !== "Administrador");
  // Curso inativo só continua na lista se for o atual do usuário.
  const listaCursos = cursos.data?.filter((c) => c.ativo || c.id === usuario?.cursoId) ?? [];

  async function enviar(dados: Dados) {
    setErroGeral(null);
    const cursoId = dados.perfil === "CoordenadorCurso" ? dados.cursoId : null;
    try {
      if (usuario) await atualizar.mutateAsync({ id: usuario.id, body: { nomeCompleto: dados.nomeCompleto, perfil: dados.perfil, cursoId }, versao: versao ?? usuario.versao });
      else await aoCriar({ email: dados.email, nomeCompleto: dados.nomeCompleto, perfil: dados.perfil, cursoId });
      aoConcluir();
    } catch (e) {
      // 409 (e-mail em uso, versão antiga), 422 (último administrador) e 403: Alert, mantendo o formulário.
      setErroGeral(tratarErro(e, usuario ? CAMPOS.filter((c) => c !== "email") : CAMPOS, (campo, message) => setError(campo, { message })));
    }
  }

  return (
    <form noValidate onSubmit={handleSubmit(enviar)} className="flex flex-col gap-4">
      {erroGeral && <Alert variante="urgente">{erroGeral}</Alert>}
      {cursos.isError && <Alert variante="urgente">Não foi possível carregar os cursos. Feche e tente novamente.</Alert>}
      <Field rotulo="Nome completo" erro={errors.nomeCompleto?.message} obrigatorio>
        <Input autoComplete="off" {...register("nomeCompleto")} />
      </Field>
      {usuario ? (
        <dl className="text-sm">
          <dt className="text-xs text-suave">E-mail (é o login)</dt>
          <dd className="break-all">{usuario.email}</dd>
        </dl>
      ) : (
        <Field rotulo="E-mail" erro={errors.email?.message} dica="É o login da pessoa." obrigatorio>
          <Input type="email" autoComplete="off" inputMode="email" {...register("email")} />
        </Field>
      )}
      {ehProprio ? (
        <dl className="text-sm">
          <dt className="text-xs text-suave">Perfil</dt>
          <dd>{rotuloPerfil(usuario?.perfil ?? "")}</dd>
          {usuario?.curso && (
            <>
              <dt className="mt-2 text-xs text-suave">Curso</dt>
              <dd>{usuario.curso}</dd>
            </>
          )}
          <dd className="mt-2 text-xs text-suave">Você não pode alterar o próprio acesso.</dd>
        </dl>
      ) : (
        <>
          <Field rotulo="Perfil" erro={errors.perfil?.message} obrigatorio>
            <Select {...register("perfil")}>
              <option value="">Selecione</option>
              {perfisPermitidos.map((p) => (
                <option key={p} value={p}>
                  {rotuloPerfil(p)}
                </option>
              ))}
            </Select>
          </Field>
          {perfil === "CoordenadorCurso" && (
            <Field rotulo="Curso" erro={errors.cursoId?.message} obrigatorio>
              <Select {...register("cursoId")}>
                <option value="">{cursos.isPending ? "Carregando…" : "Selecione"}</option>
                {listaCursos.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.nome}
                  </option>
                ))}
              </Select>
            </Field>
          )}
        </>
      )}
      <div className="flex flex-wrap justify-end gap-2">
        <Button variante="secundaria" onClick={aoCancelar} disabled={isSubmitting}>
          Cancelar
        </Button>
        <Button type="submit" carregando={isSubmitting}>
          Salvar
        </Button>
      </div>
    </form>
  );
}
