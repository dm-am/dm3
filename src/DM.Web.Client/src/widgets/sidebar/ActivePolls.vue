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
    <SecondaryText v-else-if="activePolls === null">
      Не удалось загрузить.
      <button
        type="button"
        class="retry-link"
        @click="store.fetchActivePolls(true)"
      >
        Повторить
      </button>
    </SecondaryText>
    <SecondaryText v-else-if="activePolls.length === 0">
      Активных опросов пока нет
    </SecondaryText>
    <PollCard v-else v-for="poll in activePolls" :key="poll.id" :poll="poll" />
    <DashSeparator spacing="tiny" width="75%" />
    <div>
      <span class="muted" aria-hidden="true">- </span
      ><router-link class="forward" :to="{ name: 'polls' }"
        >Все опросы</router-link
      >
    </div>
  </SidebarBlock>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"

.forward
  font-weight: bold

.muted
  color: $text-muted

.retry-link
  +inline-link-button
</style>
