<script setup lang="ts">
/**
 * RulesExternalLinks - правила о ссылках на сторонние ресурсы
 */

import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import { useExpandable } from "@/shared/lib/composables/useExpandable";

interface LinkRule {
  id: string;
  title: string;
  content: string[];
}

const { toggle, isExpanded } = useExpandable();

const sections: LinkRule[] = [
  {
    id: "other-rpg",
    title: "Другие ролевые сайты",
    content: [
      "Ссылки без согласования = 6 баллов. Исключение: личные разговоры, профили, закрытые игры. Для согласования напишите администрации.",
    ],
  },
  {
    id: "commercial",
    title: "Коммерческие ресурсы",
    content: [
      "Реклама, донаты, краудфандинги, партнерские ссылки — только с разрешения администрации. Исключение: тематическое обсуждение (книги, игры, арт).",
    ],
  },
  {
    id: "personal",
    title: "Личные ресурсы",
    content: [
      "Соцсети, Discord, Telegram — можно в профиле. В чате и играх с осторожностью. Массовая рассылка = спам.",
    ],
  },
  {
    id: "allowed",
    title: "Что можно без согласования",
    content: [
      "Справочники, вики, энциклопедии, изображения, видео, файлообменники для игровых материалов.",
    ],
  },
  {
    id: "warning",
    title: "Ссылки на 18+ контент",
    content: [
      "Ссылки на мат, откровенные или шокирующие материалы требуют текстового предупреждения в том же предложении. Это приравнивается к тегу [nsfw].",
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
  <section class="rules-external-links">
    <BlockTitle>Ссылки на сторонние ресурсы</BlockTitle>

    <div class="links-table" role="list">
      <template v-for="section in sections" :key="section.id">
        <div
          class="links-row"
          :class="{ expanded: isExpanded(section.id) }"
          role="button"
          tabindex="0"
          :aria-expanded="isExpanded(section.id)"
          :aria-controls="`links-details-${section.id}`"
          @click="toggle(section.id)"
          @keydown="handleKeydown($event, section.id)"
        >
          <span class="expand-icon" aria-hidden="true">{{
            isExpanded(section.id) ? "▼" : "▶"
          }}</span>
          <span class="link-title">{{ section.title }}</span>
        </div>
        <div
          v-if="isExpanded(section.id)"
          :id="`links-details-${section.id}`"
          class="links-details"
        >
          <ul>
            <li v-for="(item, idx) in section.content" :key="idx">
              {{ item }}
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

.rules-external-links
  margin: $big 0

.links-table
  +table

.links-row
  +expandable-row

  &:focus-visible
    outline: 2px solid $link
    outline-offset: -2px

.expand-icon
  +expand-icon

.link-title
  font-weight: 500

.links-details
  user-select: text
  +expandable-details
  +expandable-details-list

@media (max-width: $mobile-breakpoint)
  .links-row
    +expandable-row-mobile

  .links-details
    +expandable-details-mobile
</style>
