import { zodResolver } from "@hookform/resolvers/zod";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { Link, useNavigate } from "react-router";
import { z } from "zod";
import { Alert } from "../components/ui/Alert";
import { Button } from "../components/ui/Button";
import { Field } from "../components/ui/Field";
import { Input } from "../components/ui/Input";
import { LogoUnivag } from "../components/ui/LogoUnivag";
import { useToast } from "../components/ui/Toast";
import { tratarErro } from "../lib/api";
import { useSessao } from "./useSessao";

// Espelha as regras da API (Identity): mínimo 10, ao menos um dígito e uma maiúscula.
const esquema = z
  .object({
    senhaAtual: z.string().min(1, "Informe a senha atual."),
    novaSenha: z
      .string()
      .min(10, "A nova senha deve ter no mínimo 10 caracteres.")
      .regex(/\d/, "A nova senha deve ter ao menos um número.")
      .regex(/\p{Lu}/u, "A nova senha deve ter ao menos uma letra maiúscula."),
    confirmacao: z.string(),
  })
  .refine((d) => d.novaSenha === d.confirmacao, { path: ["confirmacao"], message: "A confirmação deve ser igual à nova senha." });
type Dados = z.infer<typeof esquema>;
const CAMPOS = ["senhaAtual", "novaSenha", "confirmacao"] as const;

/** Fora do AppShell: com troca pendente é a única tela acessível, então traz o próprio "Sair". */
export function TrocarSenhaPage() {
  const { usuario, trocarSenha, sair } = useSessao();
  const navigate = useNavigate();
  const toast = useToast();
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const [mostrar, setMostrar] = useState(false);
  const tipo = mostrar ? "text" : "password";
  const obrigatoria = !!usuario?.deveTrocarSenha;
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<Dados>({ resolver: zodResolver(esquema), defaultValues: { senhaAtual: "", novaSenha: "", confirmacao: "" } });

  async function enviar({ senhaAtual, novaSenha }: Dados) {
    setErroGeral(null);
    try {
      await trocarSenha(senhaAtual, novaSenha);
      toast.sucesso("Senha alterada.");
      void navigate("/", { replace: true });
    } catch (e) {
      setErroGeral(tratarErro(e, CAMPOS, (campo, message) => setError(campo, { message })));
    }
  }

  return (
    <main id="conteudo" className="flex min-h-dvh items-center justify-center bg-fundo px-4 py-10 text-texto sm:px-8">
      <title>{`${obrigatoria ? "Defina uma nova senha" : "Trocar senha"} · NAP`}</title>
      <div className="animate-surgir flex w-full max-w-sm flex-col gap-6">
        <LogoUnivag />
        <div className="flex flex-col gap-1.5">
          <h1 className="font-titulo text-[1.75rem] leading-[1.05] font-black tracking-[-0.02em]">{obrigatoria ? "Defina uma nova senha" : "Trocar senha"}</h1>
          <p className="text-suave">
            {obrigatoria ? "Sua senha é temporária. Defina uma nova para continuar." : "Informe a senha atual e escolha uma nova."} Use ao menos 10 caracteres, com um número e uma letra maiúscula.
          </p>
        </div>
        <form noValidate onSubmit={handleSubmit(enviar)} className="flex flex-col gap-4">
          {erroGeral && (
            <Alert variante="urgente" className="animate-surgir">
              {erroGeral}
            </Alert>
          )}
          <Field rotulo="Senha atual" erro={errors.senhaAtual?.message}>
            <Input type={tipo} autoComplete="current-password" {...register("senhaAtual")} />
          </Field>
          <Field rotulo="Nova senha" erro={errors.novaSenha?.message}>
            <Input type={tipo} autoComplete="new-password" {...register("novaSenha")} />
          </Field>
          <Field rotulo="Confirmar nova senha" erro={errors.confirmacao?.message}>
            <Input type={tipo} autoComplete="new-password" {...register("confirmacao")} />
          </Field>
          <label className="flex cursor-pointer items-center gap-2 text-sm pointer-coarse:min-h-11">
            <input type="checkbox" checked={mostrar} onChange={(e) => setMostrar(e.target.checked)} className="size-4 accent-primaria" />
            Mostrar senhas
          </label>
          <Button type="submit" carregando={isSubmitting} className="mt-1 w-full">
            Salvar nova senha
          </Button>
        </form>
        {obrigatoria ? (
          <Button variante="fantasma" onClick={() => void sair()} className="w-full">
            Sair
          </Button>
        ) : (
          <Link to="/" className="inline-flex items-center justify-center text-sm font-semibold text-primaria underline-offset-2 hover:underline pointer-coarse:min-h-11">
            Cancelar
          </Link>
        )}
      </div>
    </main>
  );
}
