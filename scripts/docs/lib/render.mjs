// The document itself. It carries facts only: no date, no commit, no count of
// tests, so it changes when the site does and at no other time.
import { compare } from "./repo.mjs";
import { SECTIONS, UNASSIGNED, sectionOf } from "./sections.mjs";

export const FIRST_LINE = "Считается скриптом scripts/docs/build-status.mjs; рукой не правится";

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

function routeTables(data) {
  const lines = ["## Маршруты", ""];
  const order = [...SECTIONS.map(([name]) => name), UNASSIGNED];
  for (const section of order) {
    const rows = data.rows.filter((row) => sectionOf(row.path) === section);
    if (rows.length === 0) continue;
    lines.push(`### ${section}`, "", "| Маршрут | Путь | Страница | Доступ | Кадры |", "|---|---|---|---|---|");
    for (const row of rows) {
      const frames = row.redirect ? "-" : framesOf(row, data.frames);
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

const endpointCode = ({ method, path }) => code(`${method} ${path}`);

/** Endpoints the client reaches only through an address the server writes into an answer. */
function servedList(data) {
  if (data.served.length === 0) return [];
  const lines = [
    `Ручки, адрес которых сервер пишет в ответ, а клиент открывает через \`apiUrl\` (${data.opensServed.map(code).join(", ")}), в список выше не входят:`,
    "",
  ];
  for (const { endpoint, files } of data.served) {
    lines.push(`- ${endpointCode(endpoint)}, адрес пишет ${files.map(code).join(", ")}`);
  }
  lines.push("");
  return lines;
}

/** Calls of the client that land on no endpoint: a typo, or an endpoint that is gone. */
function unmatchedList(data) {
  if (data.unmatched.length === 0) return [];
  const lines = ["Вызовы клиента, которые не ведут ни на одну ручку:", ""];
  for (const call of data.unmatched) lines.push(`- ${code(call.file)}: ${endpointCode(call)}`);
  lines.push("");
  return lines;
}

function orphanList(data) {
  const lines = [
    "## Сервер без клиента",
    "",
    "Ручки, на которые не ведет ни один вызов Api клиента и ни один адрес, который сервер пишет в ответ (метод и путь, имена параметров не важны).",
    "",
  ];
  for (const endpoint of data.orphans) lines.push(`- ${endpointCode(endpoint)}`);
  lines.push("", ...servedList(data), ...unmatchedList(data));
  if (data.unreadCalls.length > 0) {
    lines.push(
      "Вызовы, адрес которых скрипт не прочел: ручка, на которую они ведут, может стоять в списке выше ложно.",
      "",
    );
    for (const { file, call } of data.unreadCalls) lines.push(`- ${code(file)}: ${code(call)}`);
    lines.push("");
  }
  return lines;
}

/** Pages nothing in the client links to; the catch-all is reached by any wrong address and is left out. */
export function unlinked(data) {
  return data.rows.filter(
    (row) => !row.redirect && !row.path.includes("(.*)") && data.clientLinks.get(row).size === 0,
  );
}

function unlinkedTable(data) {
  const lines = [
    "## Страницы без входа с сайта",
    "",
    "Ни имени, ни адреса страницы нет ни в одной ссылке клиента, кроме роутера, спеков и самой страницы.",
    "",
    "| Маршрут | Путь | Ведет снаружи |",
    "|---|---|---|",
  ];
  for (const row of unlinked(data)) {
    const outside = [...data.serverLinks.get(row)].sort(compare).join(", ") || "ничто";
    lines.push(`| ${row.name ?? "-"} | ${code(row.path)} | ${outside} |`);
  }
  lines.push("");
  return lines;
}

function documentTable(documents) {
  const lines = [
    "## Документы",
    "",
    `Обходят все документы: ${documents.walkers.join(", ")}.`,
    "",
    "| Документ | Открывают по пути |",
    "|---|---|",
  ];
  for (const document of documents.list) {
    const tests = document.tests.length > 0 ? document.tests.join(", ") : "отдельного теста нет";
    lines.push(`| ${code(document.path)} | ${tests} |`);
  }
  lines.push("");
  return lines;
}

function summary(data) {
  const pages = data.rows.filter((row) => !row.redirect);
  const framed = pages.filter((row) => data.frames.states.some((state) => state.route === row));
  const held = data.documents.list.filter((document) => document.tests.length > 0);
  return (
    `Маршрутов ${pages.length}, с кадром ${framed.length}, ручек ${data.endpoints.length}, ` +
    `без клиента ${data.orphans.length}, страниц без входа ${unlinked(data).length}, ` +
    `документов ${data.documents.list.length}, с отдельным тестом ${held.length}.`
  );
}

const METHOD = [
  "Как считается:",
  "",
  "- маршрут это именованная запись `router.ts`, перенаправления идут строками без кадра и в итог не входят;",
  '- доступ "вход" значит `requiresAuth` на записи или выше нее; проверки ролей внутри страниц сюда не входят;',
  "- кадр это состояние `scripts/visual/states.ts`, адрес которого открывает маршрут; куда состояние уходит кликами, не учитывается;",
  "- ручки берутся из `artifacts/openapi-routes.json`; у ручки есть клиент, если на нее ведет вызов Api или адрес, который сервер пишет в ответ, а клиент открывает через `apiUrl`;",
  "- тест `test/DM.Architecture.Tests` обходит все документы, если в коде отбирает `.md` по целому дереву, и открывает документ, если в коде называет его путь от корня строковыми литералами; комментарии, сообщения и фикстуры не в счет.",
  "",
];

export function render(data) {
  const lines = [FIRST_LINE, "", "# Состояние сайта", "", summary(data), "", ...METHOD];
  if (data.unreadRecords > 0) lines.push(`Записей роутера, которые скрипт не прочел: ${data.unreadRecords}.`, "");
  lines.push(
    ...routeTables(data),
    ...roleTable(data.frames),
    ...orphanList(data),
    ...unlinkedTable(data),
    ...documentTable(data.documents),
  );
  return `${lines.join("\n").replace(/\n+$/, "")}\n`;
}
