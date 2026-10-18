<script setup lang="ts">
/**
 * RulesBans - ban mechanics section
 */

import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import {
  ExpandableList,
  type ExpandableItem,
} from "@/shared/ui/ExpandableList";
import { useExpandOnHash } from "./expandableListRef";

const sections: ExpandableItem[] = [
  {
    id: "points",
    title: "Механика баллов",
    content: [
      "Набрали 6 баллов — сайт сам ставит демократический бан на 48 часов.",
      "Автобан снимает или заменяет своим решением старший модератор или администратор — на это и даются 48 часов.",
      "Максимум 6 баллов за один календарный день.",
      "3+ балла за одно нарушение — блокировка чата на сутки.",
      "При получении бана сгорает 6 баллов, остаток переносится.",
    ],
  },
  {
    id: "terms",
    title: "Сроки банов",
    content: [
      "1-й бан — 3 дня.",
      "2-й бан — 2 недели.",
      "3-й бан — 2 месяца.",
      "4-й бан — 1 год.",
      "5-й бан и далее — решение администрации, вплоть до перманентного.",
    ],
  },
  {
    id: "types",
    title: "Типы банов",
    content: [
      "Демократический бан — нельзя писать в общем чате, создавать топики и комментировать на форуме, комментировать чужие игры и блоги. Свои игры и блоги, посты в комнатах, публикации и личные сообщения остаются открытыми, читать можно все.",
      "Полный бан — вход на сайт закрыт, писать нельзя нигде. Применяется за серьезные нарушения.",
    ],
  },
  {
    id: "decay",
    title: "Сгорание баллов и банов",
    content: [
      'Через 6 месяцев без нарушений сначала "забываются" баны (по 6 месяцев на каждый), затем баллы.',
      'Новое нарушение после "забывания" — отсчет заново.',
    ],
  },
  {
    id: "appeal",
    title: "Обжалование",
  },
];

const listRef = useExpandOnHash(sections);
</script>

<template>
  <section id="bans" class="rules-bans">
    <BlockTitle>Как работают баны</BlockTitle>
    <ExpandableList ref="listRef" :items="sections" :allow-multiple="true">
      <template #item-appeal>
        <ul>
          <li>
            Если не согласны с решением конкретного модератора, его можно
            обжаловать, заполнив соответствующую
            <router-link to="/complaint"
              ><strong>форму жалоб</strong></router-link
            >.
          </li>
          <li>
            Решения по обжалованию принимаются коллегиально. Хамство =
            дополнительные баллы.
          </li>
        </ul>
      </template>
    </ExpandableList>
  </section>
</template>

<style scoped lang="sass">
.rules-bans
  margin: $big 0
</style>
