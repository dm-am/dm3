#!/usr/bin/env node
/**
 * Проверка хука в обе стороны: каждая форма создания второй миграции
 * блокируется, каждый штатный цикл проходит.
 * Запуск: node .claude/hooks/block-new-migrations.test.js
 *
 * Обе стороны важны одинаково: пересоздание InitialCreate — штатный цикл, и
 * хук, запрещающий его, ломает то, что должен защищать (довод в шапке хука).
 */

const { findViolation } = require("./block-new-migrations.js");

const BLOCK = [
  ["Bash", { command: "dotnet ef migrations add AddColumn" }, "вторая миграция"],
  ["Bash", { command: "dotnet-ef migrations add AddColumn" }, "форма глобального инструмента"],
  ["Bash", { command: "cd src && dotnet ef migrations add Whatever" }, "после разделителя команд"],
  ["Bash", { command: "dotnet  ef   migrations  add  Spaced" }, "лишние пробелы"],
  ["PowerShell", { command: "dotnet ef migrations add AddColumn" }, "та же команда из PowerShell"],
  ["Write", { file_path: "src/X/Migrations/20260101120000_AddColumn.cs" }, "файл новой миграции"],
  ["Write", { path: "src/X/Migrations/20260101120000_AddColumn.cs" }, "тот же файл под ключом path"],
];

const PASS = [
  ["Bash", { command: "dotnet ef migrations add InitialCreate" }, "пересоздание единственной миграции"],
  ["Bash", { command: "dotnet ef database update" }, "накат схемы"],
  ["Bash", { command: "grep -rn 'dotnet ef migrations add' docs/" }, "упоминание команды в поиске"],
  ["Write", { file_path: "src/X/Migrations/20260729122733_InitialCreate.cs" }, "сама InitialCreate"],
  ["Write", { file_path: "src/X/Migrations/20260729122733_InitialCreate.Designer.cs" }, "ее designer"],
  ["Write", { file_path: "src/X/Migrations/DmDbContextModelSnapshot.cs" }, "снапшот модели"],
  ["Write", { file_path: "src/X/Features/Games/GameService.cs" }, "обычный файл"],
  ["Read", { file_path: "src/X/Migrations/20260101_AddColumn.cs" }, "чтение файла миграции"],
];

let failures = 0;

for (const [tool, input, why] of BLOCK) {
  const blocked = findViolation(tool, input) !== null;
  console.log(`${blocked ? "ok   " : "ПЛОХО"} блок    :: ${tool} ${JSON.stringify(input)}   (${why})`);
  if (!blocked) failures++;
}

for (const [tool, input, why] of PASS) {
  const blocked = findViolation(tool, input) !== null;
  console.log(`${blocked ? "ПЛОХО" : "ok   "} пропуск :: ${tool} ${JSON.stringify(input)}   (${why})`);
  if (blocked) failures++;
}

console.log(`\nвсего ${BLOCK.length + PASS.length}, расхождений ${failures}`);
process.exit(failures === 0 ? 0 : 1);
