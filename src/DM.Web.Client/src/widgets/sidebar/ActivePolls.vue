<template>
  <SidebarEntityList
    token="ActivePolls"
    title="Активные опросы"
    :lines="3"
    :items="activePolls"
    :errored="!!activePollsError"
    empty="Активных опросов пока нет"
    :retry="() => store.fetchActivePolls(true)"
    :forward-to="{ name: 'polls' }"
    forward-label="Все опросы"
  >
    <template #item="{ item }">
      <li><PollCard :poll="item" /></li>
    </template>
  </SidebarEntityList>
</template>

<script setup lang="ts">
import { onMounted } from "vue";
import { storeToRefs } from "pinia";
import SidebarEntityList from "./SidebarEntityList.vue";
import PollCard from "./PollCard.vue";
import { usePollsStore } from "@/entities/poll";

const store = usePollsStore();
const { activePolls, activePollsError } = storeToRefs(store);

onMounted(() => store.fetchActivePolls());
</script>
