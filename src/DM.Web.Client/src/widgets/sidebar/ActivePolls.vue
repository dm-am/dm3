<script setup lang="ts">
import SidebarBlock from "./SidebarBlock.vue";
import SidebarSkeleton from "./SidebarSkeleton.vue";
import { usePollsStore } from "@/entities/poll";
import { onMounted } from "vue";
import Poll from "./Poll.vue";
import { storeToRefs } from "pinia";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";

const store = usePollsStore();
const { activePolls } = storeToRefs(store);

onMounted(() => store.fetchActivePolls());
</script>

<template>
  <SidebarBlock token="ActivePolls">
    <template #title>Активные опросы</template>
    <SidebarSkeleton v-if="activePolls === null" :lines="3" />
    <SecondaryText v-else-if="activePolls.length === 0">
      Нет активных опросов
    </SecondaryText>
    <Poll v-else v-for="poll in activePolls" :key="poll.id" :poll="poll" />
    <div class="separator">
      - - - - - - - - - - - - - - - - - - - - - - - - - -
    </div>
    <div>
      <span class="muted">- </span
      ><router-link class="forward" :to="{ name: 'polls' }"
        >Все опросы</router-link
      >
    </div>
  </SidebarBlock>
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
