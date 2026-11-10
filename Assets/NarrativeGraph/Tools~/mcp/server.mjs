#!/usr/bin/env node
// MCP server for Narrative Graph. It talks to the Unity editor through the local bridge
// (Tools > Narrative Graph > Permitir herramientas externas). No dependencies: Node 18+.
//
// Claude Desktop / Claude Code config:
//   { "mcpServers": { "narrative-graph": { "command": "node", "args": ["<path>/server.mjs"] } } }

import { createInterface } from "node:readline";

const BRIDGE = process.env.NARRATIVE_GRAPH_BRIDGE ?? "http://127.0.0.1:47821";

const tools = [
  {
    name: "list_graphs",
    description: "Names of the narrative graphs (chapters) in the open Unity project.",
    inputSchema: { type: "object", properties: {} },
  },
  {
    name: "read_chapter",
    description: "A chapter as text: sections, numbered steps, their fields and where each way out leads.",
    inputSchema: { type: "object", properties: { graph: { type: "string" } }, required: ["graph"] },
  },
  {
    name: "step_manual",
    description: "Every step type available, with its fields and ways out. Read it before adding steps.",
    inputSchema: { type: "object", properties: {} },
  },
  {
    name: "edit_chapter",
    description:
      "Applies several changes at once, or none if one is wrong. Steps are named by number ('2.3'), id, or the id given to a step added in the same batch. " +
      "Operations: add {id, type, title, after, port, fields:[{name,value}]}, set {step, fields}, connect {from, to, port}, disconnect {from, port}, section {step, value}, delete {step}.",
    inputSchema: {
      type: "object",
      properties: {
        graph: { type: "string" },
        ops: { type: "array", items: { type: "object" } },
      },
      required: ["graph", "ops"],
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
    case "list_graphs": return bridge("/graphs");
    case "read_chapter": return bridge("/chapter?graph=" + encodeURIComponent(args.graph));
    case "step_manual": return bridge("/manual");
    case "edit_chapter":
      return bridge("/edit?graph=" + encodeURIComponent(args.graph), JSON.stringify({ ops: args.ops }));
    default: return { ok: false, text: "Unknown tool " + name };
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
      serverInfo: { name: "narrative-graph", version: "0.1.0" },
    } });
  } else if (method === "tools/list") {
    send({ jsonrpc: "2.0", id, result: { tools } });
  } else if (method === "tools/call") {
    try {
      const r = await call(params.name, params.arguments ?? {});
      send({ jsonrpc: "2.0", id, result: { content: [{ type: "text", text: r.text }], isError: !r.ok } });
    } catch (e) {
      send({ jsonrpc: "2.0", id, result: {
        content: [{ type: "text", text: "Unity is not reachable. Open the project and turn on Tools > Narrative Graph > Permitir herramientas externas. (" + e.message + ")" }],
        isError: true } });
    }
  } else if (id !== undefined) {
    send({ jsonrpc: "2.0", id, error: { code: -32601, message: "Method not found" } });
  }
});
