// The document itself. It carries facts only: no date, no commit, no count of
// tests, so it changes when the site does and at no other time.
import { SECTIONS, UNASSIGNED, sectionOf } from "./sections.mjs";

const FIRST_LINE = "Считается скриптом scripts/docs/build-status.mjs; рукой не правится";

/** Meta flags as the access column names them; any other flag is shown by its key. */
const FLAGS = {
  gameZone: "зона игры",
  blogZone: "зона блога",
  moderationZone: "модерация",
  fromLetter: "из письма",
};

/** Keys of the meta that name the tab and say nothing about access. */
const TITLE_KEYS = new Set(["title", "section", "dynamicTitle", "requiresAuth"]);

const NO_FRAME = "кадра нет";

function cell(text) {
  return String(text).replace(/\|/g, "\\|");
}

function code(text) {
  return `\`${cell(text)}\``;
}

function accessOf(meta) {
  const parts = [meta.requiresAuth ? "вход" : "все"];
  for (const [key, value] of Object.entries(meta)) {
    if (TITLE_KEYS.has(key) || value === false) continue;
    parts.push(FLAGS[key] ?? key);
  }
  return parts.join(", ");
}

function pageOf(row) {
  if (row.redirect) {
    const target = row.redirect.name ?? row.redirect.path ?? `вычисляется (${row.redirect.computed})`;
    return `перенаправление: ${row.redirect.name || row.redirect.path ? code(target) : target}`;
  }
  if (!row.component) return "-";
  return code(row.component.replace(/^@\/pages\//, ""));
}

function framesOf(row, frames) {
  const names = frames.states.filter((state) => state.route === row).map((state) => state.name);
  return names.length > 0 ? names.join(", ") : NO_FRAME;
}

function routeTables(site) {
  const lines = ["## Маршруты", ""];
  const order = [...SECTIONS.map(([name]) => name), UNASSIGNED];
  for (const section of order) {
    const rows = site.rows.filter((row) => sectionOf(row.path) === section);
    if (rows.length === 0) continue;
    lines.push(`### ${section}`, "", "| Маршрут | Путь | Страница | Доступ | Кадры |", "|---|---|---|---|---|");
    for (const row of rows) {
      const frames = row.redirect ? "-" : framesOf(row, site.frames);
      lines.push(
        `| ${row.name ?? "-"} | ${code(row.path)} | ${pageOf(row)} | ${accessOf(row.meta)} | ${frames} |`,
      );
    }
    lines.push("");
  }
  return lines;
}

function roleTable(frames) {
  const lines = ["## Кадры по ролям", "", "| Роль | Состояний |", "|---|---|"];
  for (const role of frames.roles) {
    lines.push(`| ${role} | ${frames.states.filter((state) => state.role === role).length} |`);
  }
  lines.push("");
  if (frames.unread > 0) lines.push(`Состояний, которые скрипт не прочел: ${frames.unread}.`, "");
  return lines;
}

function summary(site) {
  const pages = site.rows.filter((row) => !row.redirect);
  const framed = pages.filter((row) => site.frames.states.some((state) => state.route === row));
  return `Маршрутов ${pages.length}, с кадром ${framed.length}, эндпоинтов ${site.endpoints.length}.`;
}

const METHOD = [
  "Как считается:",
  "",
  "- маршрут это именованная запись `router.ts` и его модулей `routes/`, перенаправления идут строками без кадра и в итог не входят;",
  '- доступ "вход" значит `requiresAuth` на записи или выше нее; проверки ролей внутри страниц сюда не входят;',
  "- кадр это состояние `scripts/visual/states.ts`, адрес которого открывает маршрут; куда состояние уходит кликами, не учитывается;",
  "- эндпоинты берутся из `artifacts/openapi-routes.json`.",
  "",
];

export function render(site) {
  const lines = [FIRST_LINE, "", "# Состояние сайта", "", summary(site), "", ...METHOD];
  if (site.unreadRecords > 0) lines.push(`Записей роутера, которые скрипт не прочел: ${site.unreadRecords}.`, "");
  lines.push(...routeTables(site), ...roleTable(site.frames));
  return `${lines.join("\n").replace(/\n+$/, "")}\n`;
}
