<script setup lang="ts">
import MenuBlock from "@/views/layout/MenuBlock.vue";
import { usePollsStore } from "@/stores/polls";
import { onMounted } from "vue";
import ThePoll from "@/views/layout/sidebar/ThePoll.vue";
import { storeToRefs } from "pinia";
import SecondaryText from "@/components/layout/SecondaryText.vue";

const store = usePollsStore();
const { activePolls } = storeToRefs(store);

onMounted(() => store.fetchActivePolls());
</script>

<template>
  <menu-block token="OpenPolls">
    <template #title>Опросы</template>
    <the-loader v-if="!activePolls" />
    <secondary-text v-else-if="!activePolls.length"
      >Нет активных опросов</secondary-text
    >
    <the-poll v-else v-for="poll in activePolls" :key="poll.id" :poll="poll" />
    <div class="all-polls-link">
      <span class="muted">- </span><router-link class="forward" :to="{ name: 'polls' }">Все опросы</router-link>
    </div>
  </menu-block>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.all-polls-link
  margin-top: $small

.forward
  font-weight: bold

.muted
  color: $text-muted
</style>
