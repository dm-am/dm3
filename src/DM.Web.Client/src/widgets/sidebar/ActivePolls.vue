<script setup lang="ts">
import SidebarBlock from "./SidebarBlock.vue";
import SidebarSkeleton from "./SidebarSkeleton.vue";
import { usePollsStore } from "@/entities/poll";
import { onMounted } from "vue";
import PollCard from "./PollCard.vue";
import { storeToRefs } from "pinia";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { DashSeparator } from "@/shared/ui/DashSeparator";

const store = usePollsStore();
const { activePolls, activePollsError } = storeToRefs(store);

onMounted(() => store.fetchActivePolls());
</script>

<template>
  <SidebarBlock token="ActivePolls">
    <template #title>Активные опросы</template>
    <SidebarSkeleton
      v-if="activePolls === null && !activePollsError"
      :lines="3"
    />
    <li v-else-if="activePolls === null">
      <SecondaryText>
        Не удалось загрузить.
        <button
          type="button"
          class="retry-link"
          @click="store.fetchActivePolls(true)"
        >
          Повторить
        </button>
      </SecondaryText>
    </li>
    <li v-else-if="activePolls.length === 0">
      <SecondaryText>Активных опросов пока нет</SecondaryText>
    </li>
    <!-- All the cards in one list item, because the first card drops its top
         margin (PollCard's own `.poll:first-child`) and a card per item would
         make every one of them the first. -->
    <li v-else>
      <PollCard v-for="poll in activePolls" :key="poll.id" :poll="poll" />
    </li>
    <li><DashSeparator spacing="tiny" width="75%" /></li>
    <li>
      <span class="muted" aria-hidden="true">- </span
      ><router-link class="forward" :to="{ name: 'polls' }"
        >Все опросы</router-link
      >
    </li>
  </SidebarBlock>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

.forward
  font-weight: bold

.muted
  color: $text-muted

.retry-link
  +inline-link-button
</style>
