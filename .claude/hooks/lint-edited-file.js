#!/usr/bin/env node
/**
 * Hook: линт отредактированного файла сразу после записи.
 *
 * Зачем: гейты перед пушем находят ошибку линтера через часы работы, хук через
 * секунду после правки. Это классы, которые ловит именно линтер: нарушение
 * границ FSD, возврат нативных confirm/alert, неиспользованные импорты после
 * удаления кода.
 *
 * Почему только измененный файл, а не весь проект: полный прогон занимает
 * десятки секунд и после каждой правки неприемлем. Один файл — около секунды.
 *
 * Почему не type-check: vue-tsc проектный, инкрементально один файл не
 * проверяет, и двадцать секунд на правку сделали бы хук невыносимым. Типы
 * остаются гейтам перед пушем.
 *
 * Exit 2 = вернуть вывод ассистенту как замечание. Хук не блокирует правку:
 * файл уже записан, а промежуточное состояние в середине рефакторинга
 * законно бывает красным. Это сигнал, а не запрет.
 */

const { execFileSync } = require("child_process");
const path = require("path");

const CLIENT_ROOT = "src/DM.Web.Client";
const LINTABLE = /\.(vue|ts|tsx|js|cjs|mjs)$/i;

/**
 * Каталог линтера и путь файла относительно него, или null, если файл линтеру
 * не принадлежит.
 *
 * Экспортировано ради теста: разбор пути здесь единственная нетривиальная часть.
 *
 * Маркер ищется как каталог, а не как подстрока. Подстрока совпадает и с
 * "src/DM.Web.Client.ts", относительный путь выходит равным "ts", линтер
 * отвечает кодом 2 и строкой "No files matching the pattern", и хук печатает
 * это как жалобу на файл, которого нет.
 */
function resolveTarget(filePath) {
  if (!filePath || !LINTABLE.test(filePath)) return null;

  const normalized = filePath.split(path.sep).join("/");
  const at = normalized.indexOf(CLIENT_ROOT + "/");
  if (at < 0) return null;

  const clientDir = normalized.slice(0, at + CLIENT_ROOT.length);
  const relative = normalized.slice(clientDir.length + 1);
  return relative ? { clientDir, relative } : null;
}

module.exports = { resolveTarget };

if (require.main !== module) return;

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

const target = resolveTarget(input?.tool_input?.file_path);
if (!target) process.exit(0);

const { clientDir, relative } = target;

// Бинарь зовется напрямую через node, а не через npx: на Windows npx.cmd из
// child_process не запускается (status=null, пустой вывод), и хук молча
// пропускал бы все.
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
