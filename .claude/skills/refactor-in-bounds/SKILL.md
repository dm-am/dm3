---
name: refactor-in-bounds
description: "Refactoring over the list of files the task gives, with no change of behaviour or look, in small steps, each proven by a before and after comparison. For moves, cleanup, removal of the unused, and merging modules once the task has decided what merges with what and which interface stays (рефакторинг в рамках, перенос, чистка, удаление неиспользуемого, слияние модулей)."
user-invocable: true
argument-hint: "<list of files and the plan item>"
---

# Refactoring in bounds

## The rules the order stands on

From [PROCESS.md](../../../docs/conventions/PROCESS.md): [the verdict of a gate is its exit code](../../../docs/conventions/PROCESS.md#вердикт-гейта-по-коду-возврата), [a test must fail on broken code](../../../docs/conventions/PROCESS.md#тест-обязан-падать-на-сломанном-коде), runs are kept apart in time ([zones, runs, commits](../../../docs/conventions/PROCESS.md#зоны-прогоны-коммиты)). Git rollbacks are forbidden to an agent ([CLAUDE.md](../../CLAUDE.md)): HEAD stays the start of the task, and the base and the rollback of a step come from a copy of its files (step 3).

## Order

1. Write down the bounds: the files of the task and what stays as it was (the look, the server's answers, the behaviour). A file outside the list is not edited, what it needs goes to the report as a line "file:line, what". If a move breaks a path a gate register or a test reads the file by, and they are not on the list, the step is not done.
2. If the task assigns the stand, take its base before the first edit: `node scripts/visual/visual.mjs snap <label> --rev HEAD` and `node scripts/visual/visual.mjs api-snap <label> --rev HEAD`. The tool measures the stand already running and rebuilds nothing in it. When a snapshot is comparable is described in [scripts/visual/README.md](../../../scripts/visual/README.md), read before the first snapshot.
3. Before each step the files it touches are copied to the scratchpad with the layout of the repository, from the root in Git Bash: `cp --parents <paths> <scratchpad>/<step>/`. The copy is the rollback of the step and the base of its token comparison, and the comparison tool keeps the build before the step itself (step 5.3). A step that failed is not fixed with new edits on top: it is rolled back (`cp -r <scratchpad>/<step>/. .` from the root, files the step created are deleted) and split into smaller steps.
4. Steps are small, one kind of edit per step. A move and a cleanup are separate steps: a move does not rewrite the code, so the comparison shows a clean move, and the cleanup in the next step shows its own diff.
5. Every step is proven, from the cheap to the costly:
   1. Compiler and lint. Client, from `src/DM.Web.Client`: `npm run type-check` (four configs, specs and e2e among them) and `npx eslint --max-warnings 0 <files>`. Server, from the root: `dotnet build DM.sln`, where a warning is an error.
   2. Tests of what was touched: from `src/DM.Web.Client` `npx vitest run <specs and gate registers>`, from the root `dotnet test <test project>` for the touched modules.
   3. Comparison of the code of the step, from the root:
      - A move on the client is compared by the build. Before the step `node scripts/visual/compare-code.mjs --build --base-dir src/DM.Web.Client --save-base <scratchpad>/<step>-build --base-only`, after it `node scripts/visual/compare-code.mjs --build --baseline <scratchpad>/<step>-build`.
      - An edit of styles alone the same way, through `node scripts/visual/compare-css.mjs` with `--base-dir`, `--save-base <file> --base-only` and `--baseline <file>`.
      - An edit of comments is compared by tokens, with the revision as the only base. First `node scripts/visual/compare-code.mjs --head-dir <scratchpad>/<step> -- <paths>` shows that before the step the files matched HEAD in code, then `node scripts/visual/compare-code.mjs -- <paths>` compares the step. So an edit of comments goes before a cleanup of the same files.
   4. The stand, if the task assigns it: `node scripts/visual/visual.mjs diff <label>` over the working tree for what is drawn. `api-diff <label>` takes the server's answers from the running API, so it proves a server edit only after the API is rebuilt from the tree (`dm.ps1 e2e`, the data stays), when the task assigns that rebuild. Without it the server edit is not proven, and the report says so.
6. The unused is found by the compiler, not by a text search. A text search finds the name in a comment, in a string and on another symbol of the same name, and does not see a reference through a re-export. The list of the unused shrinks from before to after the step by exactly what was deleted.
   - Client, from `src/DM.Web.Client`: `npx vue-tsc --noEmit -p tsconfig.app.json --composite false --noUnusedLocals --noUnusedParameters`, and the same over `tsconfig.vitest.json` for the specs. It does not see unused exports: those are found by reading imports and exports through the TypeScript API, the `<script>` blocks of components included.
   - Server: IDE0005, IDE0051, IDE0052 and IDE0060 are raised in `.editorconfig` and enforced in the build through `EnforceCodeStyleInBuild`, so `dotnet build <project> --no-incremental` fails on the unused by itself. Without `--no-incremental` a project that was not rebuilt prints none of its findings. These analyzers do not see non-private members, those are found by a reference search over the semantic model.
   - What is reached by a string (a route, a registration through reflection, a path in a test, `vi.mock`, a template reference `ref="..."`) the compiler does not see, and `vue-tsc` calls a variable a template reaches through `ref="..."` unused outright. Removing such a thing is marked in the report "not proven until the full run of the wave": the full suites are not run in a step.
7. Modules are merged only when the task names what merges with what and which interface stays: that is the orchestrator's decision.
   - The argument is the deletion test: remove the module in thought. The complexity vanished, so the module is a pass-through and merges into its neighbour. It spread over the callers, so the module earns its place. An executor whose task fails this test writes so in the report and does not change the decision.
   - The merged module has a narrow interface: outward only what the callers call. Tests go through it and replace the tests of the small parts instead of piling on them.
   - A before and after comparison does not prove such a step. It is closed by the review of a fresh agent on the model the rule of [CLAUDE.md](../../CLAUDE.md) names, and the merge is not ready before that review.

## Report

For every step: what was done, the files, the proving commands with their exit codes, for a removal of the unused the lists before and after. Apart: what other files need, deleted files, removals marked "not proven until the full run of the wave", server edits without a rebuilt API, merge steps waiting for review, the model and the effort the agent ran on.

## Bounds

- Behaviour and look do not change, even where an edit is plainly better: such an edit goes to the report as a candidate.
- A test that a move or a merge removes is replaced by a check no weaker, and that check is proven on broken code.
- The stand, a reset, a seed and a rebuild of the API only on a direct assignment of the task.
