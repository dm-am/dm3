#!/usr/bin/env node
/**
 * Hook: блокировка опасных git команд
 * Exit 2 = блокировка с сообщением для Claude
 */

let input = '';

process.stdin.setEncoding('utf8');
process.stdin.on('data', chunk => input += chunk);
process.stdin.on('end', () => {
  try {
    const data = JSON.parse(input);
    const command = data.tool_input?.command || '';

    // Проверяем опасные git команды
    const dangerousPatterns = [
      /git\s+checkout(?:\s|$)/i,
      /git\s+reset\s+--hard/i,
      /git\s+clean\s+-[a-z]*f[a-z]*d|git\s+clean\s+-[a-z]*d[a-z]*f/i,
      /git\s+stash\s+drop/i
    ];

    for (const pattern of dangerousPatterns) {
      if (pattern.test(command)) {
        console.error('BLOCKED: Эта git команда ЗАПРЕЩЕНА!');
        console.error('');
        console.error('Запрещенные команды:');
        console.error('  git checkout — теряет незакоммиченные изменения');
        console.error('  git reset --hard — теряет незакоммиченные изменения');
        console.error('  git clean -fd — удаляет untracked файлы');
        console.error('  git stash drop — теряет stash');
        console.error('');
        console.error('Альтернативы:');
        console.error('  git stash (без drop)');
        console.error('  Спросить пользователя');
        process.exit(2);
      }
    }

    process.exit(0);
  } catch (e) {
    process.exit(0);
  }
});
