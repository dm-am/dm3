<template>
  <SidebarBlock token="PopularGames">
    <template #title>Популярные игры</template>
    <SidebarSkeleton
      v-if="store.popularGames === null && !failed"
      :lines="10"
    />
    <SecondaryText v-else-if="store.popularGames === null">
      Не удалось загрузить
    </SecondaryText>
    <SecondaryText v-else-if="store.popularGames.length === 0">
      Популярных игр пока нет
    </SecondaryText>
    <template v-else>
      <GameLink
        v-for="game in store.popularGames"
        :key="game.id"
        :game="game"
        :counters="true"
        :alwaysShowCounters="!userStore.user"
      />
    </template>
    <div class="separator">
      - - - - - - - - - - - - - - - - - - - - - - - - - -
    </div>
    <div>
      <span class="muted">- </span>
      <router-link
        class="forward"
        :to="{
          name: 'games',
          query: { sortBy: 'popularity', sortOrder: 'desc' },
        }"
        >Все популярные игры</router-link
      >
    </div>
  </SidebarBlock>
</template>

<script setup lang="ts">
import SidebarBlock from "./SidebarBlock.vue";
import SidebarSkeleton from "./SidebarSkeleton.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import GameLink from "./GameLink.vue";
import { useGamesStore } from "@/entities/game";
import { useUserStore } from "@/entities/user";
import { onMounted, ref } from "vue";

const store = useGamesStore();
const userStore = useUserStore();

// The games store does not expose an error ref for this list, so detect
// failure locally: when the fetch settles and the list is still null,
// the request failed (prevents an eternal skeleton).
const failed = ref(false);

onMounted(async () => {
  await store.fetchPopularGames();
  failed.value = store.popularGames === null;
});
</script>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.muted
  color: $text-muted

.forward
  font-weight: bold

.separator
  color: $text-muted
</style>
