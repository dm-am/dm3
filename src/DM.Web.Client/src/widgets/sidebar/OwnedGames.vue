<template>
  <SidebarBlock token="OwnedGames" title="Мои игры">
    <SidebarSkeleton
      v-if="store.participatingGamesLoading && !store.participatingGames"
      :lines="5"
    />
    <li v-else-if="store.participatingGamesError">
      <SecondaryText class="error">Не удалось загрузить</SecondaryText>
    </li>
    <li
      v-else-if="
        !store.participatingGames || store.participatingGames.length === 0
      "
    >
      <SecondaryText>У вас пока нет игр</SecondaryText>
    </li>
    <template v-else>
      <template v-for="(group, at) in groups" :key="group.key">
        <li v-if="at > 0" aria-hidden="true">
          <DashSeparator spacing="none" width="75%" />
        </li>
        <SidebarGameLink
          v-for="game in group.games"
          :key="game.id"
          :game="game"
          :always-show-counters="true"
          wait-marker
          :prefix="group.prefix"
        />
      </template>
    </template>
    <li aria-hidden="true">
      <DashSeparator spacing="none" width="75%" />
    </li>
    <SidebarForwardLink :to="{ name: 'games', query: { status: 'Active' } }"
      >Все активные игры</SidebarForwardLink
    >
    <SidebarForwardLink :to="{ name: 'create-game' }"
      >Создать игру</SidebarForwardLink
    >
  </SidebarBlock>
</template>

<script setup lang="ts">
import SidebarBlock from "./SidebarBlock.vue";
import SidebarSkeleton from "./SidebarSkeleton.vue";
import SidebarForwardLink from "./SidebarForwardLink.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import SidebarGameLink from "./SidebarGameLink.vue";
import { DashSeparator } from "@/shared/ui/DashSeparator";
import { useAuthStore } from "@/entities/user";
import { useGamesStore } from "@/entities/game";
import { onMounted } from "vue";
import { useViewerChange } from "@/shared/lib/composables/useViewerChange";
import { useLeftSidebarGames } from "./leftSidebarRows";

const userStore = useAuthStore();
const store = useGamesStore();

/** The curated, the led, the played and the read games, a separator between two groups. */
const { own: groups } = useLeftSidebarGames();

// onMounted, not an immediate watch: that fired before the user store was hydrated.
onMounted(() => {
  if (userStore.user?.username) {
    store.fetchParticipatingGames();
  }
});

// Reset first, then load for the viewer who is here now: the old rows must not stand meanwhile.
useViewerChange((username) => {
  store.resetParticipatingGames();
  if (username) store.fetchParticipatingGames();
});
</script>

<style scoped lang="sass">
.error
  color: $accent-red
</style>
