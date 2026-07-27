<script setup lang="ts">
/**
 * RulesPage - site rules page.
 *
 * All sections are static (not loaded from DB).
 * Penalty data lives inline and is rendered via the unified ExpandableList
 * (multi-column mode with per-id slots for rich detail content).
 *
 * Editing — through code.
 */

import { onMounted, ref } from "vue";
import { useRoute } from "vue-router";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import {
  ExpandableList,
  type ExpandableListColumn,
} from "@/shared/ui/ExpandableList";
import { DISCORD_INVITE_URL } from "@/shared/config/contacts";
import RulesIntro from "./RulesIntro.vue";
import RulesHelpLinks from "./RulesHelpLinks.vue";
import RulesExternalLinks from "./RulesExternalLinks.vue";
import RulesBans from "./RulesBans.vue";
import RulesAuthors from "./RulesAuthors.vue";
import RulesStaffTable from "./RulesStaffTable.vue";
import type { ExpandableListExpose } from "./expandableListRef";

const route = useRoute();
const penaltiesListRef = ref<ExpandableListExpose | null>(null);

// Type alias (not interface) so the implicit index signature satisfies
// the ExpandableItem constraint of ExpandableList.
type Penalty = {
  id: string;
  violation: string;
  points: string;
  sortValue: number;
};

// Order: from most serious violations to less serious
const penalties: Penalty[] = [
  { id: "hacking", violation: "Атака на сайт", points: "∞", sortValue: 100 },
  { id: "insult", violation: "Оскорбление", points: "6", sortValue: 6.2 },
  {
    id: "advertising",
    violation: "Несогласованная реклама",
    points: "6",
    sortValue: 6.1,
  },
  {
    id: "multiaccounts",
    violation: "Мультиаккаунт",
    points: "6",
    sortValue: 6,
  },
  {
    id: "banned-proxy",
    violation: "Нарушение бана",
    points: "5",
    sortValue: 5,
  },
  {
    id: "provocation",
    violation: "Провокация и политика",
    points: "3",
    sortValue: 3,
  },
  {
    id: "shock-content",
    violation: "Шок-контент без [nsfw]",
    points: "2",
    sortValue: 2,
  },
  { id: "profanity", violation: "Мат без [nsfw]", points: "1", sortValue: 1.5 },
  { id: "flame", violation: "Флейм", points: "1", sortValue: 1 },
  {
    id: "flood",
    violation: "Флуд, вайп и оффтоп",
    points: "1",
    sortValue: 0.5,
  },
].sort((a, b) => b.sortValue - a.sortValue);

const penaltyColumns: ExpandableListColumn<Penalty>[] = [
  { key: "violation", label: "Нарушение" },
  { key: "points", label: "Баллы", width: "80px", align: "center", bold: true },
];

// Deep-link support: #<item-id> (e.g. "#hacking") auto-expands the matching
// penalty row on mount. Scrolling itself is handled globally by the router
// (router.ts already does document.getElementById(hash) on every
// navigation) — this only adds the expand-on-arrival behavior.
onMounted(() => {
  const id = route.hash.slice(1);
  if (id && penalties.some((penalty) => penalty.id === id)) {
    penaltiesListRef.value?.expandItem(id);
  }
});
</script>

<template>
  <page-title v-once>Правила</page-title>

  <RulesIntro />

  <RulesHelpLinks />

  <!-- Penalties table -->
  <section id="penalties" class="rules-section">
    <BlockTitle>Нарушения и баллы</BlockTitle>
    <ExpandableList
      ref="penaltiesListRef"
      :items="penalties"
      :columns="penaltyColumns"
      :allow-multiple="true"
    >
      <template #item-hacking>
        Взлом, DDoS, эксплуатация уязвимостей, шантаж и угрозы — перманентный
        бан. Хотите помочь найти уязвимость — сначала согласуйте с
        администрацией. На критические обращения отвечаем в течение 7 дней.
      </template>

      <template #item-insult>
        Публичное умышленное унижение личности, действий или убеждений
        пользователя. Исправили сами до модерации — баллы могут снизить.
      </template>

      <template #item-advertising>
        Ссылки на ролевые проекты, набор игроков на сторонние сайты, продвижение
        товаров и услуг. Спам-аккаунты банятся навсегда. Хотите согласовать —
        напишите администрации.
      </template>

      <template #item-multiaccounts>
        Один человек — один аккаунт. Дополнительные аккаунты банятся навсегда,
        основной получает 6 баллов (0 баллов — если сразу сообщить об ошибке
        через
        <router-link to="/support"
          ><strong>форму обращения</strong></router-link
        >
        или
        <a :href="DISCORD_INVITE_URL" target="_blank" rel="noopener noreferrer"
          ><strong>Discord</strong></a
        >).
      </template>

      <template #item-banned-proxy>
        Публикация текстов за забаненного пользователя запрещена. Мастер в бане
        может согласовать ассистента для ведения игры.
      </template>

      <template #item-provocation>
        Троллинг, провокационные вбросы, подстрекательство, этнические
        оскорбления и любые обсуждения политики. В играх с политическим
        сеттингом поставьте тег "Острые темы" и закройте комнаты от посторонних.
      </template>

      <template #item-shock-content>
        Шок-контент и откровенные материалы без тега [nsfw] запрещены везде.
        Исключения: посты в играх, личные переписки и контент с закрытым
        доступом.
      </template>

      <template #item-profanity>
        Мат без тега [nsfw] или цензуры (***) запрещен везде. Исключения:
        устоявшиеся аббревиатуры (ХЗ), игровые посты и сообщения, личные
        переписки и контент с закрытым доступом. Исключение не действует на
        посты и сообщения в играх с тегом "без мата".
      </template>

      <template #item-flame>
        Словесная пикировка, не связанная с изначальным спором. Переход на
        личности без прямых оскорблений. Затяжные эмоциональные споры.
      </template>

      <template #item-flood>
        Многократные однотипные сообщения и комментарии, создание большого
        количества бессодержательных топиков, уход от темы в служебных разделах
        форума.
      </template>
    </ExpandableList>
    <p class="penalty-note">
      Набрали 6 баллов — получили бан. Баллы сгорают через 6 месяцев без
      нарушений.
    </p>
  </section>

  <RulesExternalLinks />

  <RulesBans />

  <!-- For authors and masters -->
  <RulesAuthors />

  <RulesStaffTable />
</template>

<style scoped lang="sass">
.rules-section
  margin: $big 0

.penalty-note
  margin-top: $small
  font-size: $secondary-font-size
  color: $text-muted
</style>
