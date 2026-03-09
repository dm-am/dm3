<script setup lang="ts">
/**
 * RulesMasters - правила для мастеров игр
 */

import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import { useExpandable } from "@/shared/lib/composables/useExpandable";

interface MasterInfo {
  id: string;
  title: string;
  content: string[];
}

const { toggle, isExpanded } = useExpandable();

const sections: MasterInfo[] = [
  {
    id: "principles",
    title: "Основные принципы",
    content: [
      "Игра — сотворчество мастера и игроков. Мастер ведет как считает нужным, игроки принимают его условия.",
      "Мастер может попросить любого покинуть обсуждение (кроме администратора при исполнении). Отказ = бан + удаление сообщений.",
      "Администрация не вмешивается в игры, кроме случаев нарушения правил сайта.",
      "Модули в неигровых целях (кроме блогов и блокнотов) — согласуйте с администрацией.",
    ],
  },
  {
    id: "notebooks",
    title: "Модули-блокноты",
    content: [
      "Личное пространство для черновиков, персонажей, заметок. Согласование не требуется.",
      'Обязательно: статус "В оформлении", количество игроков "По приглашениям", слово "Блокнот" в названии.',
      "Один личный блокнот на аккаунт. Нельзя ставить плюсы и минусы, создавать персонажей (кроме НПС).",
      'Коллективные блокноты: тег "Блог", статус "В оформлении", "По приглашениям" — без ограничений по количеству.',
    ],
  },
  {
    id: "blogs",
    title: "Модули-блоги",
    content: [
      'Публичный дневник, статьи, творчество. Обязательно: тег "Блог", статус "В игре".',
      'После установки тега "Блог" убрать его невозможно.',
      "Нельзя ставить плюсы и минусы (только комментарии). Пост из блога не станет лучшим постом недели.",
      'Все правила сайта действуют полностью, без "на усмотрение мастера".',
      "Блоги не в списке наборов. Отображаются в списке новых блогов (макс. 5) и самых читаемых блогов.",
    ],
  },
  {
    id: "inactive",
    title: "Неактивные модули",
    content: [
      "Месяц без постов — предупреждение, через неделю — принудительное закрытие и архив.",
      "Возврат из архива — через администрацию. Исторически ценные модули могут быть исключением.",
    ],
  },
  {
    id: "storage",
    title: "Хранение контента",
    content: [
      "Контент хранится на серверах DM на все время существования проекта.",
      "Удаляется навсегда: CSAM, личные данные без согласия, призывы к терроризму и экстремизму, пропаганда суицида и наркотиков, спам и вредоносный контент.",
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
  <section class="rules-masters">
    <BlockTitle>Для мастеров</BlockTitle>

    <div class="masters-table" role="list">
      <template v-for="section in sections" :key="section.id">
        <div
          class="masters-row"
          :class="{ expanded: isExpanded(section.id) }"
          role="button"
          tabindex="0"
          :aria-expanded="isExpanded(section.id)"
          :aria-controls="`masters-details-${section.id}`"
          @click="toggle(section.id)"
          @keydown="handleKeydown($event, section.id)"
        >
          <span class="expand-icon" aria-hidden="true">{{
            isExpanded(section.id) ? "▼" : "▶"
          }}</span>
          <span class="master-title">{{ section.title }}</span>
        </div>
        <div
          v-if="isExpanded(section.id)"
          :id="`masters-details-${section.id}`"
          class="masters-details"
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

.rules-masters
  margin: $big 0

.masters-table
  +table

.masters-row
  +expandable-row

  &:focus-visible
    outline: 2px solid $link
    outline-offset: -2px

.expand-icon
  +expand-icon

.master-title
  font-weight: 500

.masters-details
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
  .masters-row
    +expandable-row-mobile

  .masters-details
    +expandable-details-mobile
</style>
