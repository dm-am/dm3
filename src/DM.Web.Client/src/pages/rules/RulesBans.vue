<script setup lang="ts">
/**
 * RulesBans - как работают баны
 */

import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import { useExpandable } from "@/shared/lib/composables/useExpandable";

interface BanItem {
  text: string;
  sub?: string[];
}

interface BanInfo {
  id: string;
  title: string;
  content: BanItem[];
}

const { toggle, isExpanded } = useExpandable();

const sections: BanInfo[] = [
  {
    id: "points",
    title: "Механика баллов",
    content: [
      { text: "Набрал 6 баллов — автоматический бан." },
      {
        text: "При 6+ баллах — автобан на 12 часов до рассмотрения администрацией.",
      },
      { text: "Максимум 6 баллов за один календарный день." },
      { text: "3+ балла за одно нарушение — блокировка чата на сутки." },
      { text: "При получении бана сгорает 6 баллов, остаток переносится." },
    ],
  },
  {
    id: "terms",
    title: "Сроки банов",
    content: [
      { text: "1-й бан — 3 дня." },
      { text: "2-й бан — 2 недели." },
      { text: "3-й бан — 2 месяца." },
      { text: "4-й бан — 1 год." },
      {
        text: "5-й бан и далее — решение администрации, вплоть до перманентного.",
      },
    ],
  },
  {
    id: "types",
    title: "Типы банов",
    content: [
      {
        text: "Демократический бан — нельзя писать на форуме, в чате, в чужих играх. Свои игры доступны, читать можно все.",
      },
      {
        text: "Полный бан — доступ к сайту закрыт полностью. Применяется за серьезные нарушения.",
      },
    ],
  },
  {
    id: "decay",
    title: "Сгорание баллов и банов",
    content: [
      {
        text: 'Через 6 месяцев без нарушений сначала "забываются" баны (по 6 месяцев на каждый), затем баллы.',
      },
      { text: 'Новое нарушение после "забывания" — отсчет заново.' },
    ],
  },
  {
    id: "appeal",
    title: "Обжалование",
    content: [
      {
        text: "Не согласны — напишите администратору с сутью претензии и аргументами.",
      },
      {
        text: "Решения принимаются коллегиально. Хамство = дополнительные баллы.",
      },
    ],
  },
];

function handleKeydown(event: KeyboardEvent, id: string) {
  if (event.key === "Enter" || event.key === " ") {
    event.preventDefault();
    toggle(id);
  }
}
</script>

<template>
  <section class="rules-bans">
    <BlockTitle>Как работают баны</BlockTitle>

    <div class="bans-table" role="list">
      <template v-for="section in sections" :key="section.id">
        <div
          class="bans-row"
          :class="{ expanded: isExpanded(section.id) }"
          role="button"
          tabindex="0"
          :aria-expanded="isExpanded(section.id)"
          :aria-controls="`bans-details-${section.id}`"
          @click="toggle(section.id)"
          @keydown="handleKeydown($event, section.id)"
        >
          <span class="expand-icon" aria-hidden="true">{{
            isExpanded(section.id) ? "▼" : "▶"
          }}</span>
          <span class="ban-title">{{ section.title }}</span>
        </div>
        <div
          v-if="isExpanded(section.id)"
          :id="`bans-details-${section.id}`"
          class="bans-details"
        >
          <ul>
            <li v-for="(item, idx) in section.content" :key="idx">
              {{ item.text }}
            </li>
          </ul>
        </div>
      </template>
    </div>
  </section>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/Tables"

.rules-bans
  margin: $big 0

.bans-table
  +table

.bans-row
  +expandable-row

  &:focus-visible
    outline: 2px solid $link
    outline-offset: -2px

.expand-icon
  +expand-icon

.ban-title
  font-weight: 500

.bans-details
  +expandable-details
  user-select: text

  > ul
    margin: 0
    padding-left: $medium
    list-style: disc

    > li
      margin: $minor 0
      color: $text

@media (max-width: $mobile-breakpoint)
  .bans-row
    +expandable-row-mobile

  .bans-details
    +expandable-details-mobile
</style>
