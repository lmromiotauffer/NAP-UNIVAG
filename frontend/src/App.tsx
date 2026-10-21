import { QueryClientProvider } from "@tanstack/react-query";
import { Suspense } from "react";
import { RouterProvider } from "react-router";
import { SessaoProvider } from "./auth/SessaoProvider";
import { Carregando } from "./components/ui/Carregando";
import { Skeleton } from "./components/ui/Skeleton";
import { ToastProvider } from "./components/ui/Toast";
import { queryClient } from "./queryClient";
import { router } from "./router";

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <SessaoProvider>
        <ToastProvider>
          {/* Páginas carregam sob demanda (router.tsx); dentro do AppShell há outro Suspense que preserva o menu. */}
          <Suspense
            fallback={
              <Carregando rotulo="Carregando página" className="flex min-h-dvh flex-col gap-4 bg-fundo p-6">
                <Skeleton className="h-8 w-72 max-w-full" />
                <Skeleton className="h-64 w-full" />
              </Carregando>
            }
          >
            <RouterProvider router={router} />
          </Suspense>
        </ToastProvider>
      </SessaoProvider>
    </QueryClientProvider>
  );
}
