# Liyodev Assistant MCP server

Lets an assistant that speaks MCP (Claude Desktop, Claude Code, other clients) read and apply changes
to the contexts of the Liyodev Assistant while Unity is open.

1. In Unity: **Liyodev ▸ Asistente: herramientas externas (puerto local)**. The editor listens on
   `http://127.0.0.1:47822`, only on this computer.
2. Add the server to your client (Node 18 or newer, no install needed):

```json
{ "mcpServers": { "liyodev-assistant": { "command": "node", "args": ["<project>/Packages/com.liyodev.assistant/Tools/mcp/server.mjs"] } } }
```

Tools: `list_contexts`, `context_targets`, `context_select`, `context_manual`, `context_state`,
`apply_ops`.

## Cinematics

1. `list_contexts` → `cinematics`.
2. `context_targets` with `context: "cinematics"` → the cinematics in the project.
3. `context_select` with `context: "cinematics"` and `id: "Cap3_03_ElCarro"` → opens it in the Director.
4. `context_manual` and `context_state` → the guion grammar, the numbered guion (`L1`, `L2`...), the
   last bake report, the pending AI tasks and the animation states.
5. `apply_ops` with `context: "cinematics"` and `ops: [...]` → validates, bakes in memory, shows the
   diff and only then writes the guion and closes the tasks. If the bake fails, nothing is written.

`apply_ops` writes and bakes. Read `context_state` first and use `match` on the lines you change.

The narrative graph keeps its own MCP server in `Packages/com.liyodev.narrativegraph/Tools/mcp` for
graph editing without the graph window open. When the graph editor is open, its context also appears
here.
