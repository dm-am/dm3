<script setup lang="ts">
/**
 * RulesExternalLinks - external links rules
 */

import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import { ExpandableList, type ExpandableItem } from "@/shared/ui/ExpandableList";

const sections: ExpandableItem[] = [
  {
    id: "other-rpg",
    title: "Другие ролевые сайты",
    content: [
      "Ссылки без согласования = 6 баллов. Исключение: личные переписки и профили. Для согласования напишите администрации.",
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
      "Соцсети, Discord, Telegram — можно в профиле. В глобальном чате и других публичных местах — умеренно и в контексте. Массовая рассылка = спам.",
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
</script>

<template>
  <section class="rules-external-links">
    <BlockTitle>Ссылки на сторонние ресурсы</BlockTitle>
    <ExpandableList :items="sections" />
  </section>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"

.rules-external-links
  margin: $big 0
</style>
