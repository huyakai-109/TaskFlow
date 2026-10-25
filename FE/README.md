# TaskFlow — Web client

React client for the Kanban TaskFlow board. The API it talks to is in [`../BE`](../BE);
the behaviour it implements is specified in [`../docs/features`](../docs/features).

## Stack

| | |
|---|---|
| React | 19 |
| Language | TypeScript (strict) |
| Build | Vite |
| Server state | TanStack Query |
| Routing | React Router |
| Styling | CSS Modules over design tokens (`src/styles/tokens.css`), light + dark |

Exact versions live in `package.json`.

## Commands

| Purpose | Command |
|---|---|
| Dev server | `npm run dev` → http://localhost:5173 |
| Typecheck | `npm run typecheck` |
| Lint | `npm run lint` |
| Build | `npm run build` |
| Preview a build | `npm run preview` |

No test runner is set up yet — adding one is a decision for the user, not a default.

## Configuration

`VITE_API_BASE_URL` points at the API (`.env.development` for local work, `.env.example`
as the template). Everything prefixed `VITE_` is inlined into the bundle and is therefore
public — no secrets.

The dev port is pinned to **5173** with `strictPort`, because the API allow-lists that
exact origin (`Cors:AllowedOrigins`). Letting Vite fall back to another port would break
every authenticated call with an opaque CORS error instead of failing loudly here.

## Layout

```
src/
  api/        client.ts (the only place that calls fetch), types.ts (envelope + task model)
  app/        application shell
  features/   one folder per feature; board/ so far
  styles/     tokens.css, global.css
  router.tsx  all routes
```

## State of play

The board page is a placeholder: it renders the five columns and fetches nothing.
**The F01 API contract is not agreed yet**, and building against a guessed shape is
what the contract-first rule in `../CLAUDE.md` exists to prevent. `src/api/types.ts`
carries the agreed task *model* from `f01-workflow.md`, not a wire format.

Once the contract lands, the board gains one paged query per column (50 rows; `Done`
20 ordered by `CompletedAt`), the four async states — loading, empty, error, success —
and drag-and-drop whose live drop targets come from each task's `allowedTransitions`.

Authentication is also pending: `setAccessToken` keeps the access token in memory only,
and `refreshAccessToken` deliberately throws until the API moves its refresh token to an
`httpOnly` cookie (see *Known follow-ups* in `../docs/ROADMAP.md`).
