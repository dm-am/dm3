#!/usr/bin/env node
/**
 * Проверка хука в обе стороны: каждая опасная форма блокируется, каждая
 * безопасная проходит. Запуск: node .claude/hooks/block-dangerous-git.test.js
 *
 * Существует потому, что прежняя версия хука останавливала 2 варианта из 10, а
 * при расширении сразу нашлись две ошибки в другую сторону: правило для удаления
 * ветки с флагом регистронезависимости ловило безопасную форму, а проверка
 * подстроки в любом месте строки блокировала коммит, чье сообщение объясняло
 * запрет. Гейт надо проверять на способность падать И на способность не падать.
 */

const { findViolation } = require('./block-dangerous-git');

// [строка команды, ожидается ли блокировка, зачем этот случай]
const CASES = [
  // Прямые вызовы: должны блокироваться.
  ['git checkout main', true, 'имя из CLAUDE.md'],
  ['git checkout -- src/file.cs', true, 'форма с путем'],
  ['git reset --hard origin/dev', true, 'имя из CLAUDE.md'],
  ['git reset --merge', true, 'эквивалент --hard по эффекту'],
  ['git clean -fd', true, 'имя из CLAUDE.md'],
  ['git clean -f', true, 'один флаг вместо двух — прежняя версия пропускала'],
  ['git clean -xdf', true, 'флаги в другом порядке'],
  ['git clean --force', true, 'длинная форма флага'],
  ['git stash drop', true, 'имя из CLAUDE.md'],
  ['git stash clear', true, 'хуже drop: снимает все'],
  ['git restore .', true, 'прежняя версия пропускала'],
  ['git restore src/file.cs', true, 'перезапись рабочего дерева'],
  ['git push --force origin dev', true, 'перезапись истории на remote'],
  ['git push -f', true, 'короткая форма'],
  ['git branch -D feature', true, 'удаление ветки независимо от слияния'],
  ['git gc --prune=now', true, 'обрывает коммиты под тегом аудита'],
  ['git reflog expire --expire=now --all', true, 'то же через reflog'],
  ['cd "D:/repo" && git clean -f', true, 'после разделителя команд'],
  ['git status; git restore .', true, 'вторая команда в цепочке'],

  // Безопасные: должны проходить.
  ['git status', false, 'чтение'],
  ['git stash', false, 'откладывание без снятия — рекомендованная альтернатива'],
  ['git stash list', false, 'чтение'],
  ['git restore --staged src/file.cs', false, 'только индекс, рабочее дерево не тронуто'],
  ['git reset --soft origin/dev', false, 'squash: дерево и индекс сохраняются'],
  ['git clean -n', false, 'сухой прогон ничего не удаляет'],
  ['git push origin dev', false, 'обычный push — решение владельца, не потеря работы'],
  ['git push --follow-tags', false, 'push с тегами'],
  ['git show HEAD:src/file.cs', false, 'рекомендованный способ вернуть файл'],
  ['git branch -d merged', false, 'отказывается удалять неслитую ветку'],
  ['git log --oneline', false, 'чтение'],
  ['npm run build', false, 'не git вообще'],

  // Упоминание вместо вызова: должно проходить.
  [
    "git commit -F - <<EOF\nwhy git clean -f is banned\nEOF",
    false,
    'запрещенная команда в теле heredoc — это данные',
  ],
  [
    'node -e "const p=/git reset --hard/; console.log(p)"',
    false,
    'запрещенная строка в аргументе другой программы',
  ],
  [
    'grep -rn "git checkout" docs/',
    false,
    'поиск упоминания в документации',
  ],

  // Известная и принятая цена привязки к началу команды.
  ['sh -c "git clean -f"', false, 'обход через sh -c не ловится: задокументировано'],
];

let bad = 0;
for (const [command, shouldBlock, why] of CASES) {
  const violation = findViolation(command);
  const blocked = violation !== null;
  const ok = blocked === shouldBlock;
  if (!ok) bad++;
  const verdict = ok ? 'ok   ' : 'ПЛОХО';
  const expected = shouldBlock ? 'блок   ' : 'пропуск';
  console.log(
    `${verdict} ${expected} :: ${command.replace(/\n/g, '\\n')}` +
      (ok ? `   (${why})` : `   ПОЛУЧЕНО ${blocked ? 'блок' : 'пропуск'} — ${why}`),
  );
}

console.log(`\nвсего ${CASES.length}, расхождений ${bad}`);
process.exit(bad === 0 ? 0 : 1);
