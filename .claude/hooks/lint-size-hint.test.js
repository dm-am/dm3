#!/usr/bin/env node
/**
 * Both lint hooks with the real eslint and the client's real formatter, on a
 * client of their own: a size finding still sends the agent back with the exit
 * code it always did, and the hint on how to cut reaches the agent with it.
 * Run: node .claude/hooks/lint-size-hint.test.js
 */

const { spawnSync } = require('child_process');
const fs = require('fs');
const os = require('os');
const path = require('path');

const REAL_CLIENT = path.resolve(__dirname, '..', '..', 'src', 'DM.Web.Client');
const HINT = 'cut by meaning, each part named after what it does, never by size alone';

const root = fs.mkdtempSync(path.join(os.tmpdir(), 'lint-size-hint-'));
const client = path.join(root, 'src', 'DM.Web.Client');
fs.mkdirSync(path.join(client, 'src'), { recursive: true });
// A junction needs no rights on Windows, and the packages are the real ones.
fs.symlinkSync(path.join(REAL_CLIENT, 'node_modules'), path.join(client, 'node_modules'), 'junction');
fs.copyFileSync(path.join(REAL_CLIENT, 'lint-formatter.mjs'), path.join(client, 'lint-formatter.mjs'));
fs.writeFileSync(path.join(client, 'package.json'), '{ "type": "module" }\n');
fs.writeFileSync(
  path.join(client, 'eslint.config.js'),
  [
    'import vue from "eslint-plugin-vue";',
    'export default [',
    '  { rules: { "max-depth": ["error", 1], "no-debugger": "error" } },',
    '  ...vue.configs["flat/base"],',
    '  { files: ["**/*.vue"], rules: { "vue/max-lines-per-block": ["error", { template: 1 }] } },',
    '];',
    '',
  ].join('\n'),
);

const write = (name, code) => {
  const file = path.join(client, 'src', name);
  fs.writeFileSync(file, code);
  return file;
};
const deep = write('deep.js', 'export function f(a) {\n  if (a) {\n    if (a > 1) return 1;\n  }\n  return 0;\n}\n');
const other = write('other.js', 'debugger;\nexport default 1;\n');
const clean = write('clean.js', 'export default 1;\n');
const block = write('block.vue', '<template>\n  <p>\n    a\n  </p>\n</template>\n');

const FINDING = /max-depth|no-debugger|vue\/max-lines-per-block/;

function edited(file) {
  const result = spawnSync(process.execPath, [path.join(__dirname, 'lint-edited-file.js')], {
    input: JSON.stringify({ tool_input: { file_path: file } }),
    encoding: 'utf8',
  });
  return { exit: result.status, hint: result.stderr.includes(HINT), finding: FINDING.test(result.stderr) };
}

function agent(file) {
  const transcript = path.join(root, 'agent.jsonl');
  fs.writeFileSync(transcript, JSON.stringify({ type: 'assistant', message: { content: [{ type: 'tool_use', name: 'Write', input: { file_path: file } }] } }));
  const result = spawnSync(process.execPath, [path.join(__dirname, 'lint-agent-files.js')], {
    input: JSON.stringify({ cwd: root, stop_hook_active: false, agent_transcript_path: transcript }),
    encoding: 'utf8',
  });
  return { exit: result.status, hint: result.stderr.includes(HINT), finding: FINDING.test(result.stderr) };
}

const cases = [
  ['edit: a size finding goes back with exit 2 and the hint', edited(deep), { exit: 2, hint: true, finding: true }],
  ['edit: another finding goes back without the hint', edited(other), { exit: 2, hint: false, finding: true }],
  ['edit: a clean file is silent', edited(clean), { exit: 0, hint: false, finding: false }],
  ['edit: a block-size finding of a plugin rule goes back with the hint', edited(block), { exit: 2, hint: true, finding: true }],
  ['stop: a size finding sends the agent back with the hint', agent(deep), { exit: 2, hint: true, finding: true }],
  ['stop: another finding sends it back without the hint', agent(other), { exit: 2, hint: false, finding: true }],
  ['stop: a clean file is silent', agent(clean), { exit: 0, hint: false, finding: false }],
  ['stop: a block-size finding of a plugin rule sends it back with the hint', agent(block), { exit: 2, hint: true, finding: true }],
];

// The formatter adds lines and changes none: without them the text is the default one.
const eslint = path.join(client, 'node_modules', 'eslint', 'bin', 'eslint.js');
const lint = (...args) => spawnSync(process.execPath, [eslint, 'src/deep.js', ...args], { cwd: client, encoding: 'utf8' });
const stylish = lint();
const hinted = lint('--format', './lint-formatter.mjs');
const withoutHints = hinted.stdout.split('\n').filter((line) => !line.includes(HINT)).join('\n');
cases.push(['the formatter prints the default output plus the hint lines', { same: withoutHints === stylish.stdout, exit: hinted.status, hints: hinted.stdout.split(HINT).length - 1 }, { same: true, exit: 1, hints: 1 }]);

fs.rmSync(path.join(client, 'node_modules'));
fs.rmSync(root, { recursive: true, force: true });

let failures = 0;
for (const [why, actual, expected] of cases) {
  const ok = JSON.stringify(actual) === JSON.stringify(expected);
  console.log(`${ok ? 'ok ' : 'BAD'} ${why}${ok ? '' : ` :: got ${JSON.stringify(actual)}`}`);
  if (!ok) failures++;
}

console.log(`\ntotal ${cases.length}, failed ${failures}`);
process.exit(failures === 0 ? 0 : 1);
