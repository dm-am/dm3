<template>
  <menu-block v-if="userStore.user && hasContent" token="Subscriptions">
    <template #title>Подписки</template>

    <!-- Subscribed Games -->
    <template v-if="gamesStore.subscribedGames && gamesStore.subscribedGames.length > 0">
      <game-menu-link
        v-for="game in gamesStore.subscribedGames"
        :key="game.id"
        :game="game"
        :counters="true"
        :alwaysShowCounters="false"
      />
    </template>

    <!-- Subscribed Blogs -->
    <template v-if="blogsStore.subscribedBlogs && blogsStore.subscribedBlogs.length > 0">
      <div
        v-for="blog in blogsStore.subscribedBlogs"
        :key="blog.id"
        class="blog-link"
      >
        <span class="muted">- </span>
        <router-link :to="{ name: 'blog', params: { id: blog.id } }">
          {{ blog.title }}
        </router-link>
      </div>
    </template>

    <!-- Empty state -->
    <secondary-text v-if="!hasContent">
      Нет подписок
    </secondary-text>
  </menu-block>
</template>

<script setup lang="ts">
import { computed, watch } from "vue";
import MenuBlock from "@/widgets/menu/MenuBlock.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import GameMenuLink from "@/widgets/menu/GameMenuLink.vue";
import { useGamesStore } from "@/entities/game";
import { useBlogsStore } from "@/entities/blog";
import { useUserStore } from "@/entities/user";

const gamesStore = useGamesStore();
const blogsStore = useBlogsStore();
const userStore = useUserStore();

const hasContent = computed(() => {
  const gamesCount = gamesStore.subscribedGames?.length ?? 0;
  const blogsCount = blogsStore.subscribedBlogs?.length ?? 0;
  return gamesCount > 0 || blogsCount > 0;
});

// Fetch subscribed content when user logs in
watch(
  () => userStore.user,
  (user) => {
    if (user) {
      gamesStore.fetchSubscribedGames();
      blogsStore.fetchSubscribedBlogs();
    } else {
      gamesStore.resetSubscribedGames();
      blogsStore.resetSubscribedBlogs();
    }
  },
  { immediate: true }
);
</script>

<style scoped lang="sass">
.blog-link
  font-size: 0.9rem

  .muted
    color: var(--text-muted)
</style>
