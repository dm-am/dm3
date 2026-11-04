<script setup lang="ts">
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import {
  ExpandableList,
  type ExpandableItem,
} from "@/shared/ui/ExpandableList";
import { useExpandOnHash } from "./expandableListRef";
import { MAX_WARNING_POINTS, points } from "./warningPolicy";

const sections: ExpandableItem[] = [
  {
    id: "other-rpg",
    title: "Другие ролевые сайты",
    content: [
      `Ссылки без согласования = ${points(MAX_WARNING_POINTS)}. Для согласования напишите администрации.`,
      "Исключение: личные переписки и профили.",
    ],
  },
  {
    id: "commercial",
    title: "Коммерческие ресурсы",
    content: [
      "Реклама, донаты, краудфандинги, партнерские ссылки — только с разрешения администрации.",
      "Исключение: тематическое обсуждение (книги, игры, арт).",
    ],
  },
  {
    id: "personal",
    title: "Личные ресурсы",
    content: [
      "Соцсети, Discord, Telegram — можно в профиле.",
      "В глобальном чате и других публичных местах — умеренно и в контексте.",
      "Массовая рассылка = спам.",
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

const listRef = useExpandOnHash(sections);
</script>

<template>
  <section id="links" class="rules-external-links">
    <BlockTitle>Ссылки на сторонние ресурсы</BlockTitle>
    <ExpandableList ref="listRef" :items="sections" :allow-multiple="true" />
  </section>
</template>

<style scoped lang="sass">
.rules-external-links
  margin: $big 0
</style>
