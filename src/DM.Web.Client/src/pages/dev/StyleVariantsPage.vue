<script setup lang="ts">
/**
 * StyleVariantsPage — DEV-ONLY mockup catalog for the pending style
 * decisions (owner picks variants by number, then the page is removed):
 *   1. Statistics-card overlay alpha (slightly smaller candidates).
 *   2. "Отправить"-style primary buttons (currently link-colored).
 *   3. "Аккаунт не найден" dialogs and EmptyState without centered text.
 *   4. Info sections (registration notice and alike) without muted gray.
 *
 * Everything renders with real components where practical (StatBoard,
 * Button, EmptyState, DialogTitle) so the variants are judged in true
 * chrome. Local style probes are mockup-only: the picked value becomes a
 * theme variable at implementation time (no hand-picked colors in prod).
 */
import DialogTitle from "@/shared/ui/Layout/DialogTitle.vue";
import { EmptyState } from "@/shared/ui/EmptyState";
import { StatBoard } from "@/features/leaderboard";
import type { LeaderboardEntry } from "@/shared/api/models/community";

const players: LeaderboardEntry[] = [
  { rank: 1, entityId: "p1", name: "SolohinLex", score: 847 },
  { rank: 2, entityId: "p2", name: "Astrellan", score: 512 },
  { rank: 3, entityId: "p3", name: "Miriamel", score: 386 },
  { rank: 4, entityId: "p4", name: "GrayWanderer", score: 291 },
  { rank: 5, entityId: "p5", name: "Ночная Сова", score: 154 },
];

const games: LeaderboardEntry[] = [
  {
    rank: 1,
    entityId: "g1",
    publicId: "tenig",
    name: "Тени старого города",
    score: 1240,
  },
  {
    rank: 2,
    entityId: "g2",
    publicId: "zamok",
    name: "Замок на болотах",
    score: 918,
  },
  {
    rank: 3,
    entityId: "g3",
    publicId: "dorog",
    name: "Дорога на север",
    score: 640,
  },
  {
    rank: 4,
    entityId: "g4",
    publicId: "korab",
    name: "Последний корабль",
    score: 402,
  },
  {
    rank: 5,
    entityId: "g5",
    publicId: "gorod",
    name: "Город тысячи ворот",
    score: 287,
  },
];

const blogs: LeaderboardEntry[] = [
  {
    rank: 1,
    entityId: "b1",
    publicId: "hronk",
    name: "Хроники мастерской",
    score: 356,
  },
  {
    rank: 2,
    entityId: "b2",
    publicId: "zapis",
    name: "Записки рассказчика",
    score: 244,
  },
  {
    rank: 3,
    entityId: "b3",
    publicId: "puter",
    name: "Путевые заметки",
    score: 187,
  },
  {
    rank: 4,
    entityId: "b4",
    publicId: "arhiv",
    name: "Архив сюжетов",
    score: 121,
  },
  {
    rank: 5,
    entityId: "b5",
    publicId: "svito",
    name: "Свиток недели",
    score: 76,
  },
];

const overlayVariants = [
  { num: "1.0", probe: "probe-050", note: "текущее: $overlay-subtle, 0.05" },
  { num: "1.1", probe: "probe-040", note: "0.04" },
  { num: "1.2", probe: "probe-035", note: "0.035" },
  { num: "1.3", probe: "probe-030", note: "0.03" },
  {
    num: "1.4",
    probe: "probe-024",
    note: "0.024 (= $bg-element-overlay светлой темы)",
  },
];

const buttonVariants = [
  {
    num: "2.0",
    cls: "v-current",
    note: "текущее: заливка $link, белый текст (цвета ссылок)",
  },
  {
    num: "2.1",
    cls: "v-bold",
    note: "обычный +button, primary выделен только жирным текстом",
  },
  {
    num: "2.2",
    cls: "v-strong",
    note: "+button с усиленной серой заливкой ($control-bg-hover-overlay) и жирным",
  },
  {
    num: "2.3",
    cls: "v-invert",
    note: "инверсия: заливка $text, текст цвета фона (в обеих темах контраст автоматом)",
  },
  {
    num: "2.4",
    cls: "v-accent",
    note: "заливка $bg-element-accent (идиома активного сегмента) и жирный",
  },
];

// --- Секция 5: календарные реплики (июль 2026, сегодня 22, выбрано 15) ---

const CAL_WEEKDAYS = ["Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс"];
const CAL_MONTHS_SHORT = [
  "Янв",
  "Фев",
  "Мар",
  "Апр",
  "Май",
  "Июн",
  "Июл",
  "Авг",
  "Сен",
  "Окт",
  "Ноя",
  "Дек",
];

interface CalDay {
  label: number;
  inMonth: boolean;
  isToday: boolean;
  isSelected: boolean;
}

// July 2026: the 1st is Wednesday -> two leading June days (29, 30),
// 31 days, then trailing August days up to the fixed 42 cells.
const calDays: CalDay[] = [];
for (const d of [29, 30]) {
  calDays.push({ label: d, inMonth: false, isToday: false, isSelected: false });
}
for (let d = 1; d <= 31; d++) {
  calDays.push({
    label: d,
    inMonth: true,
    isToday: d === 22,
    isSelected: d === 15,
  });
}
for (let d = 1; calDays.length < 42; d++) {
  calDays.push({ label: d, inMonth: false, isToday: false, isSelected: false });
}

const calYears = Array.from({ length: 12 }, (_, i) => 2015 + i);
</script>

<template>
  <page-title>Мокапы: стиль</page-title>

  <!-- ================================================================ -->
  <BlockTitle>Секция 1. Оверлей карточек статистики</BlockTitle>
  <p class="section-intro">
    Сейчас карточки внутри дайджест-топика красятся переменной
    <span class="code">$overlay-subtle</span>: светлая тема rgba(0, 0, 0, 0.05),
    темная rgba(255, 255, 255, 0.05). Ниже варианты с чуть меньшей альфой (в
    темной теме та же альфа на белом). Выбранное значение станет переменной
    темы.
  </p>

  <div v-for="v in overlayVariants" :key="v.num" class="variant">
    <p class="variant-label">
      <strong>{{ v.num }}.</strong> {{ v.note }}
    </p>
    <div class="topic-replica" :class="v.probe">
      <div class="boards-grid">
        <StatBoard
          title="Лучший игрок по сумме оценок"
          :entries="players"
          kind="player"
        />
        <StatBoard
          title="Лучшая игра по сумме оценок"
          :entries="games"
          kind="game"
        />
        <StatBoard title="Лучший блог по лайкам" :entries="blogs" kind="blog" />
      </div>
    </div>
  </div>

  <!-- ================================================================ -->
  <BlockTitle>Секция 2. Кнопки "Отправить"</BlockTitle>
  <p class="section-intro">
    Сейчас primary-кнопка форм заливается цветом ссылок ($link). Варианты ниже
    показаны на реальной полосе футера формы ($bg-element-accent): активная,
    заблокированная и соседняя "Отмена" (обычный +button, как сейчас).
    Ховер-состояния уточняются при внедрении.
  </p>

  <div v-for="v in buttonVariants" :key="v.num" class="variant">
    <p class="variant-label">
      <strong>{{ v.num }}.</strong> {{ v.note }}
    </p>
    <div class="controls-replica">
      <button type="button" :class="v.cls">Отправить</button>
      <button type="button" :class="v.cls" disabled>Отправить</button>
      <button type="button" class="v-plain">Отмена</button>
    </div>
  </div>

  <!-- ================================================================ -->
  <BlockTitle>Секция 3. "Аккаунт не найден" и empty-состояния</BlockTitle>
  <p class="section-intro">
    Диалог восстановления доступа и общий EmptyState сейчас центрируют текст.
    Варианты без центрирования (рамка вокруг диалога здесь только для мокапа).
  </p>

  <div class="variant">
    <p class="variant-label">
      <strong>3.0.</strong> Текущее: текст по центру, кнопка на всю ширину
    </p>
    <div class="dialog-replica center-text">
      <DialogTitle>Аккаунт не найден</DialogTitle>
      <p class="main-text">
        Аккаунта с почтой <strong>user@example.com</strong> не существует.
      </p>
      <p class="link-row">
        <button type="button" class="ilink-normal">
          Попробовать другую почту?
        </button>
      </p>
      <Button type="button" class="full-width-btn">Закрыть</Button>
    </div>
  </div>

  <div class="variant">
    <p class="variant-label">
      <strong>3.1.</strong> Все слева, кнопка обычной ширины
    </p>
    <div class="dialog-replica">
      <DialogTitle>Аккаунт не найден</DialogTitle>
      <p class="main-text">
        Аккаунта с почтой <strong>user@example.com</strong> не существует.
      </p>
      <p class="link-row">
        <button type="button" class="ilink-normal">
          Попробовать другую почту?
        </button>
      </p>
      <Button type="button">Закрыть</Button>
    </div>
  </div>

  <div class="variant">
    <p class="variant-label">
      <strong>3.2.</strong> Слева, ссылка продолжает текст строкой ниже, кнопка
      обычной ширины
    </p>
    <div class="dialog-replica">
      <DialogTitle>Аккаунт не найден</DialogTitle>
      <p class="main-text">
        Аккаунта с почтой <strong>user@example.com</strong> не существует.
        Проверьте адрес или
        <button type="button" class="ilink">попробуйте другую почту</button>.
      </p>
      <Button type="button">Закрыть</Button>
    </div>
  </div>

  <div class="variant">
    <p class="variant-label">
      <strong>3.3.</strong> Текущий EmptyState: центр, иконка 64px сверху
    </p>
    <div class="empty-context">
      <EmptyState
        icon="envelope"
        title="Нет переписок"
        hint="Найдите собеседника через поиск выше"
      />
    </div>
  </div>

  <div class="variant">
    <p class="variant-label">
      <strong>3.4.</strong> Слева, иконка 32px слева от текста
    </p>
    <div class="empty-context">
      <div class="empty-left empty-left--icon">
        <svg class="empty-left-icon" viewBox="0 0 24 24" aria-hidden="true">
          <path
            fill="currentColor"
            d="M4 5h16a1 1 0 0 1 1 1v12a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1Zm1 2.4V17h14V7.4l-7 5.25L5 7.4ZM6.7 7l5.3 3.97L17.3 7H6.7Z"
          />
        </svg>
        <div>
          <div class="empty-left-title">Нет переписок</div>
          <div class="empty-left-hint">
            Найдите собеседника через поиск выше
          </div>
        </div>
      </div>
    </div>
  </div>

  <div class="variant">
    <p class="variant-label"><strong>3.5.</strong> Слева, без иконки</p>
    <div class="empty-context">
      <div class="empty-left">
        <div>
          <div class="empty-left-title">Нет переписок</div>
          <div class="empty-left-hint">
            Найдите собеседника через поиск выше
          </div>
        </div>
      </div>
    </div>
  </div>

  <div class="variant">
    <p class="variant-label">
      <strong>3.6.</strong> Слева, без иконки, на подложке $overlay-subtle
    </p>
    <div class="empty-context">
      <div class="empty-left empty-left--surface">
        <div>
          <div class="empty-left-title">Нет переписок</div>
          <div class="empty-left-hint">
            Найдите собеседника через поиск выше
          </div>
        </div>
      </div>
    </div>
  </div>

  <!-- ================================================================ -->
  <BlockTitle>Секция 4. Инфо-секции</BlockTitle>
  <p class="section-intro">
    Референс: нотис на регистрации (рамка со скруглением, серый текст вторичного
    размера). Варианты в ширине диалога регистрации; боксовые варианты без
    скругления (скругление только у контролов).
  </p>

  <div class="variant">
    <p class="variant-label">
      <strong>4.0.</strong> Текущее: рамка со скруглением, $text-muted,
      вторичный размер
    </p>
    <div class="dialog-width">
      <div class="info-replica info-current">
        <p><strong>Создание дополнительных аккаунтов запрещено.</strong></p>
        <p>
          Если вы утратили доступ к аккаунту, воспользуйтесь
          <button type="button" class="ilink">восстановлением доступа</button>
          или обратитесь в
          <button type="button" class="ilink">поддержку</button>.
        </p>
      </div>
    </div>
  </div>

  <div class="variant">
    <p class="variant-label">
      <strong>4.1.</strong> Рамка, обычный цвет текста, вторичный размер
    </p>
    <div class="dialog-width">
      <div class="info-replica info-border">
        <p><strong>Создание дополнительных аккаунтов запрещено.</strong></p>
        <p>
          Если вы утратили доступ к аккаунту, воспользуйтесь
          <button type="button" class="ilink">восстановлением доступа</button>
          или обратитесь в
          <button type="button" class="ilink">поддержку</button>.
        </p>
      </div>
    </div>
  </div>

  <div class="variant">
    <p class="variant-label">
      <strong>4.2.</strong> Подложка $overlay-subtle без рамки, обычный цвет
    </p>
    <div class="dialog-width">
      <div class="info-replica info-overlay">
        <p><strong>Создание дополнительных аккаунтов запрещено.</strong></p>
        <p>
          Если вы утратили доступ к аккаунту, воспользуйтесь
          <button type="button" class="ilink">восстановлением доступа</button>
          или обратитесь в
          <button type="button" class="ilink">поддержку</button>.
        </p>
      </div>
    </div>
  </div>

  <div class="variant">
    <p class="variant-label">
      <strong>4.3.</strong> Подложка $highlight-overlay-blue (существующая
      информационная подсветка), обычный цвет
    </p>
    <div class="dialog-width">
      <div class="info-replica info-highlight">
        <p><strong>Создание дополнительных аккаунтов запрещено.</strong></p>
        <p>
          Если вы утратили доступ к аккаунту, воспользуйтесь
          <button type="button" class="ilink">восстановлением доступа</button>
          или обратитесь в
          <button type="button" class="ilink">поддержку</button>.
        </p>
      </div>
    </div>
  </div>

  <div class="variant">
    <p class="variant-label">
      <strong>4.4.</strong> Без бокса: обычный текст в потоке, отделен отступом
    </p>
    <div class="dialog-width">
      <div class="info-replica info-plain">
        <p><strong>Создание дополнительных аккаунтов запрещено.</strong></p>
        <p>
          Если вы утратили доступ к аккаунту, воспользуйтесь
          <button type="button" class="ilink">восстановлением доступа</button>
          или обратитесь в
          <button type="button" class="ilink">поддержку</button>.
        </p>
      </div>
    </div>
  </div>

  <div class="variant">
    <p class="variant-label">
      <strong>4.5.</strong> Рамка, обычный цвет и основной размер шрифта
    </p>
    <div class="dialog-width">
      <div class="info-replica info-border info-fullsize">
        <p><strong>Создание дополнительных аккаунтов запрещено.</strong></p>
        <p>
          Если вы утратили доступ к аккаунту, воспользуйтесь
          <button type="button" class="ilink">восстановлением доступа</button>
          или обратитесь в
          <button type="button" class="ilink">поддержку</button>.
        </p>
      </div>
    </div>
  </div>

  <!-- ================================================================ -->
  <BlockTitle>Секция 5. Календарь: клик по месяцу и году</BlockTitle>
  <p class="section-intro">
    В календарях выбора дня (фильтры, чат, профиль) заголовок "Июль 2026" сейчас
    статичный: до другого года можно дойти только листая по месяцу. Варианты
    быстрого перехода ниже; состояния одного поповера показаны рядом, ховеры
    живые. Сетки месяцев и лет уже есть в пикере статистики, реализация
    переиспользует их; копируемость шапки сохраняется.
  </p>

  <div class="variant">
    <p class="variant-label">
      <strong>5.0.</strong> Текущее: заголовок статичный
    </p>
    <div class="cal-row">
      <div class="cal-state">
        <div class="cal">
          <div class="cal-header">
            <button type="button" class="cal-nav">‹</button>
            <span class="cal-title">Июль 2026</span>
            <button type="button" class="cal-nav">›</button>
          </div>
          <div class="cal-weekdays">
            <span v-for="w in CAL_WEEKDAYS" :key="w" class="cal-weekday">{{
              w
            }}</span>
          </div>
          <div class="cal-days">
            <button
              v-for="(d, i) in calDays"
              :key="i"
              type="button"
              class="cal-day"
              :class="{
                'out-month': !d.inMonth,
                today: d.isToday,
                selected: d.isSelected,
              }"
            >
              {{ d.label }}
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>

  <div class="variant">
    <p class="variant-label">
      <strong>5.1.</strong> Заголовок-кнопка, дрилл в два уровня: клик по "Июль
      2026" открывает сетку месяцев, в ее шапке клик по "2026" открывает сетку
      лет; выбор возвращает на уровень ниже
    </p>
    <div class="cal-row">
      <div class="cal-state">
        <div class="cal">
          <div class="cal-header">
            <button type="button" class="cal-nav">‹</button>
            <button type="button" class="cal-title-btn">Июль 2026</button>
            <button type="button" class="cal-nav">›</button>
          </div>
          <div class="cal-weekdays">
            <span v-for="w in CAL_WEEKDAYS" :key="w" class="cal-weekday">{{
              w
            }}</span>
          </div>
          <div class="cal-days">
            <button
              v-for="(d, i) in calDays"
              :key="i"
              type="button"
              class="cal-day"
              :class="{
                'out-month': !d.inMonth,
                today: d.isToday,
                selected: d.isSelected,
              }"
            >
              {{ d.label }}
            </button>
          </div>
        </div>
        <span class="cal-state-caption">дни: весь заголовок кликается</span>
      </div>
      <div class="cal-state">
        <div class="cal">
          <div class="cal-header">
            <button type="button" class="cal-nav">‹</button>
            <button type="button" class="cal-title-btn">2026</button>
            <button type="button" class="cal-nav">›</button>
          </div>
          <div class="cal-cells">
            <button
              v-for="(m, i) in CAL_MONTHS_SHORT"
              :key="m"
              type="button"
              class="cal-cell"
              :class="{ selected: i === 6 }"
            >
              {{ m }}
            </button>
          </div>
        </div>
        <span class="cal-state-caption">месяцы: "2026" кликается дальше</span>
      </div>
      <div class="cal-state">
        <div class="cal">
          <div class="cal-header">
            <button type="button" class="cal-nav">‹</button>
            <span class="cal-title">2015–2026</span>
            <button type="button" class="cal-nav">›</button>
          </div>
          <div class="cal-cells">
            <button
              v-for="y in calYears"
              :key="y"
              type="button"
              class="cal-cell"
              :class="{ selected: y === 2026 }"
            >
              {{ y }}
            </button>
          </div>
        </div>
        <span class="cal-state-caption">годы: блоками по 12</span>
      </div>
    </div>
  </div>

  <div class="variant">
    <p class="variant-label">
      <strong>5.2.</strong> Раздельные зоны: клик по "Июль" открывает сетку
      месяцев, клик по "2026" сразу сетку лет (к годам на клик быстрее); сетки
      те же, что в 5.1
    </p>
    <div class="cal-row">
      <div class="cal-state">
        <div class="cal">
          <div class="cal-header">
            <button type="button" class="cal-nav">‹</button>
            <span class="cal-title-split"
              ><button type="button" class="cal-zone">Июль</button>
              <button type="button" class="cal-zone">2026</button></span
            >
            <button type="button" class="cal-nav">›</button>
          </div>
          <div class="cal-weekdays">
            <span v-for="w in CAL_WEEKDAYS" :key="w" class="cal-weekday">{{
              w
            }}</span>
          </div>
          <div class="cal-days">
            <button
              v-for="(d, i) in calDays"
              :key="i"
              type="button"
              class="cal-day"
              :class="{
                'out-month': !d.inMonth,
                today: d.isToday,
                selected: d.isSelected,
              }"
            >
              {{ d.label }}
            </button>
          </div>
        </div>
        <span class="cal-state-caption">дни: две отдельные зоны в шапке</span>
      </div>
    </div>
  </div>

  <div class="variant">
    <p class="variant-label">
      <strong>5.3.</strong> Без дрилла: внешняя пара двойных шевронов листает
      год, заголовок остается статичным
    </p>
    <div class="cal-row">
      <div class="cal-state">
        <div class="cal">
          <div class="cal-header">
            <button type="button" class="cal-nav">«</button>
            <button type="button" class="cal-nav">‹</button>
            <span class="cal-title">Июль 2026</span>
            <button type="button" class="cal-nav">›</button>
            <button type="button" class="cal-nav">»</button>
          </div>
          <div class="cal-weekdays">
            <span v-for="w in CAL_WEEKDAYS" :key="w" class="cal-weekday">{{
              w
            }}</span>
          </div>
          <div class="cal-days">
            <button
              v-for="(d, i) in calDays"
              :key="i"
              type="button"
              class="cal-day"
              :class="{
                'out-month': !d.inMonth,
                today: d.isToday,
                selected: d.isSelected,
              }"
            >
              {{ d.label }}
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>

  <div class="variant">
    <p class="variant-label">
      <strong>5.4.</strong> Селекты месяца и года в шапке (шевроны листают
      месяц, как сейчас)
    </p>
    <div class="cal-row">
      <div class="cal-state">
        <div class="cal">
          <div class="cal-header">
            <button type="button" class="cal-nav">‹</button>
            <span class="cal-title-split"
              ><button type="button" class="cal-select">Июль ⌄</button>
              <button type="button" class="cal-select">2026 ⌄</button></span
            >
            <button type="button" class="cal-nav">›</button>
          </div>
          <div class="cal-weekdays">
            <span v-for="w in CAL_WEEKDAYS" :key="w" class="cal-weekday">{{
              w
            }}</span>
          </div>
          <div class="cal-days">
            <button
              v-for="(d, i) in calDays"
              :key="i"
              type="button"
              class="cal-day"
              :class="{
                'out-month': !d.inMonth,
                today: d.isToday,
                selected: d.isSelected,
              }"
            >
              {{ d.label }}
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Inputs"

.section-intro
  margin: 0 0 $medium
  line-height: 1.5

.code
  font-family: "Courier New", monospace

.variant
  margin-bottom: $big

.variant-label
  margin: 0 0 $small
  line-height: 1.5

// --- Секция 1: дайджест-контекст -------------------------------------

// Replica of the topic-card chrome (dashed border + element surface) the
// digest boards actually sit in.
.topic-replica
  border: 1px dashed $border
  background-color: $bg-element
  padding: $medium

.boards-grid
  display: grid
  grid-template-columns: repeat(3, 1fr)
  gap: $medium

.topic-replica :deep(.stat-board)
  border: none
  background-color: var(--sv-probe)

// Mockup-only probes; the picked alpha becomes a theme variable.
.probe-050
  --sv-probe: rgba(0, 0, 0, 0.05)

.probe-040
  --sv-probe: rgba(0, 0, 0, 0.04)

.probe-035
  --sv-probe: rgba(0, 0, 0, 0.035)

.probe-030
  --sv-probe: rgba(0, 0, 0, 0.03)

.probe-024
  --sv-probe: rgba(0, 0, 0, 0.024)

html.theme_Dark .probe-050
  --sv-probe: rgba(255, 255, 255, 0.05)

html.theme_Dark .probe-040
  --sv-probe: rgba(255, 255, 255, 0.04)

html.theme_Dark .probe-035
  --sv-probe: rgba(255, 255, 255, 0.035)

html.theme_Dark .probe-030
  --sv-probe: rgba(255, 255, 255, 0.03)

html.theme_Dark .probe-024
  --sv-probe: rgba(255, 255, 255, 0.024)

@media (max-width: 620px)
  .boards-grid
    grid-template-columns: 1fr

// --- Секция 2: полоса футера формы -----------------------------------

// Replica of Form's .controls strip (real primary-button context).
.controls-replica
  display: flex
  gap: $small
  padding: $medium
  background-color: $bg-element-accent
  border-radius: 0 0 $border-radius $border-radius
  max-width: 380px
  box-sizing: border-box

.v-plain
  +button

.v-current
  +button
  &
    background-color: $link
    border-color: $link
    color: #fff
  &:hover:not(:disabled)
    background-color: $link-hover
    border-color: $link-hover

.v-bold
  +button
  &
    font-weight: bold

.v-strong
  +button
  &
    background-color: $control-bg-hover-overlay
    font-weight: bold

.v-invert
  +button
  &
    background-color: $text
    border-color: $text
    color: $bg-page

.v-accent
  +button
  &
    background-color: $bg-element-accent
    font-weight: bold

// --- Секция 3: диалог и empty-состояния ------------------------------

// Static stand-in for the narrow Dialog (380px, $bg-page surface). The
// border exists only so the box is visible on the same-colored page.
.dialog-replica
  width: 380px
  max-width: 100%
  box-sizing: border-box
  padding: $medium
  background-color: $bg-page
  border: 1px solid $border
  margin-bottom: $small

.center-text
  text-align: center

.main-text
  margin: 0 0 $small
  line-height: 1.5

.link-row
  margin: 0 0 $medium

.full-width-btn
  width: 100%

// Inline link-button replicas of the auth forms (.inline-link bold /
// .field-action normal).
.ilink
  +inline-link-button
  &
    font-weight: bold

.ilink-normal
  +inline-link-button

// Real list background context for EmptyState variants.
.empty-context
  border: 1px dashed $border
  background-color: $bg-element
  max-width: 580px

.empty-left
  display: flex
  gap: $small
  padding: $big $medium

  &--surface
    background-color: $overlay-subtle

.empty-left-icon
  width: 32px
  height: 32px
  flex-shrink: 0
  color: $text-muted
  opacity: 0.5

.empty-left-title
  color: $text
  margin-bottom: $tiny

.empty-left-hint
  font-size: $secondary-font-size
  color: $text-muted

// --- Секция 4: инфо-секции -------------------------------------------

.dialog-width
  width: 380px
  max-width: 100%

.info-replica
  font-size: $secondary-font-size
  line-height: 1.5

  p
    margin: $minor 0

    &:first-child
      margin-top: 0

    &:last-child
      margin-bottom: 0

.info-current
  padding: $small $medium
  border: 1px solid $border
  border-radius: $border-radius
  color: $text-muted

.info-border
  padding: $small $medium
  border: 1px solid $border
  color: $text

.info-overlay
  padding: $small $medium
  background-color: $overlay-subtle
  color: $text

.info-highlight
  padding: $small $medium
  background-color: $highlight-overlay-blue
  color: $text

.info-plain
  color: $text

.info-fullsize
  font-size: $font-size

// --- Секция 5: календарь ----------------------------------------------
// Панель и сетки зеркалят реальные CalendarGrid / MonthYearPicker; шапка
// здесь flex (в реальной реализации остается inline-flow ради RULE-14
// копируемости).

.cal-row
  display: flex
  flex-wrap: wrap
  gap: $medium
  align-items: flex-start

.cal-state
  display: flex
  flex-direction: column
  gap: $tiny

.cal-state-caption
  font-size: $secondary-font-size
  color: $text-muted

.cal
  width: $grid-step * 62
  box-sizing: border-box
  padding: $small
  border: 1px solid $border
  border-radius: $border-radius
  background-color: $bg-element
  box-shadow: 0 4px 12px var(--shadow-color)

.cal-header
  display: flex
  align-items: center
  margin-bottom: $small

.cal-nav
  width: $grid-step * 6
  height: $grid-step * 6
  flex-shrink: 0
  border: none
  border-radius: $border-radius
  background: transparent
  color: $link
  font-size: $font-size
  cursor: pointer
  line-height: 1

  &:hover
    background-color: $bg-element-accent

.cal-title
  flex: 1
  text-align: center
  font-weight: bold
  color: $text

.cal-title-btn
  flex: 1
  text-align: center
  border: none
  background: transparent
  font: inherit
  font-weight: bold
  color: $text
  padding: $tiny 0
  border-radius: $border-radius
  cursor: pointer

  &:hover
    background-color: $bg-element-accent

.cal-title-split
  flex: 1
  text-align: center

.cal-zone
  border: none
  background: transparent
  font: inherit
  font-weight: bold
  color: $text
  padding: $tiny
  border-radius: $border-radius
  cursor: pointer

  &:hover
    background-color: $bg-element-accent

.cal-select
  border: 1px solid $border
  background-color: $input-bg-overlay
  font: inherit
  font-size: $secondary-font-size
  color: $text
  padding: 2px $minor
  border-radius: $border-radius
  cursor: pointer

  &:hover
    background-color: $bg-element-accent

.cal-weekdays
  display: grid
  grid-template-columns: repeat(7, 1fr)
  margin-bottom: $tiny

.cal-weekday
  text-align: center
  font-size: $tertiary-font-size
  color: $text-muted

.cal-days
  display: grid
  grid-template-columns: repeat(7, 1fr)
  gap: 1px

.cal-day
  aspect-ratio: 1
  border: none
  border-radius: $border-radius
  background: transparent
  color: $text
  cursor: pointer
  font: inherit
  font-size: $secondary-font-size

  &:hover
    background-color: $bg-highlight-blue

  &.out-month
    color: $text-muted

  &.today
    font-weight: bold
    color: $link

  &.selected
    background-color: $button-bg
    color: $button-text
    font-weight: bold

.cal-cells
  display: grid
  grid-template-columns: repeat(3, 1fr)
  gap: $tiny

.cal-cell
  padding: $tiny 0
  border: none
  border-radius: $border-radius
  background: transparent
  color: $text
  cursor: pointer
  font: inherit
  font-size: $secondary-font-size

  &:hover
    background-color: $bg-highlight-blue

  &.selected
    background-color: $button-bg
    color: $button-text
    font-weight: bold
</style>
