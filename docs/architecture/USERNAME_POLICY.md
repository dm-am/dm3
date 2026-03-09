# Политика имён пользователей (Login)

## Правила валидации

### Разрешённые символы

```
a-z A-Z       Латинские буквы
а-я А-Я ёЁ    Кириллица
0-9           Цифры
_             Нижнее подчёркивание
-             Дефис
.             Точка
<пробел>      Пробел (с ограничениями)
```

### Запрещённые символы

| Символ | Причина |
|--------|---------|
| `<` `>` | XSS-инъекция |
| `"` | HTML-атрибуты, SQL |
| `'` | SQL, JS-инъекция |
| `` ` `` | JS template literals |
| `\` | Escape-последовательности |
| `/` | URL path separator |
| `?` | URL query string |
| `#` | URL fragment |
| `%` | URL encoding |
| `&` | URL parameters, HTML entities |
| `@` | Email confusion, mentions |
| `[` `]` | Markdown, JSON |
| `(` `)` | Markdown links |
| `{` `}` | Template syntax |
| `=` | URL parameters |
| `~` | Home directory, regex |
| `!` `$` `^` `*` `+` `|` `;` `:` | Shell/regex special chars |
| Control chars | Невидимые символы |
| Zero-width | Спуфинг имён |

### Правила пробелов

- Не в начале
- Не в конце
- Не подряд

### Правила длины

- Минимум: 2 символа
- Максимум: 20 символов

---

## URL-стратегия

Используется URL-кодирование:

```
Имя:  "Adam Advena"
URL:  /users/Adam%20Advena
```

---

## Реализация

- Frontend: `LoginInput.vue`
- Backend: `LoginAvailabilityService.cs`, `ActivationRequestValidator.cs`

---

## Ссылки

- [Архитектура и паттерны](./ARCHITECTURE.md) — Паттерны, структура проектов
- [Миграция пользователей](../tasks/MIGRATION.md)

---

## Принципы документации

- **Минимум дублирования** — ссылки вместо копирования
- **Код > документация** — паттерны смотреть в коде
- **Только необходимое** — то, что нельзя узнать из кода
