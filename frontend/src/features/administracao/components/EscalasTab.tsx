import { zodResolver } from "@hookform/resolvers/zod";
import { CalendarClock, Plus } from "lucide-react";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert } from "../../../components/ui/Alert";
import { Button } from "../../../components/ui/Button";
import { EmptyState } from "../../../components/ui/EmptyState";
import { ErrorState } from "../../../components/ui/ErrorState";
import { Field } from "../../../components/ui/Field";
import { classeFiltro, Input } from "../../../components/ui/Input";
import { Modal } from "../../../components/ui/Modal";
import { Select } from "../../../components/ui/Select";
import { Skeleton } from "../../../components/ui/Skeleton";
import { classeMolduraRolavel, Table, th, td } from "../../../components/ui/Table";
import { useToast } from "../../../components/ui/Toast";
import { tratarErro } from "../../../lib/api";
import { cn } from "../../../lib/cn";
import { DIA, formatarDataCivil, hojeCivil } from "../../../lib/format";
import type { EscalaProfissional } from "../../../lib/types";
import { useProfissionais } from "../../compartilhado/catalogos";
import { useEncerrarVigenciaEscala, useListaEscalas } from "../hooks/useAdministracao";
import { AutorizacoesForaEscala } from "./AutorizacoesForaEscala";
import { EscalaForm } from "./EscalaForm";
import { previaEscala } from "./previaEscala";


const esquemaVigencia = z.object({ vigenciaFim: z.string().regex(DIA, "Informe o último dia da vigência.") });
type DadosVigencia = z.infer<typeof esquemaVigencia>;

/** Encerrar a vigência é editar o fim dela: a escala deixa de valer depois da data e o profissional pode ganhar uma nova. */
function EncerrarVigenciaDialog({ escala, aoFechar, aoConcluir }: { escala: EscalaProfissional; aoFechar: () => void; aoConcluir: () => void }) {
  // Versão do momento em que abriu: um refetch durante a digitação não a troca.
  const [versao] = useState(escala.versao);
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const encerrar = useEncerrarVigenciaEscala();
  const hoje = hojeCivil();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<DadosVigencia>({ resolver: zodResolver(esquemaVigencia), defaultValues: { vigenciaFim: hoje > escala.vigenciaInicio ? hoje : escala.vigenciaInicio } });

  async function enviar(body: DadosVigencia) {
    setErroGeral(null);
    if (body.vigenciaFim < escala.vigenciaInicio) {
      setError("vigenciaFim", { message: "O fim da vigência não pode ser anterior ao início." });
      return;
    }
    try {
      await encerrar.mutateAsync({ id: escala.id, body, versao });
      aoConcluir();
    } catch (e) {
      setErroGeral(tratarErro(e, ["vigenciaFim"] as const, (campo, message) => setError(campo, { message })));
    }
  }

  return (
    <Modal aberto aoFechar={aoFechar} titulo="Encerrar vigência">
      <form noValidate onSubmit={handleSubmit(enviar)} className="flex flex-col gap-4">
        <p className="m-0 text-sm">
          Escala de {escala.profissional}, em vigor desde {formatarDataCivil(escala.vigenciaInicio)}. Compromissos já agendados não mudam.
        </p>
        {erroGeral && <Alert variante="urgente" className="animate-surgir">{erroGeral}</Alert>}
        <Field rotulo="Último dia de vigência" erro={errors.vigenciaFim?.message} obrigatorio>
          <Input type="date" min={escala.vigenciaInicio} {...register("vigenciaFim")} />
        </Field>
        <div className="flex flex-wrap justify-end gap-2">
          <Button variante="secundaria" onClick={aoFechar} disabled={isSubmitting}>
            Cancelar
          </Button>
          <Button type="submit" carregando={isSubmitting}>
            Encerrar vigência
          </Button>
        </div>
      </form>
    </Modal>
  );
}

export function EscalasTab() {
  const toast = useToast();
  const hoje = hojeCivil();
  const [profissionalId, setProfissionalId] = useState("");
  const profissionais = useProfissionais();
  const escalas = useListaEscalas(profissionalId || undefined);
  const [nova, setNova] = useState(false);
  // O snapshot da escala no momento em que o diálogo abriu é a versão do If-Match.
  const [encerrando, setEncerrando] = useState<EscalaProfissional | null>(null);

  let corpo;
  if (escalas.isError) {
    corpo = (
      <div className="p-4">
        <ErrorState erro={escalas.error} aoTentarNovamente={() => void escalas.refetch()} />
      </div>
    );
  } else if (escalas.data?.length === 0 && !escalas.isPlaceholderData) {
    corpo = (
      <EmptyState
        icone={<CalendarClock />}
        titulo={profissionalId ? "Nenhuma escala para este profissional" : "Nenhuma escala cadastrada"}
        descricao="Sem escala própria, o profissional segue o padrão do sistema: segunda a sexta, das 07h às 17h, com sexta sinalizada como sem atendimento."
        acao={
          !!profissionalId && (
            <Button variante="secundaria" tamanho="sm" onClick={() => setProfissionalId("")}>
              Limpar filtros
            </Button>
          )
        }
      />
    );
  } else {
    corpo = (
      <Table caption="Escalas dos profissionais" aria-busy={escalas.isPending || escalas.isPlaceholderData} className={cn(escalas.isPlaceholderData && "opacity-60")}>
        <thead>
          <tr className="border-b border-borda">
            <th scope="col" className={th}>Profissional</th>
            <th scope="col" className={th}>Vigência</th>
            <th scope="col" className={th}>Horários</th>
            <th scope="col" className={th}>
              <span className="sr-only">Ações</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {escalas.data
            ? escalas.data.map((e) => (
                <tr key={e.id} className="border-b border-borda text-corpo">
                  <td className={td}>
                    <span className="font-semibold">{e.profissional}</span>
                  </td>
                  <td className={cn(td, "tabular-nums")}>
                    {e.vigenciaFim ? `${formatarDataCivil(e.vigenciaInicio)} a ${formatarDataCivil(e.vigenciaFim)}` : `Desde ${formatarDataCivil(e.vigenciaInicio)}, sem término`}
                  </td>
                  <td className={td}>{previaEscala(e.faixas)}</td>
                  <td className={`${td} text-right`}>
                    {(e.vigenciaFim == null || e.vigenciaFim >= hoje) && (
                      <Button tamanho="sm" variante="secundaria" aria-label={`Encerrar vigência da escala de ${e.profissional}`} onClick={() => setEncerrando(e)}>
                        Encerrar vigência
                      </Button>
                    )}
                  </td>
                </tr>
              ))
            : Array.from({ length: 4 }, (_, i) => (
                <tr key={i} aria-hidden="true" className="border-b border-borda">
                  <td className={td}>
                    <Skeleton className="h-4 w-40" />
                  </td>
                  <td className={td}>
                    <Skeleton className="h-4 w-32" />
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
      {/* Mesma barra das outras listagens (Usuários, Auditoria): pílula com o rótulo dentro, visível só para leitores de tela. */}
      <div className="flex flex-wrap items-center justify-between gap-2">
        {/* basis-full no celular: o filtro ocupa a linha inteira e o botão desce; a partir de `sm` dividem a linha. */}
        <div role="search" aria-label="Filtrar escalas" className="flex min-w-0 grow basis-full flex-wrap items-center gap-2 sm:basis-0">
          <label htmlFor="escalas-profissional" className="sr-only">
            Profissional
          </label>
          <Select id="escalas-profissional" className={classeFiltro} value={profissionalId} onChange={(e) => setProfissionalId(e.target.value)}>
            <option value="">Profissional: todos</option>
            {profissionais.data?.map((p) => (
              <option key={p.id} value={p.id}>
                {p.nome}
              </option>
            ))}
          </Select>
        </div>
        <Button onClick={() => setNova(true)} className="rounded-full">
          <Plus aria-hidden="true" className="size-4" />
          Nova escala
        </Button>
      </div>
      <section aria-label="Lista de escalas" className={classeMolduraRolavel}>
        {corpo}
      </section>
      <Modal aberto={nova} aoFechar={() => setNova(false)} titulo="Nova escala">
        {nova && (
          <EscalaForm
            aoCancelar={() => setNova(false)}
            aoConcluir={() => {
              toast.sucesso("Escala cadastrada.");
              setNova(false);
            }}
          />
        )}
      </Modal>
      {encerrando && (
        <EncerrarVigenciaDialog
          escala={encerrando}
          aoFechar={() => setEncerrando(null)}
          aoConcluir={() => {
            toast.sucesso("Vigência encerrada.");
            setEncerrando(null);
          }}
        />
      )}
      <AutorizacoesForaEscala />
    </>
  );
}
