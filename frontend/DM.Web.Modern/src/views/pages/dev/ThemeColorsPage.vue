<script setup lang="ts">
/**
 * Theme Colors Test Page
 * Полная демонстрация всех цветовых переменных и UI элементов системы тем
 */
import { ref } from "vue";

// Переменные (Variables.sass + Layout.sass + Inputs.sass)
const sizeGroups = [
  {
    name: "Сетка",
    vars: [
      { sass: "$grid-step", value: "4px", usage: "базовый шаг" },
      { sass: "$tiny", value: "2px", usage: "" },
      { sass: "$minor", value: "4px", usage: "" },
      { sass: "$small", value: "8px", usage: "" },
      { sass: "$medium", value: "16px", usage: "" },
      { sass: "$big", value: "32px", usage: "" },
      { sass: "$major", value: "64px", usage: "" },
      { sass: "$large", value: "128px", usage: "" },
    ],
  },
  {
    name: "Типографика",
    vars: [
      { sass: "$tertiary-font-size", value: "12px", usage: "мелкий" },
      { sass: "$secondary-font-size", value: "14px", usage: "вторичный" },
      { sass: "$font-size", value: "16px", usage: "основной" },
      { sass: "$title-font-size", value: "20px", usage: "заголовки" },
      { sass: "$menu-font-size", value: "25px", usage: "меню" },
    ],
  },
  {
    name: "Лейаут",
    vars: [
      { sass: "$max-width", value: "1400px", usage: "макс. ширина" },
      { sass: "$min-width", value: "1000px", usage: "мин. ширина" },
      { sass: "$sidebar-width", value: "330px", usage: "боковая панель" },
      { sass: "$header-height", value: "100px", usage: "шапка" },
      { sass: "$header-row-height", value: "55px", usage: "строка шапки" },
      { sass: "$footer-height", value: "115px", usage: "подвал" },
    ],
  },
  {
    name: "Прочее",
    vars: [
      {
        sass: "$border-radius",
        value: "8px",
        usage: "скругление (диалоги, карточки)",
      },
      { sass: "$animation-time", value: "0.3s", usage: "анимации" },
      { sass: "$input-padding", value: "5px 8px", usage: "отступы в полях" },
    ],
  },
];

// Миксины
const mixinGroups = [
  {
    name: "Inputs.sass",
    mixins: [
      { sass: "+input", usage: "стиль полей ввода" },
      { sass: "+input-base", usage: "базовый стиль без шрифта" },
      { sass: "+button", usage: "стиль кнопок (пунктир, жирный)" },
      { sass: "+badge", usage: "бейдж/тег" },
    ],
  },
  {
    name: "Tables.sass",
    mixins: [
      { sass: "+table", usage: "внешняя граница таблицы" },
      { sass: "+table-header", usage: "заголовок таблицы" },
      { sass: "+table-row", usage: "строка таблицы" },
      { sass: "+table-columns", usage: "границы столбцов + padding" },
    ],
  },
  {
    name: "Layout.sass",
    mixins: [
      { sass: "+menu-container", usage: "контейнер меню" },
      { sass: "+content-container", usage: "контейнер контента" },
      { sass: "+sidebar-container", usage: "контейнер сайдбара" },
      { sass: "+square($size)", usage: "квадрат заданного размера" },
      { sass: "+icon", usage: "шрифт Iconic" },
    ],
  },
  {
    name: "_BbcodeContent.sass",
    mixins: [
      { sass: "+bbcode-content", usage: "стили BBCode (отображение)" },
      { sass: "+bbcode-content-editor", usage: "стили BBCode (редактор)" },
    ],
  },
];

// Цвета (ThemeVariables.css)
const colorGroups = [
  {
    name: "Текст",
    colors: [
      { var: "--text", sass: "$text", usage: "Основной текст" },
      {
        var: "--text-muted",
        sass: "$text-muted",
        usage: "Приглушённый, подписи",
      },
      { var: "--text-meta", sass: "$text-meta", usage: "Акцентный коричневый" },
      {
        var: "--text-on-green",
        sass: "$text-on-green",
        usage: "Текст на зелёном фоне",
      },
      {
        var: "--text-on-red",
        sass: "$text-on-red",
        usage: "Текст на красном фоне",
      },
    ],
  },
  {
    name: "Заголовки",
    colors: [
      { var: "--heading", sass: "$heading", usage: "Основные" },
      { var: "--heading-alt", sass: "$heading-alt", usage: "Альтернативные" },
    ],
  },
  {
    name: "Акценты",
    colors: [
      { var: "--accent-green", sass: "$accent-green", usage: "Позитив, успех" },
      {
        var: "--accent-green-hover",
        sass: "$accent-green-hover",
        usage: "Позитив hover",
      },
      {
        var: "--accent-red",
        sass: "$accent-red",
        usage: "Внимание, NSFW, удаление",
      },
      {
        var: "--accent-red-hover",
        sass: "$accent-red-hover",
        usage: "Внимание hover",
      },
    ],
  },
  {
    name: "Ссылки",
    colors: [
      { var: "--link", sass: "$link", usage: "Основные" },
      { var: "--link-hover", sass: "$link-hover", usage: "При наведении" },
      { var: "--link-nav", sass: "$link-nav", usage: "Навигация" },
      {
        var: "--link-nav-hover",
        sass: "$link-nav-hover",
        usage: "Навигация hover",
      },
    ],
  },
  {
    name: "Фоны",
    colors: [
      { var: "--bg-page", sass: "$bg-page", usage: "Страница" },
      { var: "--bg-element", sass: "$bg-element", usage: "Карточки, панели" },
      {
        var: "--bg-element-accent",
        sass: "$bg-element-accent",
        usage: "Акцентный (заголовки таблиц)",
      },
      {
        var: "--bg-highlight-blue",
        sass: "$bg-highlight-blue",
        usage: "Подсветка",
      },
      {
        var: "--bg-highlight-yellow",
        sass: "$bg-highlight-yellow",
        usage: "Жёлтый (спойлеры)",
      },
      {
        var: "--bg-highlight-green",
        sass: "$bg-highlight-green",
        usage: "Зелёный (приват)",
      },
      {
        var: "--bg-highlight-red",
        sass: "$bg-highlight-red",
        usage: "Красный подсвет",
      },
    ],
  },
  {
    name: "Кнопки",
    colors: [
      { var: "--button-bg", sass: "$button-bg", usage: "Фон" },
      {
        var: "--button-bg-hover",
        sass: "$button-bg-hover",
        usage: "Фон hover",
      },
      {
        var: "--button-bg-disabled",
        sass: "$button-bg-disabled",
        usage: "Фон disabled",
      },
      { var: "--button-text", sass: "$button-text", usage: "Текст" },
      {
        var: "--button-text-hover",
        sass: "$button-text-hover",
        usage: "Текст hover",
      },
      {
        var: "--button-text-disabled",
        sass: "$button-text-disabled",
        usage: "Текст disabled",
      },
      { var: "--button-border", sass: "$button-border", usage: "Рамка" },
      {
        var: "--button-border-hover",
        sass: "$button-border-hover",
        usage: "Рамка hover",
      },
      {
        var: "--button-border-disabled",
        sass: "$button-border-disabled",
        usage: "Рамка disabled",
      },
    ],
  },
  {
    name: "Границы",
    colors: [
      { var: "--border", sass: "$border", usage: "Основные" },
      {
        var: "--border-accent-blue",
        sass: "$border-accent-blue",
        usage: "Синие (цитаты)",
      },
      {
        var: "--border-accent-green",
        sass: "$border-accent-green",
        usage: "Зелёные (мод-блок)",
      },
      {
        var: "--border-accent-red",
        sass: "$border-accent-red",
        usage: "Красные (ошибки)",
      },
    ],
  },
  {
    name: "Формы",
    colors: [
      { var: "--input-bg", sass: "$input-bg", usage: "Фон поля ввода" },
      {
        var: "--input-bg-disabled",
        sass: "$input-bg-disabled",
        usage: "Фон отключённого",
      },
      {
        var: "--progress-bg",
        sass: "$progress-bg",
        usage: "Фон прогресс-бара",
      },
      {
        var: "--progress-fill",
        sass: "$progress-fill",
        usage: "Заполнение прогресса",
      },
    ],
  },
  {
    name: "Оверлеи",
    colors: [
      { var: "--shade-bg", sass: "$shade-bg", usage: "Затемнение (модалки)" },
      {
        var: "--shade-text",
        sass: "$shade-text",
        usage: "Текст на затемнении",
      },
      { var: "--overlay-bg", sass: "$overlay-bg", usage: "Лёгкий оверлей" },
      { var: "--hover-overlay", sass: "$hover-overlay", usage: "Hover эффект" },
      {
        var: "--active-overlay",
        sass: "$active-overlay",
        usage: "Active эффект",
      },
      { var: "--nsfw-overlay", sass: "$nsfw-overlay", usage: "NSFW размытие" },
      {
        var: "--highlight-overlay-green",
        sass: "$highlight-overlay-green",
        usage: "Зелёный (мод, приват)",
      },
      {
        var: "--highlight-overlay-red",
        sass: "$highlight-overlay-red",
        usage: "Красный (ошибки)",
      },
      {
        var: "--highlight-overlay-blue",
        sass: "$highlight-overlay-blue",
        usage: "Синий (цитаты)",
      },
    ],
  },
  {
    name: "Специальные",
    colors: [
      {
        var: "--shadow-color",
        sass: "$shadow-color",
        usage: "Цвет теней (box-shadow)",
      },
      {
        var: "--filter-invert",
        sass: "$filter-invert",
        usage: "CSS filter для инверсии",
      },
    ],
  },
];

const expandedGroup = ref<string | null>(null);
const spoilerOpen = ref(false);
const nsfwOpen = ref(false);
const nsfwConfirmed = ref(false);

// Toggle demos state
const toggleDemoMode = ref<"bbcode" | "visual">("bbcode");
function setToggleMode(mode: "bbcode" | "visual") {
  toggleDemoMode.value = mode;
}

// Compact mode demos state (TODO: add UI toggle for compact view demos)
// eslint-disable-next-line @typescript-eslint/no-unused-vars
const _compactDemoMode = ref(false);

function toggleGroup(name: string) {
  expandedGroup.value = expandedGroup.value === name ? null : name;
}

function confirmNsfw() {
  nsfwConfirmed.value = true;
}
</script>

<template>
  <div class="theme-colors-page">
    <h1 class="page-title">Переменные темы</h1>

    <!-- Переменные -->
    <section class="vars-section">
      <h2 class="section-title">Переменные</h2>
      <div class="vars-grid">
        <div v-for="group in sizeGroups" :key="group.name" class="vars-group">
          <h4>{{ group.name }} ({{ group.vars.length }})</h4>
          <div v-for="v in group.vars" :key="v.sass" class="var-item">
            <code>{{ v.sass }}</code>
            <span>{{ v.value }}{{ v.usage ? " — " + v.usage : "" }}</span>
          </div>
        </div>
      </div>
    </section>

    <!-- Миксины -->
    <section class="vars-section">
      <h2 class="section-title">Миксины</h2>
      <div class="vars-grid">
        <div v-for="group in mixinGroups" :key="group.name" class="vars-group">
          <h4>{{ group.name }} ({{ group.mixins.length }})</h4>
          <div v-for="m in group.mixins" :key="m.sass" class="var-item">
            <code>{{ m.sass }}</code>
            <span>{{ m.usage }}</span>
          </div>
        </div>
      </div>
    </section>

    <!-- Цвета -->
    <section class="swatches-section">
      <h2 class="section-title">
        Цвета ({{ colorGroups.reduce((sum, g) => sum + g.colors.length, 0) }}
        шт.)
      </h2>
      <div v-for="group in colorGroups" :key="group.name" class="swatch-group">
        <button class="swatch-group-header" @click="toggleGroup(group.name)">
          <span class="swatch-group-name"
            >{{ group.name }} ({{ group.colors.length }})</span
          >
          <span class="swatch-group-toggle">{{
            expandedGroup === group.name ? "−" : "+"
          }}</span>
        </button>
        <div v-show="expandedGroup === group.name" class="swatch-group-content">
          <div
            v-for="color in group.colors"
            :key="color.var"
            class="swatch-item"
          >
            <div
              class="swatch-color"
              :style="{
                backgroundColor:
                  color.var === '--filter-invert'
                    ? 'var(--text)'
                    : `var(${color.var})`,
                filter:
                  color.var === '--filter-invert'
                    ? `var(${color.var})`
                    : 'none',
              }"
            />
            <div class="swatch-info">
              <code>{{ color.sass }}</code>
              <span class="swatch-usage">{{ color.usage }}</span>
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- Live examples -->
    <section class="examples-section">
      <h2 class="section-title">Примеры</h2>

      <!-- Размеры шрифтов -->
      <div class="example-group">
        <h3 class="example-title">Размеры шрифтов</h3>
        <div class="example-content demo-typography">
          <p class="demo-font-menu">$menu-font-size (25px)</p>
          <p class="demo-font-title">$title-font-size (20px)</p>
          <p class="demo-font-base">$font-size (16px) — основной</p>
          <p class="demo-font-secondary">$secondary-font-size (14px)</p>
          <p class="demo-font-tertiary">$tertiary-font-size (12px)</p>
        </div>
      </div>

      <!-- Заголовки -->
      <div class="example-group">
        <h3 class="example-title">Заголовки</h3>
        <div class="example-content">
          <h2 class="demo-page-title">PageTitle (h2) - $heading</h2>
          <h3 class="demo-block-title">BlockTitle (h3) - $heading</h3>
          <h4 class="demo-sidebar-title">SidebarTitle (h4) - $heading-alt</h4>
        </div>
      </div>

      <!-- 2. Текст -->
      <div class="example-group">
        <h3 class="example-title">Текст</h3>
        <div class="example-content">
          <p class="demo-text">Основной текст ($text)</p>
          <p class="demo-text-muted">Приглушённый текст ($text-muted)</p>
          <p class="demo-text-meta">Акцентный коричневый текст ($text-meta)</p>
          <p class="demo-text-green">Зелёный акцент ($accent-green)</p>
          <p class="demo-text-red">Красный акцент ($accent-red)</p>
          <p class="demo-text-on-green">
            Текст на зелёном фоне ($text-on-green)
          </p>
          <p class="demo-text-on-red">Текст на красном фоне ($text-on-red)</p>
        </div>
      </div>

      <!-- 3. Ссылки -->
      <div class="example-group">
        <h3 class="example-title">Ссылки</h3>
        <div class="example-content">
          <p>
            <a href="#" class="demo-link">Обычная ссылка</a> ·
            <a href="#" class="demo-link-nav">Навигация</a> ·
            <a href="#" class="demo-link-green">Зелёная</a> ·
            <a href="#" class="demo-link-red">Красная</a>
          </p>
        </div>
      </div>

      <!-- 4. Кнопки -->
      <div class="example-group">
        <h3 class="example-title">Кнопки</h3>
        <div class="example-content">
          <div class="demo-buttons">
            <button class="demo-btn">Обычная</button>
            <button class="demo-btn" disabled>Отключена</button>
          </div>
          <p class="demo-hint">
            Пунктирная рамка, жирный текст, без скругления
          </p>
        </div>
      </div>

      <!-- 5. Бейджи -->
      <div class="example-group">
        <h3 class="example-title">Бейджи и теги</h3>
        <div class="example-content">
          <div class="demo-badges">
            <span class="demo-badge">3</span>
            <span class="demo-badge">Новое</span>
            <span class="demo-user-badge"
              >[<span class="badge-admin">A</span>]</span
            >
            <span class="demo-user-badge"
              >[<span class="badge-mod">M</span>]</span
            >
            <span class="demo-rating"
              ><span class="rating-positive">+42</span>/128</span
            >
            <span class="demo-rating"
              ><span class="rating-negative">-5</span>/10</span
            >
          </div>
          <p class="demo-hint">Миксин: +badge()</p>
        </div>
      </div>

      <!-- 6. Инпуты -->
      <div class="example-group">
        <h3 class="example-title">Формы</h3>
        <div class="example-content">
          <div class="demo-form">
            <div class="demo-form-field">
              <label>Обычное поле</label>
              <input type="text" class="demo-input" />
            </div>
            <div class="demo-form-field">
              <label>Отключено</label>
              <input type="text" class="demo-input" disabled />
            </div>
          </div>
          <div class="demo-form-field" style="grid-column: span 2">
            <label>Textarea</label>
            <textarea class="demo-input demo-textarea" rows="2"></textarea>
          </div>
          <div class="demo-checkboxes">
            <label><input type="checkbox" checked /> Отмечен</label>
            <label><input type="checkbox" /> Не отмечен</label>
            <label><input type="checkbox" disabled /> Отключён</label>
          </div>
          <p class="demo-hint">+input(): сплошная рамка, box-shadow на focus</p>
        </div>
      </div>

      <!-- 7. Сбор средств -->
      <div class="example-group">
        <h3 class="example-title">Сбор средств</h3>
        <div class="example-content">
          <div class="demo-progress">
            <div class="demo-progress-fill" style="width: 65%"></div>
            <div class="demo-progress-text">17000 / 50000 р.</div>
          </div>
        </div>
      </div>

      <!-- 8. Таблица -->
      <div class="example-group">
        <h3 class="example-title">Таблица</h3>
        <div class="example-content">
          <table class="demo-table">
            <thead>
              <tr>
                <th>Раздел</th>
                <th>Темы</th>
                <th>Сообщения</th>
                <th>Последнее</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td>Общий раздел</td>
                <td>142</td>
                <td>3856</td>
                <td class="demo-text-meta">2 часа назад</td>
              </tr>
              <tr>
                <td>Игровые системы</td>
                <td>89</td>
                <td>1247</td>
                <td class="demo-text-meta">вчера</td>
              </tr>
            </tbody>
          </table>
          <p class="demo-hint">
            Заголовок: $bg-element-accent, ячейки: $bg-element
          </p>
        </div>
      </div>

      <!-- 9. Пагинация -->
      <div class="example-group">
        <h3 class="example-title">Пагинация</h3>
        <div class="example-content">
          <div class="demo-paging">
            <a href="#" class="paging-link">&laquo;</a>
            <a href="#" class="paging-link">1</a>
            <a href="#" class="paging-link">2</a>
            <a href="#" class="paging-link">3</a>
            <a href="#" class="paging-link">&raquo;</a>
          </div>
        </div>
      </div>

      <!-- 10. Фоны -->
      <div class="example-group">
        <h3 class="example-title">Фоны</h3>
        <div class="example-content">
          <div class="demo-backgrounds">
            <div class="demo-bg demo-bg-page">bg-page</div>
            <div class="demo-bg demo-bg-element">bg-element</div>
            <div class="demo-bg demo-bg-element-accent">bg-element-accent</div>
            <div class="demo-bg demo-bg-highlight-blue">bg-highlight-blue</div>
            <div class="demo-bg demo-bg-highlight-yellow">
              bg-highlight-yellow
            </div>
            <div class="demo-bg demo-bg-highlight-green">
              bg-highlight-green
            </div>
            <div class="demo-bg demo-bg-highlight-red">bg-highlight-red</div>
          </div>
        </div>
      </div>

      <!-- 11. BBCode контент -->
      <div class="example-group">
        <h3 class="example-title">BBCode контент</h3>
        <div class="example-content demo-bbcode-content">
          <!-- Типографика -->
          <p>
            <strong>Жирный текст</strong>, <em>курсив</em>, <u>подчёркнутый</u>,
            <s>зачёркнутый</s>
          </p>

          <!-- Цитата -->
          <blockquote class="demo-quote">
            <cite>Пользователь написал:</cite>
            <p>Цитата: $border-accent-blue + $highlight-overlay-blue</p>
          </blockquote>

          <!-- Мод-блок -->
          <div class="demo-mod">
            <div class="demo-mod-author">Модератор:</div>
            <p>Мод-блок: $border-accent-green + $highlight-overlay-green</p>
          </div>

          <!-- Warning блок -->
          <div class="demo-warning">
            <div class="demo-warning-header">Внимание:</div>
            <p>Warning: $border-accent-red + $highlight-overlay-red</p>
          </div>

          <!-- Спойлер -->
          <div class="demo-spoiler-container">
            <a class="demo-spoiler-head" @click="spoilerOpen = !spoilerOpen">{{
              spoilerOpen ? "Скрыть содержимое" : "Показать содержимое"
            }}</a>
            <div v-show="spoilerOpen" class="demo-spoiler">
              Содержимое спойлера. Фон: $bg-highlight-yellow, рамка пунктиром.
            </div>
          </div>

          <!-- NSFW -->
          <div class="demo-nsfw-container">
            <a class="demo-nsfw-head" @click="nsfwOpen = !nsfwOpen">{{
              nsfwOpen
                ? "Скрыть шокирующий контент"
                : "Показать шокирующий контент"
            }}</a>
            <div v-show="nsfwOpen" class="demo-nsfw">
              <div
                v-if="!nsfwConfirmed"
                class="demo-nsfw-overlay"
                @click="confirmNsfw"
              >
                <span class="demo-nsfw-warning"
                  >Если вам исполнилось 18 лет и вы готовы к просмотру контента,
                  который может оказаться для вас неприемлемым, нажмите
                  сюда.</span
                >
              </div>
              NSFW контент. Такой же стиль как спойлер, но с оверлеем 18+.
            </div>
          </div>

          <!-- Приватный текст -->
          <p>
            Обычный текст и <span class="demo-private">приватный текст</span> в
            одной строке.
          </p>

          <!-- Код -->
          <pre class="demo-code">console.log("Hello, world!");</pre>

          <!-- Cut marker -->
          <hr class="demo-cut-marker" />

          <!-- Noparse -->
          <p>
            Noparse:
            <span class="demo-noparse">[b]текст без форматирования[/b]</span>
          </p>

          <!-- Списки -->
          <ul class="demo-list">
            <li>Элемент списка 1</li>
            <li>Элемент списка 2</li>
          </ul>
          <ol class="demo-list">
            <li>Нумерованный 1</li>
            <li>Нумерованный 2</li>
          </ol>
        </div>
      </div>

      <!-- 12. Карточка отзыва о сайте -->
      <div class="example-group">
        <h3 class="example-title">Отзыв о сайте</h3>
        <div class="example-content">
          <div class="demo-review">
            <div class="demo-review-text">
              dm.am - отличный ресурс для настольных ролевых игр! Рекомендую
              всем любителям НРИ.
            </div>
            <div class="demo-review-author">
              <span class="demo-user-link">
                <span class="demo-user-avatar"></span>
                НРИшник
              </span>
            </div>
          </div>
          <p class="demo-hint">Скруглённые углы: $border-radius</p>
        </div>
      </div>

      <!-- 13. Оверлеи -->
      <div class="example-group">
        <h3 class="example-title">Оверлеи</h3>
        <div class="example-content">
          <div class="demo-overlays">
            <div class="demo-overlay-box demo-shade-box">
              <span>$shade-bg</span>
            </div>
            <div class="demo-overlay-box demo-overlay-bg-box">
              <span>$overlay-bg</span>
            </div>
            <div class="demo-overlay-box demo-hover-box">
              <span>$hover-overlay</span>
            </div>
            <div class="demo-overlay-box demo-active-box">
              <span>$active-overlay</span>
            </div>
          </div>
        </div>
      </div>

      <!-- 14. Лайтбокс -->
      <div class="example-group">
        <h3 class="example-title">Модальное окно</h3>
        <div class="example-content">
          <div class="demo-lightbox">
            <h2 class="demo-lightbox-title">Заголовок модалки</h2>
            <p>Содержимое модального окна.</p>
            <button class="demo-btn">Действие</button>
          </div>
          <p class="demo-hint">
            TheLightbox.vue: $bg-page, $border-radius, без границы
          </p>
        </div>
      </div>

      <!-- 15. Тени и инверсия -->
      <div class="example-group">
        <h3 class="example-title">Тени и инверсия</h3>
        <div class="example-content">
          <div class="demo-special">
            <div class="demo-shadow-box">
              <span>box-shadow с $shadow-color</span>
            </div>
            <div class="demo-invert-box">
              <div class="demo-invert-square"></div>
              <div class="demo-invert-square demo-inverted"></div>
              <span>$filter-invert (чёрный → белый в тёмной теме)</span>
            </div>
          </div>
        </div>
      </div>

      <!-- 16. Переключатель режимов редактора -->
      <div class="example-group">
        <h3 class="example-title">Переключатель режимов (варианты)</h3>
        <div class="example-content">
          <div class="toggle-demos">
            <!-- Вариант 2a: Табы - только иконки -->
            <div class="toggle-demo">
              <div class="toggle-demo-label">Иконки</div>
              <div class="toggle-tabs">
                <button
                  class="toggle-tab"
                  :class="{ active: toggleDemoMode === 'bbcode' }"
                  @click="setToggleMode('bbcode')"
                >
                  <span class="toggle-icon">&lt;/&gt;</span>
                </button>
                <button
                  class="toggle-tab"
                  :class="{ active: toggleDemoMode === 'visual' }"
                  @click="setToggleMode('visual')"
                >
                  <span class="toggle-icon"><i>Aa</i></span>
                </button>
              </div>
            </div>

            <!-- Вариант 2b: Табы - только текст -->
            <div class="toggle-demo">
              <div class="toggle-demo-label">Текст</div>
              <div class="toggle-tabs">
                <button
                  class="toggle-tab"
                  :class="{ active: toggleDemoMode === 'bbcode' }"
                  @click="setToggleMode('bbcode')"
                >
                  BBCode
                </button>
                <button
                  class="toggle-tab"
                  :class="{ active: toggleDemoMode === 'visual' }"
                  @click="setToggleMode('visual')"
                >
                  WYSIWYG
                </button>
              </div>
            </div>

            <!-- Вариант 2c: Табы - иконки и текст -->
            <div class="toggle-demo">
              <div class="toggle-demo-label">Иконки + текст</div>
              <div class="toggle-tabs">
                <button
                  class="toggle-tab"
                  :class="{ active: toggleDemoMode === 'bbcode' }"
                  @click="setToggleMode('bbcode')"
                >
                  <span class="toggle-icon">&lt;/&gt;</span> BBCode
                </button>
                <button
                  class="toggle-tab"
                  :class="{ active: toggleDemoMode === 'visual' }"
                  @click="setToggleMode('visual')"
                >
                  <span class="toggle-icon"><i>Aa</i></span> WYSIWYG
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Сравнение ВСЕХ иконок сайта -->
      <div class="example-group">
        <h3 class="example-title">ВСЕ иконки сайта</h3>
        <div class="example-content">
          <!-- ===== ГРУППА 1: Основные иконки ===== -->
          <p class="icon-row-label">Основные иконки (42px превью):</p>
          <div class="icons-inline icons-bordered">
            <svg
              viewBox="-0.7 -1.2 25.4 25.4"
              width="42"
              height="42"
              fill="none"
            >
              <path
                d="M12 20h9M16.5 3.5a2.12 2.12 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z"
                stroke="currentColor"
                stroke-width="2"
                stroke-linecap="round"
                stroke-linejoin="round"
              />
            </svg>
            <svg
              viewBox="0.3 0.57 23.35 23.35"
              width="42"
              height="42"
              fill="none"
            >
              <path
                d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"
                stroke="currentColor"
                stroke-width="2"
                stroke-linecap="round"
                stroke-linejoin="round"
              />
              <circle
                cx="12"
                cy="12"
                r="3"
                stroke="currentColor"
                stroke-width="2"
              />
            </svg>
            <svg
              viewBox="0.3 0.57 23.35 23.35"
              width="42"
              height="42"
              fill="none"
            >
              <path
                d="M3 12c0 0 4 5 9 5s9-5 9-5"
                stroke="currentColor"
                stroke-width="2"
                stroke-linecap="round"
              />
            </svg>
            <svg viewBox="-2 -2 28 28" width="42" height="42" fill="none">
              <path
                d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71"
                stroke="currentColor"
                stroke-width="2"
                stroke-linecap="round"
                stroke-linejoin="round"
              />
              <path
                d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71"
                stroke="currentColor"
                stroke-width="2"
                stroke-linecap="round"
                stroke-linejoin="round"
              />
            </svg>
            <svg
              viewBox="-2.27 -3.0 28.54 28.54"
              width="42"
              height="42"
              fill="none"
            >
              <path
                d="M3 6h18M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"
                stroke="currentColor"
                stroke-width="2"
                stroke-linecap="round"
              />
            </svg>
            <svg
              viewBox="-1.2 -0.75 26.4 26.4"
              width="42"
              height="42"
              fill="none"
            >
              <path
                d="M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z"
                stroke="currentColor"
                stroke-width="2"
              />
            </svg>
            <svg viewBox="-1.2 -0.75 26.4 26.4" width="42" height="42">
              <path
                d="M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z"
                fill="currentColor"
                stroke="currentColor"
                stroke-width="2"
              />
            </svg>
            <svg viewBox="3.1 3.1 17.8 17.8" width="42" height="42" fill="none">
              <path
                d="M18 6L6 18M6 6l12 12"
                stroke="currentColor"
                stroke-width="1.48"
                stroke-linecap="round"
              />
            </svg>
          </div>

          <!-- ===== ГРУППА 2: Стрелки навигации ===== -->
          <p class="icon-row-label">Стрелки (expand/collapse, scroll down):</p>
          <div class="icons-inline icons-bordered">
            <svg
              viewBox="0 0 12 12"
              width="42"
              height="42"
              fill="none"
              stroke="currentColor"
              stroke-width="1.5"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <path d="M2 8L6 4L10 8" />
            </svg>
            <svg
              viewBox="0 0 12 12"
              width="42"
              height="42"
              fill="none"
              stroke="currentColor"
              stroke-width="1.5"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <path d="M2 4L6 8L10 4" />
            </svg>
            <svg
              viewBox="0 0 24 24"
              width="42"
              height="42"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <path d="M7 13l5 5 5-5M7 6l5 5 5-5" />
            </svg>
          </div>

          <!-- ===== ГРУППА 3: View toggle ===== -->
          <p class="icon-row-label">View toggle (list/compact):</p>
          <div class="icons-inline icons-bordered">
            <svg viewBox="0 0 16 12" width="56" height="42" fill="currentColor">
              <circle cx="1.5" cy="1.5" r="1.5" />
              <rect x="5" y="0" width="11" height="2.5" />
              <circle cx="1.5" cy="6" r="1.5" />
              <rect x="5" y="4.75" width="11" height="2.5" />
              <circle cx="1.5" cy="10.5" r="1.5" />
              <rect x="5" y="9.25" width="11" height="2.5" />
            </svg>
            <svg viewBox="0 0 16 12" width="56" height="42" fill="currentColor">
              <rect x="0" y="0" width="16" height="2" />
              <rect x="0" y="3.33" width="16" height="2" />
              <rect x="0" y="6.67" width="16" height="2" />
              <rect x="0" y="10" width="16" height="2" />
            </svg>
          </div>

          <!-- ===== ГРУППА 4: Метки режимов редактора ===== -->
          <p class="icon-row-label">
            Метки режимов редактора (BBCode, WYSIWYG):
          </p>
          <div class="icons-inline icons-bordered">
            <span class="editor-mode-label">&lt;/&gt;</span>
            <span class="editor-mode-label"><i>Aa</i></span>
          </div>

          <!-- ===== ГРУППА 5: Отправка сообщения ===== -->
          <p class="icon-row-label">Отправка сообщения:</p>
          <div class="icons-inline icons-bordered">
            <svg
              viewBox="-2 -3 28 30"
              width="42"
              height="42"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linejoin="round"
            >
              <path d="M22 12L4 2v20L22 12z" />
              <path d="M22 12H4M4 2l8 10-8 10" />
            </svg>
          </div>

          <!-- ===== ГРУППА 6: Соцсети ===== -->
          <p class="icon-row-label">Соцсети (VK, Discord, YouTube):</p>
          <div class="icons-inline icons-bordered">
            <svg
              viewBox="-3 -3 30 30"
              width="42"
              height="42"
              fill="currentColor"
            >
              <path
                d="M15.684 0H8.316C1.592 0 0 1.592 0 8.316v7.368C0 22.408 1.592 24 8.316 24h7.368C22.408 24 24 22.408 24 15.684V8.316C24 1.592 22.391 0 15.684 0zm3.692 17.123h-1.744c-.66 0-.864-.525-2.05-1.727-1.033-1-1.49-1.135-1.744-1.135-.356 0-.458.102-.458.593v1.575c0 .424-.135.678-1.253.678-1.846 0-3.896-1.118-5.335-3.202C4.624 10.857 4 8.418 4 7.928c0-.254.102-.491.593-.491h1.744c.44 0 .61.203.78.678.847 2.489 2.27 4.674 2.853 4.674.22 0 .322-.102.322-.66V9.623c-.068-1.186-.695-1.287-.695-1.71 0-.203.17-.407.44-.407h2.744c.373 0 .508.203.508.643v3.473c0 .372.17.508.271.508.22 0 .407-.136.813-.542 1.253-1.406 2.143-3.574 2.143-3.574.119-.254.322-.491.763-.491h1.744c.525 0 .644.27.525.643-.22 1.017-2.354 4.031-2.354 4.031-.186.305-.254.44 0 .78.186.254.796.779 1.203 1.253.745.847 1.32 1.558 1.473 2.05.17.49-.085.744-.576.744z"
              />
            </svg>
            <svg viewBox="0 0 24 24" width="42" height="42" fill="currentColor">
              <path
                d="M20.317 4.37a19.791 19.791 0 0 0-4.885-1.515.074.074 0 0 0-.079.037c-.21.375-.444.864-.608 1.25a18.27 18.27 0 0 0-5.487 0 12.64 12.64 0 0 0-.617-1.25.077.077 0 0 0-.079-.037A19.736 19.736 0 0 0 3.677 4.37a.07.07 0 0 0-.032.027C.533 9.046-.32 13.58.099 18.057a.082.082 0 0 0 .031.057 19.9 19.9 0 0 0 5.993 3.03.078.078 0 0 0 .084-.028 14.09 14.09 0 0 0 1.226-1.994.076.076 0 0 0-.041-.106 13.107 13.107 0 0 1-1.872-.892.077.077 0 0 1-.008-.128 10.2 10.2 0 0 0 .372-.292.074.074 0 0 1 .077-.01c3.928 1.793 8.18 1.793 12.062 0a.074.074 0 0 1 .078.01c.12.098.246.198.373.292a.077.077 0 0 1-.006.127 12.299 12.299 0 0 1-1.873.892.077.077 0 0 0-.041.107c.36.698.772 1.362 1.225 1.993a.076.076 0 0 0 .084.028 19.839 19.839 0 0 0 6.002-3.03.077.077 0 0 0 .032-.054c.5-5.177-.838-9.674-3.549-13.66a.061.061 0 0 0-.031-.03zM8.02 15.33c-1.183 0-2.157-1.085-2.157-2.419 0-1.333.956-2.419 2.157-2.419 1.21 0 2.176 1.096 2.157 2.42 0 1.333-.956 2.418-2.157 2.418zm7.975 0c-1.183 0-2.157-1.085-2.157-2.419 0-1.333.955-2.419 2.157-2.419 1.21 0 2.176 1.096 2.157 2.42 0 1.333-.946 2.418-2.157 2.418z"
              />
            </svg>
            <svg viewBox="0 0 24 24" width="42" height="42" fill="currentColor">
              <path
                d="M23.498 6.186a3.016 3.016 0 0 0-2.122-2.136C19.505 3.545 12 3.545 12 3.545s-7.505 0-9.377.505A3.017 3.017 0 0 0 .502 6.186C0 8.07 0 12 0 12s0 3.93.502 5.814a3.016 3.016 0 0 0 2.122 2.136c1.871.505 9.376.505 9.376.505s7.505 0 9.377-.505a3.015 3.015 0 0 0 2.122-2.136C24 15.93 24 12 24 12s0-3.93-.502-5.814zM9.545 15.568V8.432L15.818 12l-6.273 3.568z"
              />
            </svg>
          </div>

          <!-- ===== ГРУППА 7: Поиск ===== -->
          <p class="icon-row-label">Поиск:</p>
          <div class="icons-inline icons-bordered">
            <svg
              viewBox="0 0 24 24"
              width="42"
              height="42"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
            >
              <circle cx="11" cy="11" r="8" />
              <path d="M21 21l-4.35-4.35" />
            </svg>
          </div>

          <!-- ===== ГРУППА 8: Специальные ===== -->
          <p class="icon-row-label">
            Специальные (empty envelope, deleted avatar):
          </p>
          <div class="icons-inline icons-bordered">
            <svg
              viewBox="0 0 64 64"
              width="56"
              height="42"
              fill="none"
              stroke="currentColor"
              stroke-width="1.5"
            >
              <rect x="8" y="12" width="48" height="36" rx="4" />
              <path d="M8 20l24 16 24-16" />
            </svg>
            <svg viewBox="0 0 56 56" width="42" height="42">
              <circle
                cx="28"
                cy="28"
                r="26"
                fill="none"
                stroke="currentColor"
                stroke-width="1"
                stroke-dasharray="4 2"
              />
              <path
                d="M18 18l20 20M38 18l-20 20"
                stroke="currentColor"
                stroke-width="1.5"
              />
            </svg>
          </div>
        </div>
      </div>
    </section>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/Tables"
@import "src/assets/styles/Inputs"
@import "src/assets/styles/BbcodeContent"

.theme-colors-page
  // no width limit

.page-title
  color: $heading
  font-size: $title-font-size
  font-weight: bold
  text-transform: uppercase
  letter-spacing: 0.5px
  margin: $medium 0 $small

.section-title
  color: $heading
  font-size: $font-size
  font-weight: bold
  text-transform: uppercase
  letter-spacing: 0.5px
  margin: 0 0 $medium
  padding-bottom: $small
  border-bottom: 1px solid
  border-color: $border

// Variables section
.vars-section
  margin-bottom: $big

.vars-grid
  display: grid
  grid-template-columns: repeat(3, 1fr)
  gap: $medium

.vars-group
  background-color: $bg-element
  padding: $medium

  h4
    margin: 0 0 $small
    color: $heading
    font-size: $font-size

.var-item
  display: flex
  justify-content: space-between
  padding: $tiny 0
  font-size: $secondary-font-size
  border-bottom: 1px dotted $border

  &:last-child
    border-bottom: none

  span
    color: $text-muted

// Shared code styling
.var-item code, .swatch-info code
  font-family: $code-font
  font-size: $secondary-font-size
  color: $link

// Typography demos
.demo-typography
  display: flex
  flex-direction: column
  gap: $tiny

  p
    margin: 0

.demo-font-menu
  font-size: $menu-font-size

.demo-font-title
  font-size: $title-font-size

.demo-font-base
  font-size: $font-size

.demo-font-secondary
  font-size: $secondary-font-size

.demo-font-tertiary
  font-size: $tertiary-font-size

// Mixin demos
.demo-mixin-input
  +input

.demo-mixin-button
  +button
  margin-right: $small

.demo-mixin-badge
  +badge
  margin-right: $small

.demo-mixin-table
  +table

  th, td
    +table-columns
    padding: $small

  th
    +table-header

  tr
    +table-row

// Swatches
.swatches-section
  margin-bottom: $big

.swatch-group
  margin-bottom: $small
  border: 1px solid $border

.swatch-group-header
  width: 100%
  display: flex
  justify-content: space-between
  align-items: center
  padding: $small $medium
  border: none
  cursor: pointer
  text-align: left
  font-family: inherit
  font-size: $font-size
  background-color: $bg-element
  color: $text

  &:hover
    background-color: $bg-element-accent

.swatch-group-name
  color: $heading

.swatch-group-toggle
  color: $text-muted
  font-size: 16px

.swatch-group-content
  padding: $small $medium
  background-color: $bg-page

.swatch-item
  display: flex
  align-items: center
  gap: $medium
  padding: $tiny 0

.swatch-color
  width: 32px
  height: 32px
  flex-shrink: 0
  border: 1px solid $border

.swatch-info
  display: flex
  flex-direction: column
  gap: 2px

.swatch-usage
  font-size: $secondary-font-size
  color: $text-muted

// Examples
.examples-section
  margin-bottom: $big

.example-group
  margin-bottom: $medium
  background-color: $bg-element
  padding: $medium

.example-title
  color: $heading
  font-size: $font-size
  font-weight: bold
  text-transform: uppercase
  letter-spacing: 0.5px
  margin: 0 0 $small

.example-content
  color: $text

.demo-hint
  margin-top: $small
  font-size: $secondary-font-size
  color: $text-muted
  font-style: italic

// Headings
.demo-page-title, .demo-block-title, .demo-sidebar-title
  font-weight: bold
  text-transform: uppercase
  letter-spacing: 0.5px
  margin: 0 0 $small

.demo-page-title
  color: $heading
  font-size: $title-font-size

.demo-block-title
  color: $heading
  font-size: $font-size

.demo-sidebar-title
  color: $heading-alt
  font-size: $font-size
  margin: 0

// Text
.demo-text, .demo-text-muted, .demo-text-meta, .demo-text-green, .demo-text-red
  margin: $tiny 0

.demo-text
  color: $text
.demo-text-muted
  color: $text-muted
.demo-text-meta
  color: $text-meta
.demo-text-green
  color: $accent-green
.demo-text-red
  color: $accent-red

.demo-text-on-green, .demo-text-on-red
  margin: $tiny 0
  padding: $tiny $small

.demo-text-on-green
  color: $text-on-green
  background: linear-gradient($highlight-overlay-green, $highlight-overlay-green), $bg-element

.demo-text-on-red
  color: $text-on-red
  background: linear-gradient($highlight-overlay-red, $highlight-overlay-red), $bg-element

// Links
.demo-link, .demo-link-green, .demo-link-red, .demo-link-nav
  text-decoration: none
  &:hover
    text-decoration: underline

.demo-link
  color: $link
  &:hover
    color: $link-hover

.demo-link-green
  color: $accent-green
  &:hover
    color: $accent-green-hover

.demo-link-red
  color: $accent-red
  &:hover
    color: $accent-red-hover

.demo-link-nav
  color: $link-nav
  &:hover
    color: $link-nav-hover
    text-decoration: none

// Buttons
.demo-buttons
  display: flex
  flex-wrap: wrap
  gap: $small

.demo-btn
  +button

// Badges
.demo-badges
  display: flex
  flex-wrap: wrap
  gap: $small
  align-items: center

// Badge - matches +badge() mixin from Inputs.sass (no border!)
.demo-badge
  +badge

.demo-user-badge
  color: $text-muted

.badge-admin, .badge-mod
  color: $accent-green
  font-weight: bold

.demo-rating
  color: $text

.rating-positive
  font-weight: bold
  color: $accent-green

.rating-negative
  font-weight: bold
  color: $accent-red

// Form
.demo-form
  display: grid
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr))
  gap: $small

.demo-form-field
  display: flex
  flex-direction: column
  gap: $tiny

  label
    font-size: $secondary-font-size
    color: $text

// Input - matches +input() mixin from Inputs.sass
.demo-input
  +input

.demo-textarea
  resize: vertical
  min-height: 60px

.demo-checkboxes
  display: flex
  flex-wrap: wrap
  gap: $medium
  margin-top: $small

  label
    display: flex
    align-items: center
    gap: $tiny
    cursor: pointer

    &:has(input:disabled)
      color: $text-muted
      cursor: not-allowed

// Progress - matches ProgressBar.vue
.demo-progress
  position: relative
  overflow: hidden
  margin: $small 0
  padding: $minor
  border-radius: $border-radius
  background-color: $progress-bg

.demo-progress-fill
  position: absolute
  top: 0
  left: 0
  bottom: 0
  background-color: $progress-fill
  // NO border-radius on fill - only container has it

.demo-progress-text
  position: relative
  color: $text

// Table
.demo-table
  width: 100%
  border-collapse: collapse
  +table

  th, td
    padding: $small
    border: 1px solid $border

  th
    +table-header
    text-align: left

  td
    background-color: $bg-element

// Paging - matches ThePaging.vue
.demo-paging
  text-align: center

.paging-link
  display: inline-block
  min-width: $grid-step * 7
  padding: $minor 0
  border-bottom: 1px solid
  border-bottom-color: $link
  text-align: center
  color: $link
  text-decoration: none

  &:hover
    border-bottom-color: $link-hover
    color: $link-hover
    text-decoration: none

// Backgrounds
.demo-backgrounds
  display: grid
  grid-template-columns: repeat(auto-fill, minmax(100px, 1fr))
  gap: $small

.demo-bg
  padding: $small
  text-align: center
  font-size: $secondary-font-size
  border: 1px solid $border
  color: $text

.demo-bg-page
  background-color: $bg-page

.demo-bg-element
  background-color: $bg-element

.demo-bg-element-accent
  background-color: $bg-element-accent

.demo-bg-highlight-blue
  background-color: $bg-highlight-blue

.demo-bg-highlight-yellow
  background-color: $bg-highlight-yellow

.demo-bg-highlight-green
  background-color: $bg-highlight-green

.demo-bg-highlight-red
  background-color: $bg-highlight-red

// BBCode
.demo-bbcode-content
  display: flex
  flex-direction: column
  gap: $small

// Quote/Mod/Warning blocks - matches _BbcodeContent.sass
// Uses +overlay-bg mixin (::before for overlay) + border-left for colored strip
.demo-quote, .demo-mod, .demo-warning
  margin: $tiny 0
  padding: 2px 6px
  background-color: $bg-element

  p
    margin: 0

  cite, .demo-mod-author, .demo-warning-header
    display: block
    font-weight: 600
    font-style: italic
    margin-bottom: $tiny

.demo-quote
  +overlay-bg($highlight-overlay-blue)
  border-left: 10px solid $border-accent-blue
  color: $text

.demo-mod
  +overlay-bg($highlight-overlay-green)
  border-left: 10px solid $border-accent-green
  color: $text-on-green

.demo-warning
  +overlay-bg($highlight-overlay-red)
  border-left: 10px solid $border-accent-red
  color: $text-on-red

.demo-spoiler-container,
.demo-nsfw-container
  margin: $tiny 0

// Spoiler/NSFW toggles - matches _BbcodeContent.sass
.demo-spoiler-head, .demo-nsfw-head
  display: block
  cursor: pointer
  text-decoration: none
  &:hover
    text-decoration: underline

.demo-spoiler-head
  color: $link
  &:hover
    color: $link-hover

.demo-nsfw-head
  color: $accent-red
  &:hover
    color: $accent-red-hover

.demo-spoiler, .demo-nsfw
  position: relative
  padding: $tiny $small
  margin-top: $tiny
  border: 1px dashed $border
  background-color: $bg-highlight-yellow

.demo-nsfw-overlay
  position: absolute
  top: 50%
  left: $minor
  right: $minor
  transform: translateY(-50%)
  z-index: 1
  display: flex
  align-items: center
  justify-content: center
  background-color: $nsfw-overlay
  backdrop-filter: blur(8px)
  cursor: pointer

.demo-nsfw-warning
  color: $accent-red
  text-align: center
  font-size: $font-size
  line-height: 1.2
  font-weight: bold
  padding: 0 $small

// Private text - matches _BbcodeContent.sass
.demo-private
  color: $text-on-green
  background: linear-gradient($highlight-overlay-green, $highlight-overlay-green), $bg-element
  padding: 0 2px
  border-radius: 2px

.demo-code
  margin: 0
  font-family: $code-font
  font-size: 16px
  white-space: pre-wrap
  color: $text

.demo-cut-marker
  border: none
  border-top: 1px dashed $border
  margin: $small 0

.demo-noparse
  font-family: $code-font

.demo-list
  margin: $small 0
  padding-left: $big

  li
    margin: 2px 0

// Review card - matches WebsiteReviewItem.vue
.demo-review
  max-width: 400px

.demo-review-text
  position: relative
  padding: $medium
  margin-bottom: $small
  background-color: $bg-highlight-green
  color: $text-on-green
  border-radius: $border-radius

  // Speech bubble arrow
  &:after
    position: absolute
    top: 100%
    left: $small
    content: ''
    border: solid $minor transparent
    border-top-color: $bg-highlight-green
    border-left-color: $bg-highlight-green

.demo-review-author
  margin-left: $small

.demo-user-link
  display: inline-flex
  align-items: center
  gap: $tiny
  color: $link

.demo-user-avatar
  display: inline-block
  width: $medium
  height: $medium
  border-radius: 50%
  background: url('@/assets/images/userpic.png') 0 0 no-repeat
  background-size: cover

// Overlays
.demo-overlays
  display: flex
  gap: $medium

.demo-overlay-box
  width: 150px
  height: 80px
  display: flex
  align-items: center
  justify-content: center
  font-size: $secondary-font-size
  border: 1px solid $border

.demo-shade-box
  background-color: $shade-bg
  color: $shade-text

.demo-overlay-bg-box
  background-color: $overlay-bg
  color: $shade-text

.demo-hover-box
  background-color: $hover-overlay

.demo-active-box
  background-color: $active-overlay

// Lightbox - matches TheLightbox.vue (no border!)
.demo-lightbox
  max-width: 300px
  padding: $medium
  border-radius: $border-radius
  background-color: $bg-page
  // NO border in real component!

.demo-lightbox-title
  margin: 0 0 $small
  color: $heading
  font-size: $title-font-size

.demo-lightbox p
  margin: 0 0 $medium
  color: $text

// Special: shadows and inversion
.demo-special
  display: flex
  flex-direction: column
  gap: $medium

.demo-shadow-box
  padding: $medium
  background-color: $bg-page
  box-shadow: 0 4px 12px $shadow-color
  border: 1px solid $border

.demo-invert-box
  display: flex
  align-items: center
  gap: $small

.demo-invert-square
  width: 32px
  height: 32px
  background-color: #000
  border: 1px solid $border

.demo-inverted
  filter: var(--filter-invert)

// Toggle demos - Mode switch variants
.toggle-demos
  display: grid
  grid-template-columns: repeat(3, 1fr)
  gap: $medium

.toggle-demo
  display: flex
  flex-direction: column
  gap: $small
  align-items: flex-start
  padding: $small
  background-color: $bg-page
  border: 1px solid $border

.toggle-demo-label
  font-size: $secondary-font-size
  color: $text-muted

// 1. Segmented buttons
.toggle-segmented
  display: flex
  border: 1px solid $border

.toggle-seg-btn
  flex: 1
  padding: $minor $small
  border: none
  background-color: $bg-element
  color: $text-muted
  font-family: inherit
  font-size: $font-size
  cursor: pointer
  transition: all 0.15s

  &:not(:last-child)
    border-right: 1px solid $border

  &:hover:not(.active)
    background-color: $bg-element-accent
    filter: brightness($hover-brightness)

  &.active
    background-color: $bg-element-accent
    filter: brightness($hover-brightness)
    cursor: default

// 2. Tabs with icons
.toggle-tabs
  display: flex
  gap: $tiny
  border-bottom: 2px solid $border

.toggle-tab
  padding: $minor $small
  border: none
  background: none
  color: $text-muted
  font-family: inherit
  font-size: $font-size
  cursor: pointer
  position: relative
  display: flex
  align-items: center
  gap: $tiny
  transition: all 0.15s

  &::after
    content: ''
    position: absolute
    bottom: -2px
    left: 0
    right: 0
    height: 2px
    background-color: transparent
    transition: background-color 0.15s

  &:hover:not(.active)
    filter: brightness($hover-brightness)

  &.active
    filter: brightness($hover-brightness)
    cursor: default
    &::after
      background-color: $text-muted

.toggle-icon
  font-style: normal
  font-weight: bold
  font-size: $secondary-font-size

// 3. Dropdown
.toggle-dropdown
  position: relative

.toggle-dropdown-btn
  +button
  display: flex
  align-items: center
  gap: $small
  width: 100%
  justify-content: space-between

.toggle-arrow
  font-size: $tertiary-font-size

// 4. Big slider with labels
.toggle-slider-big
  display: flex
  align-items: center
  gap: $small

.toggle-slider-label
  font-size: $font-size
  color: $text-muted
  cursor: pointer

  &:hover
    filter: brightness($hover-brightness)

  &.active
    filter: brightness($hover-brightness)

.toggle-slider-track
  position: relative
  width: 48px
  height: 24px
  background-color: $bg-element-accent
  border: 1px solid $border
  border-radius: 12px
  cursor: pointer

.toggle-slider-thumb
  position: absolute
  top: 2px
  left: 2px
  width: 18px
  height: 18px
  background-color: $accent-green
  border-radius: 50%
  transition: left 0.2s

  &.right
    left: 26px

// 5. Radio buttons
.toggle-radio
  display: flex
  gap: $medium

.toggle-radio-item
  display: flex
  align-items: center
  gap: $tiny
  cursor: pointer
  font-size: $font-size
  color: $text-muted

  input[type="radio"]
    accent-color: $text-muted

  &:hover
    filter: brightness($hover-brightness)

  &:has(input:checked)
    filter: brightness($hover-brightness)

// 6. Current (small toggle)
.toggle-current
  display: flex
  align-items: center
  gap: $tiny

.toggle-current-label
  font-size: $tertiary-font-size
  color: $text-muted
  font-style: normal
  cursor: pointer

  &:hover
    filter: brightness($hover-brightness)

  &.active
    filter: brightness($hover-brightness)

.toggle-current-slider
  position: relative
  width: 32px
  height: 16px
  background-color: $bg-element-accent
  border: 1px solid $border
  border-radius: 8px
  cursor: pointer

.toggle-current-thumb
  position: absolute
  top: 2px
  left: 2px
  width: 10px
  height: 10px
  background-color: $accent-green
  border-radius: 50%
  transition: left 0.2s

  &.right
    left: 18px

// Header icons comparison
.icon-row-label
  font-size: $secondary-font-size
  color: $text-muted
  margin: $small 0 $tiny

.icons-inline
  display: flex
  align-items: flex-end
  gap: $small
  padding: $small
  background-color: $bg-page
  color: $text-muted
  width: fit-content

  &.icons-bordered svg,
  &.icons-bordered .editor-mode-label
    border: 1px solid $border
    background-color: $bg-element
    padding: $minor

.deleted-avatar-demo
  background-color: $bg-element-accent
  border-radius: 50%

.editor-mode-label
  display: flex
  align-items: center
  justify-content: center
  width: 42px
  height: 42px
  font-size: 18px
  color: $text-muted
  border: 1px solid $border
  background-color: $bg-element
</style>
