<script setup lang="ts">
/**
 * PenaltyTable - интерактивная таблица нарушений
 *
 * Клик по строке раскрывает детальное описание нарушения.
 * Вся информация в одном месте, не нужно скроллить к тексту ниже.
 */

import { useExpandable } from "@/shared/lib/composables/useExpandable";

interface Penalty {
  id: string;
  violation: string;
  points: string;
  sortValue: number;
  details: string;
}

const { toggle, isExpanded } = useExpandable();

// Порядок: от самых серьезных нарушений к менее серьезным
const penalties: Penalty[] = [
  {
    id: "hacking",
    violation: "Атака на сайт",
    points: "∞",
    sortValue: 100,
    details:
      "Взлом, DDoS, эксплуатация уязвимостей, шантаж и угрозы — перманентный бан. Хочешь помочь найти уязвимость — сначала согласуй с администрацией. На критические обращения отвечаем в течение 7 дней.",
  },
  {
    id: "insult",
    violation: "Оскорбление",
    points: "6",
    sortValue: 6.2,
    details:
      "Публичное умышленное унижение личности, действий или убеждений пользователя. Текст скрывается модератором. Исправил сам до модерации — баллы могут снизить.",
  },
  {
    id: "advertising",
    violation: "Несогласованная реклама",
    points: "6",
    sortValue: 6.1,
    details:
      "Ссылки на ролевые проекты, набор игроков на сторонние сайты, продвижение товаров и услуг. Спам-аккаунты банятся навсегда. Хочешь согласовать — напиши администрации.",
  },
  {
    id: "multiaccounts",
    violation: "Мультиаккаунт",
    points: "6",
    sortValue: 6,
    details:
      "Один человек — один аккаунт. Дополнительные аккаунты банятся навсегда, основной получает 6 баллов (0 баллов — если сразу сообщил об ошибке через форму или Discord).",
  },
  {
    id: "banned-proxy",
    violation: "Нарушение бана",
    points: "5",
    sortValue: 5,
    details:
      "Публикация текстов за забаненного пользователя запрещена. Мастер в бане может согласовать ассистента для ведения игры.",
  },
  {
    id: "provocation",
    violation: "Провокация и политика",
    points: "3",
    sortValue: 3,
    details:
      "Троллинг, провокационные вбросы, подстрекательство, этнические оскорбления и любые обсуждения политики. В играх с политическим сеттингом закройте комнаты и обсуждение от посторонних.",
  },
  {
    id: "shock-content",
    violation: "Шок-контент без [nsfw]",
    points: "2",
    sortValue: 2,
    details:
      "Шок-контент и откровенные материалы только в теге [nsfw]. В играх — на усмотрение мастера, кроме страницы Информация. Тег [nsfw] не оправдывает оскорбления и провокации.",
  },
  {
    id: "profanity",
    violation: "Мат без [nsfw]",
    points: "1",
    sortValue: 1.5,
    details:
      'Можно: в [nsfw], с цензурой (***), аббревиатуры (ХЗ), в играх без тега "без мата" (кроме страницы Информация). Нельзя: форум, чат, профили, названия.',
  },
  {
    id: "flame",
    violation: "Флейм",
    points: "1",
    sortValue: 1,
    details:
      "Словесная пикировка, не связанная с изначальным спором. Переход на личности без прямых оскорблений. Затяжные эмоциональные споры.",
  },
  {
    id: "flood",
    violation: "Флуд, вайп и оффтоп",
    points: "1",
    sortValue: 0.5,
    details:
      "Многократные однотипные сообщения, создание пустых тем, уход от темы в служебных разделах.",
  },
].sort((a, b) => b.sortValue - a.sortValue);

function handleKeydown(event: KeyboardEvent, id: string) {
  if (event.key === "Enter" || event.key === " ") {
    event.preventDefault();
    toggle(id);
  }
}
</script>

<template>
  <div class="penalty-table-wrapper">
    <div class="penalty-table" role="list">
      <div class="penalty-header" aria-hidden="true">
        <span>Нарушение</span>
        <span class="points-column">Баллы</span>
      </div>
      <template v-for="penalty in penalties" :key="penalty.id">
        <div
          class="penalty-row"
          :class="{ expanded: isExpanded(penalty.id) }"
          role="button"
          tabindex="0"
          :aria-expanded="isExpanded(penalty.id)"
          :aria-controls="`details-${penalty.id}`"
          @click="toggle(penalty.id)"
          @keydown="handleKeydown($event, penalty.id)"
        >
          <span class="violation-name">
            <span class="expand-icon" aria-hidden="true">{{
              isExpanded(penalty.id) ? "▼" : "▶"
            }}</span>
            <span class="violation-text">{{ penalty.violation }}</span>
          </span>
          <span class="points-column">{{ penalty.points }}</span>
        </div>
        <div
          v-if="isExpanded(penalty.id)"
          :id="`details-${penalty.id}`"
          class="penalty-details"
        >
          {{ penalty.details }}
        </div>
      </template>
    </div>
    <p class="penalty-note">
      Набрал 6 баллов — получил бан. Баллы сгорают через 6 месяцев без
      нарушений.
    </p>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/Tables"

.penalty-table-wrapper
  margin: $medium 0

.penalty-table
  +table

.penalty-header
  display: grid
  grid-template-columns: 1fr 80px
  padding: $small $medium
  +table-header

  span
    padding: 0
    border: none

.penalty-row
  display: grid
  grid-template-columns: 1fr 80px
  padding: $small $medium
  cursor: pointer
  +table-row

  span
    padding: 0
    border: none

  &:hover
    background-color: $bg-element-hover

  &:focus-visible
    outline: 2px solid $link
    outline-offset: -2px

  &.expanded
    background-color: $bg-element-hover
    border-bottom: none

.violation-name
  display: flex
  align-items: center
  gap: $small

.expand-icon
  +expand-icon
  user-select: none

.violation-text
  user-select: text

.points-column
  text-align: center
  font-weight: 600
  user-select: text

.penalty-details
  +expandable-details
  user-select: text

.penalty-note
  margin-top: $small
  font-size: $secondary-font-size
  color: $text
  font-style: italic

@media (max-width: $mobile-breakpoint)
  .penalty-header
    grid-template-columns: 1fr 60px

  .penalty-row
    grid-template-columns: 1fr 60px

  .points-column
    font-size: $secondary-font-size
</style>
