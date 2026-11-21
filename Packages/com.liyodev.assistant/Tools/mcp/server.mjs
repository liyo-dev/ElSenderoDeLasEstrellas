#!/usr/bin/env node
// MCP server for Liyodev Assistant. It talks to the Unity editor through the local bridge
// (Liyodev > Asistente: herramientas externas). No dependencies: Node 18+.
//
// Claude Desktop / Claude Code config:
//   { "mcpServers": { "liyodev-assistant": { "command": "node", "args": ["<path>/server.mjs"] } } }

import { createInterface } from "node:readline";

const BRIDGE = process.env.LIYODEV_ASSISTANT_BRIDGE ?? "http://127.0.0.1:47822";

const tools = [
  {
    name: "list_contexts",
    description: "Assistant contexts available in the open Unity project (for example cinematics and narrative-graph).",
    inputSchema: { type: "object", properties: {} },
  },
  {
    name: "context_targets",
    description: "Selectable targets of a context. For cinematics, the list of cinematics in the project.",
    inputSchema: { type: "object", properties: { context: { type: "string" } }, required: ["context"] },
  },
  {
    name: "context_select",
    description: "Selects a target by id. For cinematics, opens it in the Director and makes it the active one.",
    inputSchema: {
      type: "object",
      properties: { context: { type: "string" }, id: { type: "string" } },
      required: ["context", "id"],
    },
  },
  {
    name: "context_manual",
    description: "The manual of the context. For cinematics, the guion grammar, camera options, effects and rules. Read it before writing ops.",
    inputSchema: { type: "object", properties: { context: { type: "string" } }, required: ["context"] },
  },
  {
    name: "context_state",
    description:
      "The current state of the selected target. For cinematics: the numbered guion (L1, L2...), the last bake report, " +
      "the pending AI tasks and the animation states of each actor.",
    inputSchema: { type: "object", properties: { context: { type: "string" } }, required: ["context"] },
  },
  {
    name: "apply_ops",
    description:
      "Applies a batch of operations to the selected target. It validates and bakes first; if the bake fails, nothing is written. " +
      "For cinematics: set_options {line, match, options:[{name,value}]}, replace_line {line, match, text}, " +
      "insert_after {line|anchor, text}, insert_before {line|anchor, text}, delete_line {line, match}, " +
      "resolve_task {task:\"P06\"|\"all\"}. Lines are the L... from context_state.",
    inputSchema: {
      type: "object",
      properties: {
        context: { type: "string" },
        ops: { type: "array", items: { type: "object" } },
      },
      required: ["context", "ops"],
    },
  },
];

async function bridge(path, body) {
  const res = await fetch(BRIDGE + path, body === undefined ? {} : { method: "POST", body });
  const text = await res.text();
  return { ok: res.ok, text };
}

async function call(name, args) {
  switch (name) {
    case "list_contexts":
      return bridge("/assistant/contexts");
    case "context_targets":
      return bridge("/assistant/targets?context=" + encodeURIComponent(args.context));
    case "context_select":
      return bridge("/assistant/select?context=" + encodeURIComponent(args.context),
        JSON.stringify({ id: args.id }));
    case "context_manual":
      return bridge("/assistant/manual?context=" + encodeURIComponent(args.context));
    case "context_state":
      return bridge("/assistant/state?context=" + encodeURIComponent(args.context));
    case "apply_ops":
      return bridge("/assistant/apply?context=" + encodeURIComponent(args.context),
        JSON.stringify({ ops: args.ops }));
    default:
      return { ok: false, text: "Unknown tool " + name };
  }
}

function send(msg) { process.stdout.write(JSON.stringify(msg) + "\n"); }

const rl = createInterface({ input: process.stdin });
rl.on("line", async (line) => {
  let req;
  try { req = JSON.parse(line); } catch { return; }
  const { id, method, params } = req;
  if (method === "initialize") {
    send({ jsonrpc: "2.0", id, result: {
      protocolVersion: params?.protocolVersion ?? "2025-06-18",
      capabilities: { tools: {} },
      serverInfo: { name: "liyodev-assistant", version: "0.1.0" },
    } });
  } else if (method === "tools/list") {
    send({ jsonrpc: "2.0", id, result: { tools } });
  } else if (method === "tools/call") {
    try {
      const r = await call(params.name, params.arguments ?? {});
      send({ jsonrpc: "2.0", id, result: { content: [{ type: "text", text: r.text }], isError: !r.ok } });
    } catch (e) {
      send({ jsonrpc: "2.0", id, result: {
        content: [{ type: "text", text: "Unity is not reachable. Open the project and turn on Liyodev > Asistente: herramientas externas (puerto local). (" + e.message + ")" }],
        isError: true } });
    }
  } else if (id !== undefined) {
    send({ jsonrpc: "2.0", id, error: { code: -32601, message: "Method not found" } });
  }
});
