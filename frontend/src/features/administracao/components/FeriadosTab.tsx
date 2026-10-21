import { zodResolver } from "@hookform/resolvers/zod";
import { CalendarOff, Plus } from "lucide-react";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert } from "../../../components/ui/Alert";
import { Button } from "../../../components/ui/Button";
import { ConfirmDialog } from "../../../components/ui/ConfirmDialog";
import { EmptyState } from "../../../components/ui/EmptyState";
import { ErrorState } from "../../../components/ui/ErrorState";
import { Field } from "../../../components/ui/Field";
import { classeFiltro, Input } from "../../../components/ui/Input";
import { Modal } from "../../../components/ui/Modal";
import { Select } from "../../../components/ui/Select";
import { Skeleton } from "../../../components/ui/Skeleton";
import { classeMolduraRolavel, Table, th, td } from "../../../components/ui/Table";
import { useToast } from "../../../components/ui/Toast";
import { mensagemErro, tratarErro } from "../../../lib/api";
import { cn } from "../../../lib/cn";
import { DIA, formatarDataCivil, hojeCivil } from "../../../lib/format";
import type { Feriado } from "../../../lib/types";
import { useCriarFeriado, useInativarFeriado, useListaFeriados } from "../hooks/useAdministracao";
import type { FeriadoCriado } from "../services/administracaoService";
import { FeriadoConflitosDialog } from "./FeriadoConflitosDialog";


const esquema = z.object({
  data: z.string().regex(DIA, "Informe a data do feriado."),
  descricao: z.string().trim().min(3, "Informe a descrição (de 3 a 120 caracteres).").max(120, "Informe a descrição (de 3 a 120 caracteres)."),
});
type Dados = z.infer<typeof esquema>;
const CAMPOS = ["data", "descricao"] as const;

function FeriadoForm({ aoCriado, aoCancelar }: { aoCriado: (r: FeriadoCriado) => void; aoCancelar: () => void }) {
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const criar = useCriarFeriado();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<Dados>({ resolver: zodResolver(esquema), defaultValues: { data: "", descricao: "" } });

  async function enviar(body: Dados) {
    setErroGeral(null);
    try {
      aoCriado(await criar.mutateAsync(body));
    } catch (e) {
      // 409 (data já é feriado) vai para o Alert, mantendo o formulário.
      setErroGeral(tratarErro(e, CAMPOS, (campo, message) => setError(campo, { message })));
    }
  }

  return (
    <form noValidate onSubmit={handleSubmit(enviar)} className="flex flex-col gap-4">
      {erroGeral && <Alert variante="urgente">{erroGeral}</Alert>}
      <Field rotulo="Data" erro={errors.data?.message} obrigatorio>
        <Input type="date" {...register("data")} />
      </Field>
      <Field rotulo="Descrição" erro={errors.descricao?.message} obrigatorio>
        <Input autoComplete="off" maxLength={120} {...register("descricao")} />
      </Field>
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

export function FeriadosTab() {
  const toast = useToast();
  const anoAtual = Number(hojeCivil().slice(0, 4));
  const [ano, setAno] = useState(anoAtual);
  const feriados = useListaFeriados(ano);
  const inativar = useInativarFeriado();
  const [novo, setNovo] = useState(false);
  const [conflitos, setConflitos] = useState<FeriadoCriado["conflitos"] | null>(null);
  const [removendo, setRemovendo] = useState<Feriado | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  async function confirmarRemocao() {
    if (!removendo) return;
    setErro(null);
    try {
      await inativar.mutateAsync(removendo.id);
      toast.sucesso("Feriado removido.");
      setRemovendo(null);
    } catch (e) {
      setErro(mensagemErro(e));
    }
  }

  let corpo;
  if (feriados.isError) {
    corpo = (
      <div className="p-4">
        <ErrorState erro={feriados.error} aoTentarNovamente={() => void feriados.refetch()} />
      </div>
    );
  } else if (feriados.data?.length === 0 && !feriados.isPlaceholderData) {
    corpo = <EmptyState icone={<CalendarOff />} titulo={`Nenhum feriado em ${ano}`} descricao="Cadastre os feriados para bloquear agendamentos nessas datas." />;
  } else {
    corpo = (
      <Table caption={`Feriados de ${ano}`} aria-busy={feriados.isPending || feriados.isPlaceholderData} className={cn(feriados.isPlaceholderData && "opacity-60")}>
        <thead>
          <tr className="border-b border-borda">
            <th scope="col" className={th}>Data</th>
            <th scope="col" className={th}>Descrição</th>
            <th scope="col" className={th}>
              <span className="sr-only">Ações</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {feriados.data
            ? feriados.data.map((f) => (
                <tr key={f.id} className="border-b border-borda text-corpo">
                  <td className={td}>{formatarDataCivil(f.data)}</td>
                  <td className={td}>
                    <span className="font-semibold">{f.descricao}</span>
                  </td>
                  <td className={`${td} text-right`}>
                    <Button
                      tamanho="sm"
                      variante="secundaria"
                      aria-label={`Remover ${f.descricao}`}
                      onClick={() => {
                        setErro(null);
                        setRemovendo(f);
                      }}
                    >
                      Remover
                    </Button>
                  </td>
                </tr>
              ))
            : Array.from({ length: 4 }, (_, i) => (
                <tr key={i} aria-hidden="true" className="border-b border-borda">
                  <td className={td}>
                    <Skeleton className="h-4 w-24" />
                  </td>
                  <td className={td}>
                    <Skeleton className="h-4 w-48" />
                  </td>
                  <td className={td} />
                </tr>
              ))}
        </tbody>
      </Table>
    );
  }

  return (
    <>
      <div className="flex flex-wrap items-center justify-between gap-2">
        {/* Pílula como nas outras barras de filtro; o rótulo fica visível porque o ano sozinho não diz o que o campo é. */}
        <div className="flex items-center gap-1.5 text-denso text-suave">
          <label htmlFor="feriados-ano">Ano</label>
          <Select id="feriados-ano" className={cn(classeFiltro, "min-w-[6rem] sm:min-w-[6rem]")} value={ano} onChange={(e) => setAno(Number(e.target.value))}>
            {[anoAtual - 1, anoAtual, anoAtual + 1, anoAtual + 2].map((a) => (
              <option key={a} value={a}>
                {a}
              </option>
            ))}
          </Select>
        </div>
        <Button onClick={() => setNovo(true)} className="rounded-full">
          <Plus aria-hidden="true" className="size-4" />
          Novo feriado
        </Button>
      </div>
      <section aria-label="Lista de feriados" className={classeMolduraRolavel}>
        {corpo}
      </section>
      <Modal aberto={novo} aoFechar={() => setNovo(false)} titulo="Novo feriado">
        {novo && (
          <FeriadoForm
            aoCancelar={() => setNovo(false)}
            aoCriado={(r) => {
              setNovo(false);
              if (r.conflitos.length > 0) setConflitos(r.conflitos);
              else toast.sucesso("Feriado cadastrado.");
            }}
          />
        )}
      </Modal>
      {conflitos && <FeriadoConflitosDialog conflitos={conflitos} aoFechar={() => setConflitos(null)} />}
      <ConfirmDialog
        aberto={!!removendo}
        titulo="Remover feriado"
        mensagem={`Remover "${removendo?.descricao}" (${formatarDataCivil(removendo?.data)})? A data volta a aceitar agendamentos.`}
        confirmar="Remover"
        aoConfirmar={() => void confirmarRemocao()}
        aoCancelar={() => setRemovendo(null)}
        carregando={inativar.isPending}
      >
        {erro && (
          <p role="alert" className="text-sm text-urgente">
            {erro}
          </p>
        )}
      </ConfirmDialog>
    </>
  );
}
