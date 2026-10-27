import type { Ref, TextareaHTMLAttributes } from "react";
import { cn } from "../../lib/cn";
import { bordaCampo, classeCampo } from "./Input";

export function Textarea({ invalid, className, ref, ...resto }: TextareaHTMLAttributes<HTMLTextAreaElement> & { invalid?: boolean; ref?: Ref<HTMLTextAreaElement> }) {
  return <textarea ref={ref} aria-invalid={invalid || undefined} className={cn(classeCampo, bordaCampo(invalid), "min-h-20 resize-y", className)} {...resto} />;
}
