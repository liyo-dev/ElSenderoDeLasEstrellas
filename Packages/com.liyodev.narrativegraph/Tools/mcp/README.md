# Narrative Graph MCP server

Lets an assistant that speaks MCP (Claude Desktop, Claude Code, other clients) read and edit the
graphs of the Unity project that is open.

1. In Unity: Tools › Narrative Graph › Permitir herramientas externas (puerto local).
   The editor listens on http://127.0.0.1:47821, only on this computer.
2. Add the server to your client (Node 18 or newer, no install needed):

```json
{ "mcpServers": { "narrative-graph": { "command": "node", "args": ["<project>/Assets/NarrativeGraph/Tools/mcp/server.mjs"] } } }
```

Tools: `list_graphs`, `read_chapter`, `step_manual`, `edit_chapter`. Edits are applied all
together or not at all, and can be undone in Unity with Ctrl+Z.
