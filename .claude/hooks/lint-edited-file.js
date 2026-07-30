#!/usr/bin/env node
/**
 * Hook: линт отредактированного файла сразу после записи.
 *
 * Зачем: аудит зафиксировал, что после правки локально не запускалось ничего —
 * ESLint жил только в CI, а до CI изменение доезжало пачкой. Классы ошибок,
 * которые ловятся именно линтером (нарушение границ FSD, возврат нативных
 * confirm/alert, неиспользованные импорты после удаления кода), всплывали
 * через часы работы вместо секунд.
 *
 * Почему только измененный файл, а не весь проект: полный прогон занимает
 * десятки секунд и после каждой правки неприемлем. Один файл — около секунды.
 *
 * Почему не type-check: vue-tsc проектный, инкрементально один файл не
 * проверяет, и двадцать секунд на правку сделали бы хук невыносимым. Типы
 * остаются на явном прогоне гейтов перед коммитом.
 *
 * Exit 2 = вернуть вывод ассистенту как замечание. Хук не блокирует правку:
 * файл уже записан, а промежуточное состояние в середине рефакторинга
 * законно бывает красным. Это сигнал, а не запрет.
 */

const { execFileSync } = require("child_process");
const path = require("path");

const CLIENT_ROOT = path.join("src", "DM.Web.Client");
const LINTABLE = /\.(vue|ts|tsx|js|cjs|mjs)$/i;

let payload = "";
try {
  payload = require("fs").readFileSync(0, "utf8");
} catch {
  process.exit(0);
}

let input;
try {
  input = JSON.parse(payload);
} catch {
  process.exit(0);
}

const filePath = input?.tool_input?.file_path;
if (!filePath || !LINTABLE.test(filePath)) process.exit(0);

// Линтер живет в src/DM.Web.Client и знает только про свое поддерево.
const normalized = filePath.split(path.sep).join("/");
const marker = CLIENT_ROOT.split(path.sep).join("/");
if (!normalized.includes(marker)) process.exit(0);

const clientDir = normalized.slice(0, normalized.indexOf(marker) + marker.length);
const relative = normalized.slice(clientDir.length + 1);

// Бинарь зовется напрямую через node, а не через npx: на Windows npx.cmd из
// child_process не запускался вовсе (status=null, пустой вывод), то есть хук
// молча пропускал все.
const eslintBin = path.join(clientDir, "node_modules", "eslint", "bin", "eslint.js");
if (!require("fs").existsSync(eslintBin)) process.exit(0);

// Спеки и конфиги линтуются тем же конфигом — исключений нет.
try {
  execFileSync(
    process.execPath,
    [eslintBin, relative, "--max-warnings", "0"],
    { cwd: clientDir, stdio: "pipe", encoding: "utf8", timeout: 60_000 },
  );
} catch (error) {
  const output = `${error.stdout ?? ""}${error.stderr ?? ""}`.trim();
  if (!output) process.exit(0);
  console.error("ESLint не доволен файлом " + relative + ":");
  console.error("");
  console.error(output);
  process.exit(2);
}

process.exit(0);
