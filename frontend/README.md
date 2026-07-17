# Carango — Frontend

Frontend da Plataforma de E-commerce de Veículos ("Carango"). React + TypeScript + Vite, consumindo a API .NET em `../backend/Api` via HTTP (nenhuma lógica de negócio vive aqui — ver `ARCHITECTURE-SPINE.md § AD-1`).

Tokens de design (`DESIGN.md`) estão em `src/theme/tokens.css`, importados globalmente.

## Rodando localmente

```bash
npm install
npm run dev      # servidor de desenvolvimento
npm run build    # build de produção
```

Requer Node ≥20.19 ou ≥22.12 (requisito do Vite 8.x).

Para rodar a stack completa, veja também `../backend/Api` (API .NET) — nenhuma integração entre os dois está configurada ainda (fora de escopo da Story 1.1; ver `deferred-work.md`).
