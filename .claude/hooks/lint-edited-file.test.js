#!/usr/bin/env node
/**
 * Проверка разбора пути в обе стороны: файл клиента отдается линтеру с верным
 * относительным путем, все остальное пропускается молча.
 * Запуск: node .claude/hooks/lint-edited-file.test.js
 *
 * Разбор пути — единственная нетривиальная часть хука, и ошибиться в нем можно
 * в обе стороны. В сторону молчания: файл клиента не признан своим и не
 * линтуется никогда. В сторону шума: маркер совпал подстрокой, относительный
 * путь вышел обрубком, линтер ответил "No files matching the pattern", и хук
 * напечатал это как замечание к файлу — жалоба на то, чего нет.
 */

const { resolveTarget } = require("./lint-edited-file.js");

const LINT = [
  ["src/DM.Web.Client/src/app/App.vue", "src/app/App.vue", "компонент"],
  ["src/DM.Web.Client/src/shared/api/client.ts", "src/shared/api/client.ts", "модуль"],
  ["src/DM.Web.Client/vite.config.ts", "vite.config.ts", "конфиг в корне клиента"],
  ["src/DM.Web.Client/src/x.spec.ts", "src/x.spec.ts", "спека линтуется тем же конфигом"],
  ["D:/repo/src/DM.Web.Client/src/x.ts", "src/x.ts", "абсолютный путь"],
];

const SKIP = [
  ["src/DM.Web.Client.ts", "имя файла начинается с имени каталога — не его файл"],
  ["src/DM.Web.ClientX/src/a.ts", "чужой каталог с тем же префиксом"],
  ["src/DM.Domain.Game/Features/Games/GameService.cs", "бэкенд"],
  ["src/DM.Web.Client/README.md", "нелинтуемое расширение"],
  ["docs/conventions/CODE_STYLE.md", "документация"],
  ["src/DM.Web.Client", "сам каталог без файла"],
  [undefined, "вызов без пути"],
];

let failures = 0;

for (const [input, expected, why] of LINT) {
  const target = resolveTarget(input);
  const ok = target !== null && target.relative === expected;
  console.log(`${ok ? "ok   " : "ПЛОХО"} линт    :: ${input} -> ${target ? target.relative : "null"}   (${why})`);
  if (!ok) failures++;
}

for (const [input, why] of SKIP) {
  const target = resolveTarget(input);
  const ok = target === null;
  console.log(`${ok ? "ok   " : "ПЛОХО"} пропуск :: ${input} -> ${target ? target.relative : "null"}   (${why})`);
  if (!ok) failures++;
}

console.log(`\nвсего ${LINT.length + SKIP.length}, расхождений ${failures}`);
process.exit(failures === 0 ? 0 : 1);
