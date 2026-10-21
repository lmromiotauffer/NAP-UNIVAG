import type { Ref, SelectHTMLAttributes } from "react";
import { cn } from "../../lib/cn";
import { bordaCampo, classeCampo } from "./Input";

export function Select({ invalid, className, ref, ...resto }: SelectHTMLAttributes<HTMLSelectElement> & { invalid?: boolean; ref?: Ref<HTMLSelectElement> }) {
  return <select ref={ref} aria-invalid={invalid || undefined} className={cn(classeCampo, bordaCampo(invalid), "max-w-full", className)} {...resto} />;
}
