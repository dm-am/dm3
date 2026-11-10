#!/usr/bin/env node
/**
 * Hook: when a subagent stops, format and lint the files it wrote.
 *
 * The list comes from the agent's own transcript, so parallel agents do not
 * answer for each other's files. Only per-file tools run here: eslint with
 * prettier on the client, dotnet format whitespace on the server. No type check,
 * build or tests: those share one tree with the other agents and belong to the
 * orchestrator's run.
 *
 * Silent on success. Exit 2 gives the findings to the agent as its next
 * instruction. It goes back once: stop_hook_active says it already has, and a
 * finding it cannot fix must not keep it running, so the second time the owner
 * gets a warning and the gates before the push are the barrier. A tool that did
 * not work (timeout, crash, not installed) says nothing about the agent's files,
 * so that goes to the owner too and the agent is left alone.
 */

const { execFileSync } = require('child_process');
const fs = require('fs');
const path = require('path');
const { resolveTarget, formatArgs } = require('./lint-edited-file');

const WRITERS = new Set(['Write', 'Edit']);
const TOOL_TIMEOUT_MS = 180_000;
const ESLINT_FOUND_PROBLEMS = 1;

/** Paths the agent wrote or edited, from the JSONL text of its transcript. */
function filesWrittenBy(transcript) {
  const files = new Set();
  for (const line of transcript.split('\n')) {
    if (!line.includes('"tool_use"')) continue;
    let record;
    try {
      record = JSON.parse(line);
    } catch {
      continue; // the agent's last line may still be half written
    }
    const content = record.message?.content;
    if (!Array.isArray(content)) continue;
    for (const block of content) {
      if (block.type === 'tool_use' && WRITERS.has(block.name) && block.input?.file_path) {
        files.add(block.input.file_path);
      }
    }
  }
  return [...files];
}

/** Which tool gets which file: client files per client directory, C# files relative to cwd. */
function planRuns(files, cwd) {
  const clients = new Map();
  const csharp = [];
  for (const file of files) {
    const target = resolveTarget(file);
    if (target) {
      clients.set(target.clientDir, [...(clients.get(target.clientDir) ?? []), target.relative]);
    } else if (/\.cs$/i.test(file)) {
      const relative = path.relative(cwd, file);
      if (!relative.startsWith('..') && !path.isAbsolute(relative)) csharp.push(relative);
    }
  }
  return { clients, csharp };
}

/** What the hook does with the findings. */
function decide(findings, alreadyReturned) {
  if (!findings) return 'pass';
  return alreadyReturned ? 'warn' : 'return';
}

/** The tool's exit status and what it printed. */
function run(args, cwd) {
  try {
    execFileSync(args[0], args.slice(1), { cwd, stdio: 'pipe', encoding: 'utf8', timeout: TOOL_TIMEOUT_MS });
    return { status: 0, output: '' };
  } catch (error) {
    return { status: error.status, output: [error.stdout, error.stderr].filter(Boolean).join('').trim() || error.message };
  }
}

/** Findings about the agent's files, apart from tools that did not work. */
function check({ clients, csharp }, cwd) {
  const findings = [];
  const brokenTools = [];
  for (const [clientDir, files] of clients) {
    // Called through node, not npx: npx.cmd does not start from child_process on Windows.
    const eslint = path.join(clientDir, 'node_modules', 'eslint', 'bin', 'eslint.js');
    // A worktree without packages has nothing to run this with.
    if (!fs.existsSync(eslint)) continue;
    const { status, output } = run([process.execPath, eslint, '--fix', '--no-warn-ignored', '--max-warnings', '0', ...formatArgs(clientDir), ...files], clientDir);
    if (status === ESLINT_FOUND_PROBLEMS) findings.push(output);
    else if (status !== 0) brokenTools.push(output);
  }
  if (csharp.length > 0) {
    const { status, output } = run(['dotnet', 'format', 'whitespace', '--folder', '--include', ...csharp], cwd);
    if (status !== 0) brokenTools.push(output);
  }
  return { findings: findings.join('\n\n'), brokenTools: brokenTools.join('\n\n') };
}

module.exports = { filesWrittenBy, planRuns, decide };

// As a hook it reads JSON on stdin; as a module it hands its parts to the test.
if (require.main === module) {
  const CHECK_DID_NOT_RUN = 'Проверка файлов агента не отработала:';
  let input;
  let files;
  try {
    input = JSON.parse(fs.readFileSync(0, 'utf8'));
    files = filesWrittenBy(fs.readFileSync(input.agent_transcript_path, 'utf8')).filter((file) => fs.existsSync(file));
  } catch (error) {
    console.log(JSON.stringify({ systemMessage: `${CHECK_DID_NOT_RUN}\n${error.message}` }));
    process.exit(0);
  }
  const { findings, brokenTools } = check(planRuns(files, input.cwd), input.cwd);

  const verdict = decide(findings, input.stop_hook_active === true);
  if (verdict === 'return') {
    console.error('The files you wrote do not pass format and lint. Fix every finding, then stop again:\n');
    console.error(findings);
    process.exit(2);
  }

  const warnings = [];
  if (verdict === 'warn') warnings.push(`Агент завершился, линт его файлов не прошел:\n${findings}`);
  if (brokenTools) warnings.push(`${CHECK_DID_NOT_RUN}\n${brokenTools}`);
  if (warnings.length > 0) console.log(JSON.stringify({ systemMessage: warnings.join('\n\n') }));
}
