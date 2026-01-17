<script setup lang="ts">
import MenuBlock from "@/views/layout/MenuBlock.vue";
import { useBoardsStore, useUserStore } from "@/stores";
import { watch } from "vue";
import { storeToRefs } from "pinia";

const store = useBoardsStore();
const { boards } = storeToRefs(store);
const { fetchBoards } = store;
const { user } = storeToRefs(useUserStore());

watch(
  () => user.value?.login,
  () => fetchBoards(),
  { immediate: true },
);
</script>

<template>
  <menu-block token="boards">
    <template #title>Форум</template>
    <the-loader v-if="!boards" />
    <div v-else v-for="forum in boards!" :key="forum.id">
      <span class="muted">-</span> <router-link :to="{ name: 'forum', params: { id: forum.id } }">{{ forum.id }}</router-link> <span class="muted">(</span><router-link
        :to="{ name: 'forum', params: { id: forum.id } }"
        :class="{ unread: forum.unreadTopicsCount > 0 }"
      >{{ forum.unreadTopicsCount || 0 }}</router-link><span class="muted">)</span>
    </div>
  </menu-block>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.muted
  +theme(color, $secondary-text)

.unread
  font-weight: bold
  +theme(color, $positive-text)
</style>
