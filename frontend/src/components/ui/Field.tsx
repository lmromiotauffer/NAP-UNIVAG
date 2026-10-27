import { cloneElement, useId, type ReactElement } from "react";

type CampoFilho = { id?: string; "aria-describedby"?: string; invalid?: boolean; required?: boolean };

export function Field({
  rotulo,
  erro,
  dica,
  obrigatorio,
  children,
}: {
  rotulo: string;
  erro?: string;
  dica?: string;
  obrigatorio?: boolean;
  children: ReactElement<CampoFilho>;
}) {
  const base = useId();
  const id = children.props.id ?? base;
  const idDica = `${id}-dica`;
  const idErro = `${id}-erro`;
  const descrito = [children.props["aria-describedby"], dica && idDica, erro && idErro].filter(Boolean).join(" ") || undefined;
  return (
    <div className="flex flex-col gap-1">
      <label htmlFor={id} className="text-sm font-medium text-texto">
        {rotulo}
        {obrigatorio && (
          <span className="ml-0.5 text-urgente" aria-hidden="true">
            *
          </span>
        )}
      </label>
      {cloneElement(children, { id, "aria-describedby": descrito, invalid: !!erro, ...(obrigatorio ? { required: true } : {}) })}
      {dica && (
        <p id={idDica} className="text-xs text-suave">
          {dica}
        </p>
      )}
      {erro && (
        <p id={idErro} role="alert" className="animate-surgir text-xs text-urgente">
          {erro}
        </p>
      )}
    </div>
  );
}
