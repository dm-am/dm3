#!/usr/bin/env node
/**
 * Hook: блокировка создания новых миграций.
 * Разрешена только InitialCreate, правило в DATA_STORAGE.md, раздел "Миграции".
 * Exit 2 = блокировка с сообщением для Claude.
 *
 * Node, а не bash+jq: jq на машине владельца нет, а хук, который падает на
 * старте, выходит с кодом 0 и пропускает все.
 *
 * Не только Write: миграции создаются не записью файла, а командой
 * `dotnet ef migrations add`, и хук, следящий за одним Write, обходится
 * штатным способом их создания. PowerShell — потому что на Windows это
 * основной инструмент оболочки в харнессе, и хук, знающий только про Bash,
 * обходится сменой инструмента.
 *
 * Запрещена вторая миграция, то есть создание миграции с любым именем кроме
 * InitialCreate. Пересоздание единственной миграции — штатный цикл из
 * DATA_STORAGE.md: запрет на него заставил бы править миграцию, designer и
 * снапшот руками, а ручной снапшот расходится с моделью. Дубль имени EF
 * отвергает сам.
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
  console.error("Изменения схемы вносить пересозданием InitialCreate (команды в docs/guides/LOCAL_SETUP.md),");
  console.error("а не правкой миграции, снапшота и designer руками: ручной снапшот расходится с моделью. Затем ресет базы.");
  console.error("");
  console.error(detail);
  process.exit(2);
};

/**
 * Решение хука о конкретном вызове инструмента: причина отказа или null.
 *
 * Экспортировано ради теста: правило, которое нельзя вызвать из теста,
 * проверяется только на живой работе, когда оно уже пропустило то, что должно
 * было остановить.
 */
function findViolation(tool, toolInput) {
  if (tool === "Write") {
    const filePath = (toolInput && (toolInput.file_path || toolInput.path)) || "";
    if (MIGRATION_FILE.test(filePath) && !ALLOWED_MIGRATION.test(filePath)) {
      return { reason: "создание новой миграции запрещено.", detail: "Путь: " + filePath };
    }
  }

  if (tool === "Bash" || tool === "PowerShell") {
    const command = (toolInput && toolInput.command) || "";
    const added = MIGRATION_ADD.exec(command);
    if (added && !ALLOWED_MIGRATION.test(added[1])) {
      return {
        reason: "команда создает вторую миграцию.",
        detail: "Команда: " + command + "\nИмя: " + added[1] + " — разрешено только InitialCreate.",
      };
    }
  }

  return null;
}

module.exports = { findViolation };

// Как хук: читает JSON на stdin. Как модуль: отдает findViolation тесту.
if (require.main === module) {
  let input = "";
  process.stdin.setEncoding("utf8");
  process.stdin.on("data", (chunk) => (input += chunk));
  process.stdin.on("end", () => {
    let data;
    try {
      data = JSON.parse(input);
    } catch {
      // Неразобранный ввод не повод блокировать работу, но и молчать нельзя:
      // молчаливый выход с кодом 0 скрывает, что хук не работает.
      console.error("block-new-migrations: не удалось разобрать ввод хука");
      process.exit(0);
    }

    const violation = findViolation(data.tool_name || "", data.tool_input || {});
    if (violation) {
      deny(violation.reason, violation.detail);
    }

    process.exit(0);
  });
}
