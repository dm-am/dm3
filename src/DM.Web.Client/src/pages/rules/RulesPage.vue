<script setup lang="ts">
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import {
  ExpandableList,
  type ExpandableListColumn,
} from "@/shared/ui/ExpandableList";
import { DISCORD_INVITE_URL } from "@/shared/config/contacts";
import { supportFor } from "@/entities/ticket";
import RulesIntro from "./RulesIntro.vue";
import RulesHelpLinks from "./RulesHelpLinks.vue";
import RulesExternalLinks from "./RulesExternalLinks.vue";
import RulesBans from "./RulesBans.vue";
import RulesAuthors from "./RulesAuthors.vue";
import RulesStaffTable from "./RulesStaffTable.vue";
import { useExpandOnHash } from "./expandableListRef";
import { AUTO_BAN_POINTS, MAX_WARNING_POINTS, points } from "./warningPolicy";

// Type alias (not interface) so the implicit index signature satisfies
// the ExpandableItem constraint of ExpandableList.
type Penalty = {
  id: string;
  violation: string;
  points: string;
};

// The top of the table is the most one warning may cost.
const TOP_PENALTY = String(MAX_WARNING_POINTS);

// Order: from most serious violations to less serious
const penalties: Penalty[] = [
  { id: "hacking", violation: "Атака на сайт", points: "∞" },
  { id: "insult", violation: "Оскорбление", points: TOP_PENALTY },
  {
    id: "advertising",
    violation: "Несогласованная реклама",
    points: TOP_PENALTY,
  },
  { id: "multiaccounts", violation: "Мультиаккаунт", points: TOP_PENALTY },
  { id: "banned-proxy", violation: "Нарушение бана", points: "5" },
  { id: "provocation", violation: "Провокация и политика", points: "3" },
  { id: "shock-content", violation: "Шок-контент без [nsfw]", points: "2" },
  { id: "profanity", violation: "Мат без [nsfw]", points: "1" },
  { id: "flame", violation: "Флейм", points: "1" },
  { id: "flood", violation: "Флуд, вайп и оффтоп", points: "1" },
];

const penaltyColumns: ExpandableListColumn<Penalty>[] = [
  { key: "violation", label: "Нарушение" },
  { key: "points", label: "Баллы", width: "80px", align: "center", bold: true },
];

const penaltiesListRef = useExpandOnHash(penalties);
</script>

<template>
  <page-title v-once>Правила</page-title>

  <RulesIntro />

  <RulesHelpLinks />

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
        бан. Если хотите помочь найти уязвимость, сначала согласуйте с
        администрацией через
        <router-link :to="supportFor('Bug')"
          ><strong>форму поддержки</strong></router-link
        >. На критические обращения отвечаем в течение 7 дней.
      </template>

      <template #item-insult>
        Публичное умышленное унижение личности, действий или убеждений
        пользователя. Если исправили сами до модерации, баллы могут снизить.
      </template>

      <template #item-advertising>
        Ссылки на ролевые проекты, набор игроков на сторонние сайты, продвижение
        товаров и услуг. Спам-аккаунты банятся навсегда. Если хотите
        согласовать, напишите администрации.
      </template>

      <template #item-multiaccounts>
        Один человек — один аккаунт. Дополнительные аккаунты банятся навсегда,
        основной получает {{ points(MAX_WARNING_POINTS) }} (0 баллов, если сразу
        сообщить об ошибке через
        <router-link :to="supportFor('AccountRemoval')"
          ><strong>форму поддержки</strong></router-link
        >
        или
        <a :href="DISCORD_INVITE_URL" target="_blank" rel="noopener noreferrer"
          ><strong>Discord</strong></a
        >).
      </template>

      <template #item-banned-proxy>
        Писать за забаненного пользователя запрещено. Мастер в бане может
        согласовать ассистента для ведения игры.
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
        посты и сообщения в играх с тегом "Без мата".
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
      Набрали {{ points(AUTO_BAN_POINTS) }} — получили бан. Баллы сгорают через
      6 месяцев без нарушений.
    </p>
  </section>

  <RulesExternalLinks />

  <RulesBans />

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
