# DM3

Платформа для текстовых ролевых игр.

## Быстрый старт

```powershell
# Windows
powershell -File .\scripts\dm.ps1 start    # Запуск Docker + API
powershell -File .\scripts\dm.ps1 seed     # Тестовые данные
powershell -File .\scripts\dm.ps1 help     # Остальные команды

# Linux и Mac: те же команды через ./scripts/dm.sh

# Клиент отдельно
cd src/DM.Web.Client && npm install && npm run dev

# Гейты CI перед пушем, один раз на клон
git config core.hooksPath scripts/hooks
```

Адреса сервисов и подробности в [LOCAL_SETUP](./docs/guides/LOCAL_SETUP.md).

## Документация

architecture/, как устроена система:

- [SYSTEM](./docs/architecture/SYSTEM.md): модули, слои и зависимости
- [AUTHENTICATION](./docs/architecture/AUTHENTICATION.md): вход, сессии, второй фактор
- [AUTHORIZATION](./docs/architecture/AUTHORIZATION.md): роли и права
- [UPLOADS](./docs/architecture/UPLOADS.md): прием, хранение и выдача загруженных картинок
- [BBCODE_RENDERING](./docs/architecture/BBCODE_RENDERING.md): разбор и вывод BBCode, кто видит скрытое

conventions/, правила разработки:

- [PATTERNS](./docs/conventions/PATTERNS.md): структура кода и блюпринт фичи
- [CODE_STYLE](./docs/conventions/CODE_STYLE.md): стандарты кода и русского текста
- [API_DESIGN](./docs/conventions/API_DESIGN.md): контракт HTTP API и форматы ответов
- [SECURITY](./docs/conventions/SECURITY.md): требования к безопасности
- [DATA_STORAGE](./docs/conventions/DATA_STORAGE.md): хранение данных
- [USERNAME_POLICY](./docs/conventions/USERNAME_POLICY.md): символы в именах
- [UI_STANDARDS](./docs/conventions/UI_STANDARDS.md): верстка, токены, окна
- [URL_STRUCTURE](./docs/conventions/URL_STRUCTURE.md): адреса страниц
- [PERFORMANCE](./docs/conventions/PERFORMANCE.md): правила оптимизации
- [PROCESS](./docs/conventions/PROCESS.md): работа над изменением до готовности

guides/, как делать:

- [LOCAL_SETUP](./docs/guides/LOCAL_SETUP.md): запуск локально
- [TESTING](./docs/guides/TESTING.md): тесты и их паттерны
- [DEPLOYMENT](./docs/guides/DEPLOYMENT.md): развертывание на сервере
- [POINT_OF_PRESENCE](./docs/guides/POINT_OF_PRESENCE.md): второй адрес сайта, точка присутствия
- [MONITORING](./docs/guides/MONITORING.md): логи, метрики, алерты

references/, справка:

- [GLOSSARY](./docs/references/GLOSSARY.md): термины
- [CONFIGURATION](./docs/references/CONFIGURATION.md): где лежат настройки

В корне docs/ документы о проекте целиком:

- [STATUS](./docs/STATUS.md): что готово, считается скриптом
- [STACK](./docs/STACK.md): из чего собран DM3 и почему
- [STACK_DM2](./docs/STACK_DM2.md): из чего собран DM2, старый живой сайт
