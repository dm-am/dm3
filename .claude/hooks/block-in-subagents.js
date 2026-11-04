#!/usr/bin/env node
/**
 * Hook: what only the main session may do. Inside a subagent the hook input
 * carries agent_id, and there the hook refuses the browser pane, the owner's
 * Chrome and a restart of the stand: the pane opens on the owner's screen, the
 * Chrome holds the owner's sign-ins, and docker or dm.ps1 pulls the stand from
 * under every other run. An agent measures a page with headless Playwright.
 * The main session's input has no agent_id, so its calls pass.
 * Exit 2 = blocked, stderr goes to the agent.
 */

const { commandSegments, stripHeredocBodies } = require('./block-dangerous-git');

const BROWSER_TOOL = /^mcp__(Claude_Browser|claude-in-chrome)__/;

// docker as a command, and dm.ps1 run by a PowerShell host, with & or by its path.
const STAND_COMMANDS = [
  /^(sudo\s+)?docker(-compose)?(\.exe)?(\s|$)/i,
  /^(pwsh|powershell)(\.exe)?\s.*\bdm\.ps1\b/i,
  /^&?\s*["']?[^\s"']*dm\.ps1\b/i,
];

/** Why this call belongs to the main session, or null when an agent may make it. */
function findRefusal(data) {
  if (!data.agent_id) return null;
  if (BROWSER_TOOL.test(data.tool_name || '')) {
    return 'браузерный пейн и Chrome владельца только у главной сессии, агент меряет страницу headless Playwright';
  }
  const command = data.tool_input?.command;
  if (typeof command !== 'string') return null;
  const segments = commandSegments(stripHeredocBodies(command));
  if (segments.some((segment) => STAND_COMMANDS.some((pattern) => pattern.test(segment)))) {
    return 'docker и dm.ps1 только у главной сессии: перезапуск стенда сносит прогоны остальных';
  }
  return null;
}

module.exports = { findRefusal };

// As a hook it reads JSON on stdin; as a module it hands findRefusal to the test.
if (require.main === module) {
  let input = '';

  process.stdin.setEncoding('utf8');
  process.stdin.on('data', (chunk) => (input += chunk));
  process.stdin.on('end', () => {
    try {
      const refusal = findRefusal(JSON.parse(input));
      if (refusal) {
        console.error(`BLOCKED: ${refusal}.`);
        console.error('Нужное от главной сессии вернуть ей словами в отчете.');
        process.exit(2);
      }
      process.exit(0);
    } catch {
      // Unparsable input: the hook cannot tell who calls, so it lets the call
      // through and says so, as block-dangerous-git does.
      console.error('block-in-subagents: не удалось разобрать ввод хука, вызов пропущен');
      process.exit(0);
    }
  });
}
