#!/usr/bin/env node
/**
 * The hook's decisions, then the hook itself as a child process with a stub in
 * place of eslint: which files are the agent's, which tool gets each of them,
 * and what the agent and the owner are told.
 * Run: node .claude/hooks/lint-agent-files.test.js
 */

const { spawnSync } = require('child_process');
const fs = require('fs');
const os = require('os');
const path = require('path');
const { filesWrittenBy, planRuns, decide } = require('./lint-agent-files.js');

const toolUse = (name, input) => JSON.stringify({ type: 'assistant', message: { content: [{ type: 'tool_use', name, input }] } });

const transcript = [
  toolUse('Write', { file_path: 'src/DM.Web.Client/src/a.ts' }),
  toolUse('Edit', { file_path: 'src/DM.Domain.Game/B.cs' }),
  toolUse('Edit', { file_path: 'src/DM.Web.Client/src/a.ts' }),
  toolUse('Read', { file_path: 'src/DM.Web.Client/src/read-only.ts' }),
  JSON.stringify({ type: 'user', message: { content: [{ type: 'tool_result', content: '"tool_use" "file_path": "x.ts"' }] } }),
  '{"type":"assistant","message":{"content":[{"type":"tool_use","name":"Write","input":{"file_pa',
].join('\n');

const cwd = path.resolve('repo');
const inRepo = (relative) => path.join(cwd, relative);

const plan = planRuns(
  [
    'src/DM.Web.Client/src/a.ts',
    'D:/other/src/DM.Web.Client/src/b.vue',
    inRepo('src/DM.Domain.Game/B.cs'),
    path.resolve('elsewhere/C.cs'),
    'docs/conventions/CODE_STYLE.md',
  ],
  cwd,
);

const cases = [
  ['the agent wrote two files, once each', filesWrittenBy(transcript), ['src/DM.Web.Client/src/a.ts', 'src/DM.Domain.Game/B.cs']],
  ['a transcript without writes has no files', filesWrittenBy(toolUse('Read', { file_path: 'a.ts' })), []],
  ['client files go per client directory', [...plan.clients.entries()], [['src/DM.Web.Client', ['src/a.ts']], ['D:/other/src/DM.Web.Client', ['src/b.vue']]]],
  ['C# inside cwd goes relative, outside and other types do not go', plan.csharp, [path.join('src', 'DM.Domain.Game', 'B.cs')]],
  ['no findings pass', decide('', false), 'pass'],
  ['no findings pass after a return too', decide('', true), 'pass'],
  ['findings send the agent back', decide('error', false), 'return'],
  ['findings after a return warn and let it stop', decide('error', true), 'warn'],
];
if (process.platform === 'win32') {
  cases.push(['C# on another drive does not go', planRuns(['Z:/x/C.cs'], cwd).csharp, []]);
}

// The hook end to end: a tree of its own with a stub that records its arguments.
const root = fs.mkdtempSync(path.join(os.tmpdir(), 'lint-agent-files-'));
const client = path.join(root, 'src', 'DM.Web.Client');
const source = path.join(client, 'src', 'a.ts');
const csharpFile = path.join(root, 'B.cs');
const stub = path.join(client, 'node_modules', 'eslint', 'bin', 'eslint.js');
const argsFile = path.join(root, 'args.json');
const transcriptFile = path.join(root, 'agent.jsonl');
fs.mkdirSync(path.dirname(source), { recursive: true });
fs.mkdirSync(path.dirname(stub), { recursive: true });
fs.writeFileSync(source, '');
fs.writeFileSync(csharpFile, '');
fs.writeFileSync(
  stub,
  `require('fs').writeFileSync(process.env.STUB_ARGS, JSON.stringify(process.argv.slice(2)));
   console.log(process.env.STUB_OUTPUT || '');
   process.exit(Number(process.env.STUB_STATUS));`,
);

function hook(writes, { status = 0, output = '', active = false, env = {}, transcriptPath = transcriptFile } = {}) {
  fs.writeFileSync(transcriptFile, writes.map((file) => toolUse('Write', { file_path: file })).join('\n'));
  fs.rmSync(argsFile, { force: true });
  const result = spawnSync(process.execPath, [path.join(__dirname, 'lint-agent-files.js')], {
    input: JSON.stringify({ cwd: root, stop_hook_active: active, agent_transcript_path: transcriptPath }),
    encoding: 'utf8',
    env: { ...process.env, STUB_ARGS: argsFile, STUB_STATUS: String(status), STUB_OUTPUT: output, ...env },
  });
  const message = result.stdout.trim() ? JSON.parse(result.stdout).systemMessage : '';
  return { exit: result.status, toAgent: result.stderr.includes(output || 'stop again') ? 'told' : result.stderr.trim(), message };
}

const clean = hook([source]);
const eslintArgs = fs.existsSync(argsFile) ? JSON.parse(fs.readFileSync(argsFile, 'utf8')) : [];
cases.push(
  ['clean files are silent', clean, { exit: 0, toAgent: '', message: '' }],
  ['eslint gets fix, the warnings limit and the file', ['--fix', '--no-warn-ignored', '--max-warnings', '0', path.join('src', 'a.ts').split(path.sep).join('/')].every((arg) => eslintArgs.includes(arg)), true],
  ['a finding sends the agent back with the output', hook([source], { status: 1, output: 'finding-1' }), { exit: 2, toAgent: 'told', message: '' }],
);

const warned = hook([source], { status: 1, output: 'finding-2', active: true });
cases.push(['the second time the owner is warned and the agent goes', { exit: warned.exit, hasFinding: warned.message.includes('finding-2') }, { exit: 0, hasFinding: true }]);

const crashed = hook([source], { status: 2, output: 'config error' });
cases.push(['a crashed eslint is not put on the agent', { exit: crashed.exit, hasOutput: crashed.message.includes('config error') }, { exit: 0, hasOutput: true }]);

const noDotnet = hook([csharpFile], { env: { PATH: '' } });
cases.push(['a missing dotnet is not put on the agent', { exit: noDotnet.exit, warned: noDotnet.message.length > 0 }, { exit: 0, warned: true }]);

const noTranscript = hook([], { transcriptPath: path.join(root, 'missing.jsonl') });
cases.push(['a transcript it cannot read goes to the owner', { exit: noTranscript.exit, warned: noTranscript.message.length > 0 }, { exit: 0, warned: true }]);

cases.push(['a file the agent removed again is skipped', hook([path.join(client, 'src', 'gone.ts')], { status: 1, output: 'finding-3' }), { exit: 0, toAgent: '', message: '' }]);

fs.rmSync(root, { recursive: true, force: true });

let failures = 0;
for (const [why, actual, expected] of cases) {
  const ok = JSON.stringify(actual) === JSON.stringify(expected);
  console.log(`${ok ? 'ok ' : 'BAD'} ${why}${ok ? '' : ` :: got ${JSON.stringify(actual)}`}`);
  if (!ok) failures++;
}

console.log(`\ntotal ${cases.length}, failed ${failures}`);
process.exit(failures === 0 ? 0 : 1);
