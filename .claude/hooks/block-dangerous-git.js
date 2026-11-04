#!/usr/bin/env node
/**
 * Hook: blocks git commands that lose work with no way back. Exit 2 blocks and
 * tells Claude why.
 *
 * The rule forbids losing unsaved work, not command names, so equivalents with
 * the same effect are listed too. Precision beats coverage: a hook that blocks
 * safe commands gets bypassed, so restore --staged, reset --soft and a plain
 * stash stay allowed.
 */

/**
 * Rules match at the START of a command segment, not anywhere in the text: the
 * hook receives the whole Bash line, including a commit message or a node -e
 * argument that only mentions a command. Position separates a call from a mention.
 * Accepted cost: a call hidden inside `sh -c "..."` is not caught.
 */

/**
 * RULES is the source of truth for forbidden git commands. AgentPermissionsShould
 * reads it and wants a deny line in .claude/settings.json for every rule. It reads
 * the start of each pattern as command text, so a pattern is anchored with ^ and
 * holds literals, \s+ and alternations before its first flag. A rule with a
 * negative lookahead (restore) is left to the hook alone.
 */
const RULES = [
  {
    pattern: /^git\s+checkout(?:\s|$)/i,
    why: 'git checkout — перезаписывает рабочее дерево',
  },
  {
    pattern: /^git\s+reset\s+(--hard|--merge)\b/i,
    why: 'git reset --hard / --merge — теряет незакоммиченные изменения (--soft разрешен)',
  },
  {
    // Any forcing flag: -f, -fd, -xdf, --force. Without one, clean deletes nothing.
    pattern: /^git\s+clean\b.*(?:-[a-zA-Z]*f|--force)/i,
    why: 'git clean с форсирующим флагом — удаляет untracked файлы',
  },
  {
    pattern: /^git\s+stash\s+(drop|clear)\b/i,
    why: 'git stash drop и clear — теряют отложенные изменения',
  },
  {
    // Without --staged it overwrites the working tree; with it only the index.
    pattern: /^git\s+restore\b(?!.*--staged)/i,
    why: 'git restore без --staged — перезаписывает рабочее дерево (--staged разрешен)',
  },
  {
    pattern: /^git\s+push\b.*(?:--force|\s-f\b)/i,
    why: 'форсированный push — перезаписывает историю на удаленном репозитории',
  },
  {
    // The only case-sensitive rule on purpose: -D deletes an unmerged branch, -d refuses.
    pattern: /^git\s+branch\s+(-D|--delete\s+--force)\b/,
    why: 'git branch -D — удаляет ветку вместе с коммитами, которых нет больше нигде (-d разрешен)',
  },
  {
    // Dangling commits and reflog entries are the last way back to lost work.
    pattern: /^git\s+(gc|prune|reflog\s+expire)\b.*(--prune|--expire)/i,
    why: 'сборка мусора с --prune/--expire — стирает висячие коммиты и записи reflog, последний способ вернуть потерянную работу',
  },
];

/** Heredoc bodies are data passed on, not commands, even when a line starts with git. */
function stripHeredocBodies(command) {
  return command.replace(
    /<<-?\s*(['"]?)([A-Za-z_][A-Za-z0-9_]*)\1[\s\S]*?^\s*\2\s*$/gm,
    '<<HEREDOC',
  );
}

/** The pieces of the line the shell starts as commands. */
function commandSegments(command) {
  return command
    .split(/\n|;|&&|\|\||\||\(|\)|`|\$\(/)
    .map((segment) => segment.trim())
    .filter(Boolean);
}

/** The rule that matched this line, or null. */
function findViolation(rawCommand) {
  const segments = commandSegments(stripHeredocBodies(rawCommand));
  for (const segment of segments) {
    for (const rule of RULES) {
      if (rule.pattern.test(segment)) return rule;
    }
  }
  return null;
}

module.exports = { findViolation, commandSegments, stripHeredocBodies };

// As a hook it reads JSON on stdin; as a module it hands findViolation to the test.
if (require.main === module) {
  let input = '';

  process.stdin.setEncoding('utf8');
  process.stdin.on('data', (chunk) => (input += chunk));
  process.stdin.on('end', () => {
    try {
      const data = JSON.parse(input);
      const violation = findViolation(data.tool_input?.command || '');

      if (violation) {
        console.error('BLOCKED: эта git-команда ЗАПРЕЩЕНА в проекте.');
        console.error('');
        console.error(`Сработало правило: ${violation.why}`);
        console.error('');
        console.error('Что делать вместо:');
        console.error('  отложить: git stash (без drop и clear)');
        console.error('  вернуть файл из коммита: git show HEAD:<путь> > <путь>');
        console.error('  во всех остальных случаях — спросить владельца');
        process.exit(2);
      }

      process.exit(0);
    } catch {
      // Unparsable input: the hook cannot tell what the command is. Letting it
      // through and saying so is the only choice that neither breaks work nor
      // pretends the check passed.
      console.error('block-dangerous-git: не удалось разобрать ввод хука, команда пропущена');
      process.exit(0);
    }
  });
}
