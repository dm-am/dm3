# План консолидации цветовой палитры DM2 → DM3

## Двухуровневая система

### Уровень 1: Примитивы (27)

```sass
// Нейтральные (от тёмного к светлому)
$neutral-900: #333      // самый тёмный
$neutral-700: #666
$neutral-500: #999
$neutral-400: #ccc
$neutral-300: #ddd
$neutral-200: #e8e8e8
$neutral-150: #eaeaea
$neutral-100: #f5f5f5
$neutral-50:  #f9f9f9
$neutral-0:   #fff      // белый
$black:       #000

// Синие
$blue-700: #304060      // тёмный
$blue-500: #3f78a8      // средний
$blue-200: #b0c4de      // светлый

// Зелёные
$green-600: #696        // средний
$green-400: #7b7        // светлее
$green-200: #b1d090     // пастельный
$green-100: #ebe9da     // очень светлый

// Красные
$red-600: #b22222       // средний
$red-400: #d33          // светлее

// Коричневые
$brown-700: #630        // тёмный (заголовки)
$brown-500: #7b532b     // средний (комментарии)

// Тёплые акценты
$yellow-100: #f5f5dc    // кремовый
$peach-200:  #ffddbf    // персиковый
$gold-400:   #ffc73f    // золотой

// Специальные
$overlay-dark: rgba(0,0,0,0.8)
$bar-dark:     #313437
$sage:         #bac7a9  // шалфей
```

### Уровень 2: Семантические токены (34)

```sass
// ТЕКСТ
$text:              $neutral-900
$text-muted:        $neutral-500
$text-meta:         $brown-500
$text-accent-green: $green-600
$text-accent-red:   $red-600

// ЗАГОЛОВКИ
$heading:     $brown-700
$heading-alt: $neutral-700

// ССЫЛКИ
$link:                    $blue-700
$link-hover:              $blue-500
$link-alt:                $neutral-500
$link-alt-hover:          $black
$link-accent-green:       $green-600
$link-accent-green-hover: $green-400
$link-accent-red:         $red-600
$link-accent-red-hover:   $red-400

// ФОНЫ
$bg-page:          $neutral-0
$bg-element:       $neutral-50
$bg-accent:        $neutral-200
$bg-accent-yellow: $yellow-100
$bg-accent-green:  $green-100
$bg-accent-red:    $peach-200
$bg-table-header:  $neutral-150
$bg-bar:           $bar-dark

// КНОПКИ
$button-bg:          $neutral-150
$button-bg-hover:    $neutral-300
$button-bg-disabled: $neutral-100

// ГРАНИЦЫ
$border:              $neutral-400
$border-accent:       $blue-200
$border-accent-green: $green-600

// ПРОГРЕСС
$bar-progress: $green-200

// УВЕДОМЛЕНИЯ
$notification-bg:         $overlay-dark
$notification-text:       $neutral-150
$notification-link:       $sage
$notification-link-hover: $gold-400
```

---

## Полная таблица: DM2 → DM3 (финальная)

| Элемент | Цвет DM2 | Цвет DM3 (сейчас) | Цвет DM3 (план) | Переменная (план) |
|---------|----------|-------------------|-----------------|-------------------|
| **ТЕКСТ ОСНОВНОЙ** |||||
| Основной текст | #333 | #333 | #333 | $text |
| Таблицы текст | #444 | #333 | #333 | $text |
| Инпуты текст | #444 | #333 | #333 | $text |
| Кнопки текст | #444 | #fff | #333 | $text |
| Кнопки загрузки файлов текст | #333 | #333 | #333 | $text |
| Сообщения текст | #333 | #333 | #333 | $text |
| Цитаты текст | #333 | #333 | #333 | $text |
| Mod-сообщения текст | #333 | #333 | #333 | $text |
| Спойлеры текст | #333 | #333 | #333 | $text |
| Кубики текст | #333 | #333 | #333 | $text |
| Чат текст | #333 | #333 | #333 | $text |
| Список персонажей текст | #333 | #333 | #333 | $text |
| Сообщение о бане текст | #000 | — | #333 | $text |
| **ТЕКСТ ПРИГЛУШЁННЫЙ** |||||
| "Offline" статус-текст | #ccc | #8a8a8a | #999 | $text-muted |
| "Заявка на рассмотрении" | #ccc | #8a8a8a | #999 | $text-muted |
| Дата редактирования | #999 | #8a8a8a | #999 | $text-muted |
| "Получатели" в посте (текст) | #999 | #8a8a8a | #999 | $text-muted |
| "Сообщение мастеру" лейбл | #999 | #8a8a8a | #999 | $text-muted |
| Информация в футере | #999 | #8a8a8a | #999 | $text-muted |
| Замок без доступа | #dcdcdc | #8a8a8a | #999 | $text-muted |
| Disabled текст кнопок | — | #8a8a8a | #999 | $text-muted |
| Placeholder | — | #8a8a8a | #999 | $text-muted |
| **ТЕКСТ МЕТАИГРОВОЙ** |||||
| Метаигровой текст | #7b532b | #8b532b | #7b532b | $text-meta |
| **ТЕКСТ АКЦЕНТ ЗЕЛЁНЫЙ** |||||
| "Online" | #363 | #337b33 | #696 | $text-accent-green |
| Приват текст в посте | #363 | #337b33 | #696 | $text-accent-green |
| Сообщение мастеру текст | #363 | #337b33 | #696 | $text-accent-green |
| Предупреждения без баллов | #363 | #337b33 | #696 | $text-accent-green |
| Отзывы о DM текст | #363 | #337b33 | #696 | $text-accent-green |
| Рейтинг в профиле (текст) | #363 | #337b33 | #696 | $text-accent-green |
| Ромб без баллов | #363 | #337b33 | #696 | $text-accent-green |
| Рейтинг в таблице (текст) | #393 | #337b33 | #696 | $text-accent-green |
| Замок с доступом | #9fcf9f | #337b33 | #696 | $text-accent-green |
| **ТЕКСТ АКЦЕНТ КРАСНЫЙ** |||||
| Предупреждения с баллами | #b22222 | #900 | #b22222 | $text-accent-red |
| Красная звезда | #b22222 | #900 | #b22222 | $text-accent-red |
| Ромб с баллами | #b22222 | #900 | #b22222 | $text-accent-red |
| Отрицательный рейтинг (текст) | #b22222 | #900 | #b22222 | $text-accent-red |
| NSFW текст | #933 | #900 | #b22222 | $text-accent-red |
| Ожидание хода | — | #900 | #b22222 | $text-accent-red |
| **ЗАГОЛОВКИ** |||||
| Заголовки | #630 | #630 | #630 | $heading |
| Кнопки кубиков текст | #630 | #630 | #630 | $heading |
| Подзаголовки (панели сайдбаров) | #666 | #666 | #666 | $heading-alt |
| **ССЫЛКИ ОСНОВНЫЕ** |||||
| Ссылки | #304060 | #304060 | #304060 | $link |
| "Скрыть" (кнопка вверх) | #506080 | #304060 | #304060 | $link |
| "Помочь проекту" | #b22222 | #900 | #304060 | $link |
| Ссылки hover | #3f78a8 | #5a9fd4 | #3f78a8 | $link-hover |
| **ССЫЛКИ АЛЬТЕРНАТИВНЫЕ** |||||
| Главное меню | #999 | #8a8a8a | #999 | $link-alt |
| Имя персонажа в сообщениях | #666 | #666 | #999 | $link-alt |
| Имя персонажа в списке | #666 | #666 | #999 | $link-alt |
| Ссылки на прочитанные ЛС | #666 | #666 | #999 | $link-alt |
| Offline имена в чате | #777 | #8a8a8a | #999 | $link-alt |
| Неактивные читатели | #808080 | #8a8a8a | #999 | $link-alt |
| Количество непрочитанных (ссылка) | #999 | #8a8a8a | #999 | $link-alt |
| Альтернативные ссылки hover | — | #000 | #000 | $link-alt-hover |
| **ССЫЛКИ АКЦЕНТ ЗЕЛЁНЫЙ** |||||
| Новые игры в списках | #393 | #337b33 | #696 | $link-accent-green |
| Новые блоги в списках | #393 | #337b33 | #696 | $link-accent-green |
| Непрочитанные ЛС ссылка | #696 | #337b33 | #696 | $link-accent-green |
| Зелёные ссылки hover | — | #4a9f4a | #7b7 | $link-accent-green-hover |
| **ССЫЛКИ АКЦЕНТ КРАСНЫЙ** |||||
| Лайки | — | #900 | #b22222 | $link-accent-red |
| Отрицательный рейтинг (ссылка) | #b22222 | #900 | #b22222 | $link-accent-red |
| Красные ссылки hover | — | #c00 | #d33 | $link-accent-red-hover |
| **ФОНЫ ОСНОВНЫЕ** |||||
| Главный фон страницы | #fff | #fff | #fff | $bg-page |
| Чат фон | #fff | #fff | #fff | $bg-page |
| **ФОНЫ ЭЛЕМЕНТОВ** |||||
| Body (вне контента) | #f2f1f0 | #fafafa | #f9f9f9 | $bg-element |
| Сообщения фон | #f2f1f1 | #fafafa | #f9f9f9 | $bg-element |
| Mod-сообщения фон | #fafafa | #fafafa | #f9f9f9 | $bg-element |
| Таблицы фон | #f9f9f9 | #fafafa | #f9f9f9 | $bg-element |
| Поля ввода фон | #f9f9f9 | #fff | #f9f9f9 | $bg-element |
| Dropdown hover | — | #fafafa | #f9f9f9 | $bg-element |
| **ФОНЫ АКЦЕНТ** |||||
| Цитаты фон | #e6e6e5 | #e8e8f2 | #e8e8e8 | $bg-accent |
| Кубики фон | #e6e6e5 | #eee | #e8e8e8 | $bg-accent |
| Обновление чата фон | #e6e6e5 | #eee | #e8e8e8 | $bg-accent |
| Чередование персонажей | #e9e9e9 | #eee | #e8e8e8 | $bg-accent |
| Кнопки кубиков фон | #f0f0f0 | #eee | #e8e8e8 | $bg-accent |
| Кнопки загрузки фон | #f0f0f0 | #eee | #e8e8e8 | $bg-accent |
| Выделенный таб | #eee | #eee | #e8e8e8 | $bg-accent |
| Like-кнопки | — | #eee | #e8e8e8 | $bg-accent |
| **ФОНЫ АКЦЕНТ ЖЁЛТЫЙ** |||||
| Спойлеры фон | #f5f5dc | #ffe | #f5f5dc | $bg-accent-yellow |
| Панель модератора (ЛК) | #f5f5dc | #ffe | #f5f5dc | $bg-accent-yellow |
| Инфо-сообщения фон | #f5f5dc | #ffe | #f5f5dc | $bg-accent-yellow |
| **ФОНЫ АКЦЕНТ ЗЕЛЁНЫЙ** |||||
| Отзывы о DM фон | #ebe9da | #ffe | #ebe9da | $bg-accent-green |
| **ФОНЫ АКЦЕНТ КРАСНЫЙ** |||||
| Сообщение о бане фон | #ffddbf | — | #ffddbf | $bg-accent-red |
| NSFW предупреждение фон | rgba(255,200,200,0.5) | — | #ffddbf | $bg-accent-red |
| **ФОНЫ ПРОГРЕСС-БАРОВ** |||||
| Прогресс-бар фон | — | #fafafa | #313437 | $bg-bar |
| Донат-бар фон | #313437 | — | #313437 | $bg-bar |
| **КНОПКИ** |||||
| Фон кнопок | #eaeaea | #36a | #eaeaea | $button-bg |
| Фон кнопок hover | — | #a8cbff | #ddd | $button-bg-hover |
| Фон кнопок disabled | — | #ddd | #f5f5f5 | $button-bg-disabled |
| Фон инпутов disabled | — | #ddd | #f5f5f5 | $button-bg-disabled |
| **ТАБЛИЦЫ** |||||
| Заголовок таблиц фон | #eaeaea | #eee | #eaeaea | $bg-table-header |
| **ГРАНИЦЫ** |||||
| Общая граница | #ccc | #dfdfdf | #ccc | $border |
| Таблицы граница | #ccc | #dfdfdf | #ccc | $border |
| Сообщения граница | #ccc | #dfdfdf | #ccc | $border |
| Спойлеры граница | #ccc | #dfdfdf | #ccc | $border |
| Кнопки граница | #ccc | #dfdfdf | #ccc | $border |
| Кнопки кубиков граница | #ccc | #dfdfdf | #ccc | $border |
| Кнопки загрузки граница | #ccc | #dfdfdf | #ccc | $border |
| Чат граница | #ccc | #dfdfdf | #ccc | $border |
| Поля ввода граница | #ccc | #dfdfdf | #ccc | $border |
| Отзывы о DM граница | #ccc | — | #ccc | $border |
| Сообщение о бане граница | — | — | #ccc | $border |
| NSFW предупреждение граница | — | — | #ccc | $border |
| Рамка аватаров | #999 | #8a8a8a | #ccc | $border |
| Цитаты левая граница | #b0c4de | #c3c3d5 | #b0c4de | $border-accent |
| Mod-блок левая граница | #363 | #337b33 | #696 | $border-accent-green |
| **ПРОГРЕСС-БАРЫ** |||||
| Заполнение прогресс-бара | — | #b8caef | #b1d090 | $bar-progress |
| Донат прогресс | #b1d090 | — | #b1d090 | $bar-progress |
| **УВЕДОМЛЕНИЯ (TOAST)** |||||
| Фон toast | — | rgba(0,0,0,0.8) | rgba(0,0,0,0.8) | $notification-bg |
| Текст toast | — | #eaeaea | #eaeaea | $notification-text |
| Ссылки в toast | — | #bac7a9 | #bac7a9 | $notification-link |
| Ссылки в toast hover | — | #ffc73f | #ffc73f | $notification-link-hover |

---

## Сводка

| Уровень | Количество |
|---------|------------|
| Примитивы | 27 |
| Токены | 34 |

## Сравнение

| Версия | Количество |
|--------|------------|
| DM2 | 49 цветов |
| Текущий DM3 | 37 переменных |
| **Новый DM3** | **34 токена** (+ 27 примитивов) |

---

## Удаляемые переменные

| Текущая переменная | Заменить на |
|--------------------|-------------|
| `$secondary-text` | `$text-muted` |
| `$muted-text` | `$heading-alt` |
| `$comment-text` | `$text-meta` |
| `$accent-text` | `$heading` |
| `$positive-text` | `$text-accent-green` / `$link-accent-green` |
| `$positive-text-hover` | `$link-accent-green-hover` |
| `$negative-text` | `$text-accent-red` / `$link-accent-red` |
| `$negative-text-hover` | `$link-accent-red-hover` |
| `$active-text` | `$link` |
| `$active-text-hover` | `$link-hover` |
| `$secondary-text-hover` | `$link-alt-hover` |
| `$panel-background` | `$bg-element` |
| `$panel-background-highlight` | удалить |
| `$panel-background-hover` | `$bg-element` |
| `$control-background` | `$bg-accent` |
| `$quote-background` | `$bg-accent` |
| `$quote-outline` | `$border-accent` |
| `$spoiler-background` | `$bg-accent-yellow` |
| `$anticipation` | `$text-accent-red` |
| `$button-text` | `$text` |
| `$button-text-hover` | удалить |
| `$button-background` | `$button-bg` |
| `$button-background-hover` | `$button-bg-hover` |
| `$button-text-disabled` | `$text-muted` |
| `$button-background-disabled` | `$button-bg-disabled` |
| `$dropdown-option-background-hover` | `$bg-element` |
| `$input-background` | `$bg-element` |
| `$input-background-disabled` | `$button-bg-disabled` |
| `$negative-border` | удалить |
| `$progress-background` | `$bg-bar` |
| `$progress-background-done` | `$bar-progress` |
| `$shade-background` | удалить |
| `$shade-text` | удалить |
| `$overlay-background` | удалить |
