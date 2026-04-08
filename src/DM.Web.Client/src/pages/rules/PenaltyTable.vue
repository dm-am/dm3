<script setup lang="ts">
/**
 * PenaltyTable - interactive violations table
 *
 * Click on a row to expand the detailed description.
 * Uses unified table styling from _Tables.sass.
 */

import { useRouter } from "vue-router";
import { useExpandable } from "@/shared/lib/composables/useExpandable";

interface Penalty {
  id: string;
  violation: string;
  points: string;
  sortValue: number;
  details: string;
}

const router = useRouter();
const { toggle, isExpanded } = useExpandable();

// Order: from most serious violations to less serious
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
      "Публичное умышленное унижение личности, действий или убеждений пользователя. Исправил сам до модерации — баллы могут снизить.",
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
      'Один человек — один аккаунт. Дополнительные аккаунты банятся навсегда, основной получает 6 баллов (0 баллов — если сразу сообщил об ошибке через <a href="/support"><strong>форму</strong></a> или <a href="https://discord.gg/dm-roleplay" target="_blank" rel="noopener noreferrer"><strong>Discord</strong></a>).',
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
      "Троллинг, провокационные вбросы, подстрекательство, этнические оскорбления и любые обсуждения политики. В играх с политическим сеттингом поставьте тег \"Острые темы\" и закройте комнаты от посторонних.",
  },
  {
    id: "shock-content",
    violation: "Шок-контент без [nsfw]",
    points: "2",
    sortValue: 2,
    details:
      "Шок-контент и откровенные материалы без тега [nsfw] запрещены везде. Исключения: посты в играх, личные переписки и контент с закрытым доступом.",
  },
  {
    id: "profanity",
    violation: "Мат без [nsfw]",
    points: "1",
    sortValue: 1.5,
    details:
      'Мат без тега [nsfw] или цензуры (***) запрещен везде. Исключения: устоявшиеся аббревиатуры (ХЗ), игровые посты и сообщения, личные переписки и контент с закрытым доступом. Исключение не действует на посты и сообщения в играх с тегом "без мата".',
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
      "Многократные однотипные сообщения и комментарии, создание большого количества бессодержательных топиков, уход от темы в служебных разделах форума.",
  },
].sort((a, b) => b.sortValue - a.sortValue);

function handleKeydown(event: KeyboardEvent, id: string) {
  if (event.key === "Enter" || event.key === " ") {
    event.preventDefault();
    toggle(id);
  }
}

/**
 * Handle clicks on links inside v-html details.
 * Internal links use Vue Router for SPA navigation.
 */
function handleDetailsClick(event: MouseEvent) {
  const target = event.target as HTMLElement;
  const link = target.closest("a");
  if (!link) return;

  const href = link.getAttribute("href");
  if (!href) return;

  // External links (with target="_blank") - let browser handle
  if (link.target === "_blank") return;

  // Internal links - use Vue Router
  event.preventDefault();
  router.push(href);
}
</script>

<template>
  <div class="penalty-table-wrapper">
    <div class="penalty-table" role="list">
      <div class="penalty-header" aria-hidden="true">
        <span class="violation-col">Нарушение</span>
        <span class="points-col">Баллы</span>
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
          <span class="violation-col">
            <span class="expand-icon" aria-hidden="true">{{
              isExpanded(penalty.id) ? "▼" : "▶"
            }}</span>
            <span class="violation-text">{{ penalty.violation }}</span>
          </span>
          <span class="points-col">{{ penalty.points }}</span>
        </div>
        <div
          v-if="isExpanded(penalty.id)"
          :id="`details-${penalty.id}`"
          class="penalty-details"
          v-html="penalty.details"
          @click="handleDetailsClick"
        />
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
  +table-header

.penalty-row
  +expandable-row
  &
    display: grid
    grid-template-columns: 1fr 80px

.violation-col
  display: flex
  align-items: center
  gap: $small

.expand-icon
  +expand-icon

.violation-text
  user-select: text

.points-col
  text-align: center
  font-weight: 600
  user-select: text

.penalty-details
  +expandable-details
  user-select: text

  :deep(a)
    color: $link
    text-decoration: none

    &:hover
      color: $link-hover
      text-decoration: underline

  :deep(strong)
    font-weight: 600

.penalty-note
  margin-top: $small
  font-size: $secondary-font-size
  color: $text-muted

@media (max-width: $mobile-breakpoint)
  .penalty-header
    grid-template-columns: 1fr 60px

  .penalty-row
    +expandable-row-mobile
    grid-template-columns: 1fr 60px

  .points-col
    font-size: $secondary-font-size

  .penalty-details
    +expandable-details-mobile
</style>
