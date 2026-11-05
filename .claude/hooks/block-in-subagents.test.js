#!/usr/bin/env node
/**
 * The hook both ways: inside an agent the main session's calls are refused and
 * the rest pass, and outside an agent everything passes.
 * Run: node .claude/hooks/block-in-subagents.test.js
 */

const { findRefusal } = require('./block-in-subagents');

const agent = (tool, input = {}) => ({ agent_id: 'a1', agent_type: 'general-purpose', tool_name: tool, tool_input: input });
const main = (tool, input = {}) => ({ tool_name: tool, tool_input: input });

// [hook input, whether it is refused, why this case]
const CASES = [
  [agent('mcp__Claude_Browser__navigate', { url: 'http://localhost:5173' }), true, 'the pane opens on the owner screen'],
  [agent('mcp__claude-in-chrome__computer', { action: 'screenshot' }), true, 'the owner Chrome'],
  [agent('Bash', { command: 'docker ps' }), true, 'docker as a command'],
  [agent('Bash', { command: 'cd docker && docker compose up -d' }), true, 'docker after a separator'],
  [agent('Bash', { command: String.raw`powershell -NoProfile -File 'D:\Projects\Web\dm3\scripts\dm.ps1' reset` }), true, 'dm.ps1 by a PowerShell host'],
  [agent('PowerShell', { command: String.raw`& 'D:\Projects\Web\dm3\scripts\dm.ps1' e2e` }), true, 'dm.ps1 with the call operator'],
  [agent('PowerShell', { command: String.raw`.\scripts\dm.ps1 seed` }), true, 'dm.ps1 by its path'],

  [agent('Bash', { command: 'cat scripts/dm.ps1' }), false, 'reading the script'],
  [agent('Bash', { command: 'ls docker' }), false, 'the folder named docker'],
  [agent('Bash', { command: 'grep -rn "docker compose" docs' }), false, 'a search for the words'],
  [agent('Bash', { command: 'git commit -F - <<EOF\nwhy docker is banned\nEOF' }), false, 'the words in a heredoc are data'],
  [agent('Read', { file_path: 'docker/.env.example' }), false, 'not a command'],

  [main('mcp__Claude_Browser__navigate', { url: 'http://localhost:5173' }), false, 'the main session keeps the pane'],
  [main('Bash', { command: 'docker ps' }), false, 'the main session restarts the stand'],
];

let bad = 0;
for (const [input, shouldRefuse, why] of CASES) {
  const refused = findRefusal(input) !== null;
  const ok = refused === shouldRefuse;
  if (!ok) bad++;
  const what = input.tool_input.command ?? input.tool_input.url ?? input.tool_input.action ?? input.tool_input.file_path;
  console.log(
    `${ok ? 'ok   ' : 'ПЛОХО'} ${shouldRefuse ? 'отказ  ' : 'пропуск'} :: ${input.agent_id ? 'агент' : 'главная'} ${input.tool_name} ${String(what).replace(/\n/g, '\n')}` +
      (ok ? `   (${why})` : `   ПОЛУЧЕНО ${refused ? 'отказ' : 'пропуск'}, ${why}`),
  );
}

console.log(`\nвсего ${CASES.length}, расхождений ${bad}`);
process.exit(bad === 0 ? 0 : 1);
