<script setup lang="ts">
import MenuBlock from "@/widgets/menu/MenuBlock.vue";
import { usePollsStore } from "@/entities/poll";
import { onMounted } from "vue";
import ThePoll from "./ThePoll.vue";
import { storeToRefs } from "pinia";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";

const store = usePollsStore();
const { activePolls } = storeToRefs(store);

onMounted(() => store.fetchActivePolls());
</script>

<template>
  <menu-block token="OpenPolls">
    <template #title>Опросы</template>
    <secondary-text v-if="!activePolls || !activePolls.length"
      >Нет активных опросов</secondary-text
    >
    <the-poll v-else v-for="poll in activePolls" :key="poll.id" :poll="poll" />
    <div class="separator">
      - - - - - - - - - - - - - - - - - - - - - - - - - -
    </div>
    <div>
      <span class="muted">- </span><router-link class="forward" :to="{ name: 'polls' }">Все опросы</router-link>
    </div>
  </menu-block>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.separator
  color: $text-muted

.forward
  font-weight: bold

.muted
  color: $text-muted
</style>
