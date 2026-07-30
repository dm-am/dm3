#!/usr/bin/env node
/**
 * Hook: блокировка создания новых миграций.
 * Разрешена только InitialCreate — см. critical_rules в .claude/CLAUDE.md.
 * Exit 2 = блокировка с сообщением для Claude.
 *
 * Почему node, а не bash+jq: jq на машине владельца нет, из-за чего
 * предыдущая версия падала на каждом Write и выходила с кодом 0,
 * то есть пропускала все. Node здесь уже используется соседним хуком.
 *
 * Почему не только Write: миграции создаются не записью файла, а командой
 * `dotnet ef migrations add`. Хук, следящий за одним Write, обходится
 * штатным способом их создания.
 *
 * PowerShell здесь потому, что на Windows это основной инструмент оболочки
 * в харнессе: хук, знающий только про Bash, обходится сменой инструмента.
 *
 * Что именно запрещено (решение владельца, 2026-07-28): вторая миграция, то
 * есть создание миграции с любым именем кроме InitialCreate. Пересоздание
 * единственной миграции — штатный цикл из DATA_STORAGE.md, и запрещать его
 * означало заставлять править миграцию, designer и снапшот руками; ровно
 * оттуда пришло расхождение снапшота с моделью. Дубль имени EF отвергает сам.
 *
 * Совпадение ищется от начала команды, а не подстрокой где угодно: подстрочная
 * проверка блокировала любой текст, где фраза просто упомянута.
 */

const ALLOWED_MIGRATION = /(InitialCreate|\.Designer\.cs$|DmDbContextModelSnapshot)/;
const MIGRATION_FILE = /Migrations[\\/].*\.cs$/i;
// dotnet-ef — форма глобального инструмента, она тоже создает миграцию.
// Команда должна стоять в начале строки или после разделителя, иначе под запрет
// попадает проза, в которой фраза только упоминается.
const MIGRATION_ADD = /(?:^|[;&|]\s*|\n\s*)dotnet[\s-]+ef\s+migrations\s+add\s+(\S+)/i;

const deny = (reason, detail) => {
  console.error("BLOCKED: " + reason);
  console.error("");
  console.error("Правило: пока разработка не завершена — только одна миграция InitialCreate.");
  console.error("Изменения схемы вносить в нее, в снапшот и в designer, затем накатывать ресетом базы.");
  console.error("");
  console.error(detail);
  process.exit(2);
};

let input = "";
process.stdin.setEncoding("utf8");
process.stdin.on("data", (chunk) => (input += chunk));
process.stdin.on("end", () => {
  let data;
  try {
    data = JSON.parse(input);
  } catch {
    // Неразобранный ввод не повод блокировать работу, но и молчать нельзя:
    // именно так предыдущая версия скрывала собственную неработоспособность.
    console.error("block-new-migrations: не удалось разобрать ввод хука");
    process.exit(0);
  }

  const tool = data.tool_name || "";
  const toolInput = data.tool_input || {};

  if (tool === "Write") {
    const filePath = toolInput.file_path || toolInput.path || "";
    if (MIGRATION_FILE.test(filePath) && !ALLOWED_MIGRATION.test(filePath)) {
      deny("создание новой миграции запрещено.", "Путь: " + filePath);
    }
  }

  if (tool === "Bash" || tool === "PowerShell") {
    const command = toolInput.command || "";
    const added = MIGRATION_ADD.exec(command);
    if (added && !ALLOWED_MIGRATION.test(added[1])) {
      deny(
        "команда создает вторую миграцию.",
        "Команда: " + command + "\nИмя: " + added[1] + " — разрешено только InitialCreate.",
      );
    }
  }

  process.exit(0);
});
