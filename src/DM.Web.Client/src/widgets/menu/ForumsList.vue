<script setup lang="ts">
import MenuBlock from "./MenuBlock.vue";
import { useBoardsStore } from "@/entities/forum";
import { useUserStore } from "@/entities/user";
import { watch } from "vue";
import { storeToRefs } from "pinia";

const store = useBoardsStore();
const { boards } = storeToRefs(store);
const { fetchBoards } = store;
const { user } = storeToRefs(useUserStore());

watch(
  () => user.value?.username,
  () => fetchBoards(),
  { immediate: true },
);
</script>

<template>
  <menu-block token="boards">
    <template #title>Форум</template>
    <secondary-text v-if="!boards || !boards.length">Нет разделов форума</secondary-text>
    <div v-else v-for="forum in boards" :key="forum.id">
      <span class="muted">- </span
      ><router-link :to="{ name: 'forum', params: { id: forum.id } }">{{
        forum.id
      }}</router-link> <span class="muted">(</span
      ><router-link
        :to="{ name: 'forum', params: { id: forum.id } }"
        >{{ forum.commentsCount || 0 }}</router-link
      ><span class="muted">)</span>
    </div>
  </menu-block>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.muted
  color: $text-muted
</style>
