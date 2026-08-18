<template>
  <SidebarBlock token="OwnedBlogs">
    <template #title>Мои блоги</template>
    <SidebarSkeleton
      v-if="store.participatingBlogsLoading && !store.participatingBlogs"
      :lines="3"
    />
    <li v-else-if="store.participatingBlogsError">
      <SecondaryText class="error">Не удалось загрузить</SecondaryText>
    </li>
    <li
      v-else-if="
        !store.participatingBlogs || store.participatingBlogs.length === 0
      "
    >
      <SecondaryText>У вас пока нет блогов</SecondaryText>
    </li>
    <template v-else>
      <BlogLink
        v-for="blog in store.participatingBlogs"
        :key="blog.id"
        :blog="blog"
        :counters="true"
        :always-show-counters="true"
      />
    </template>
    <li aria-hidden="true">
      <DashSeparator spacing="none" width="75%" />
    </li>
    <li>
      <span class="muted" aria-hidden="true">- </span>
      <router-link class="forward" :to="{ name: 'blogs' }"
        >Все блоги</router-link
      >
    </li>
    <!--
      Pairs with "Создать игру" in OwnedGames. BlogIntention.Create admits any
      authenticated user, and this block only mounts for one (LeftSidebar's
      v-if), so the row promises exactly what the server grants.
    -->
    <li>
      <span class="muted" aria-hidden="true">- </span>
      <router-link class="forward" :to="{ name: 'create-blog' }"
        >Создать блог</router-link
      >
    </li>
  </SidebarBlock>
</template>

<script setup lang="ts">
import SidebarBlock from "./SidebarBlock.vue";
import SidebarSkeleton from "./SidebarSkeleton.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import BlogLink from "./BlogLink.vue";
import { DashSeparator } from "@/shared/ui/DashSeparator";
import { useBlogsStore } from "@/entities/blog";
import { useAuthStore } from "@/entities/user";
import { onMounted } from "vue";
import { useViewerChange } from "@/shared/lib/composables/useViewerChange";

const userStore = useAuthStore();
const store = useBlogsStore();

// Initial fetch on mount. Gated by `v-if="userStore.user"` in
// LeftSidebar so this only runs when the user is authenticated.
// Avoids the race between watch { immediate: true } and user store
// hydration that can otherwise fire a fetch with a null user.
onMounted(() => {
  if (userStore.user?.username) {
    store.fetchParticipatingBlogs();
  }
});

// Same rule as OwnedGames: reset in both directions, load for the viewer who
// is here now. A sign-in in a second tab replaces the name in one step.
useViewerChange((username) => {
  store.resetParticipatingBlogs();
  if (username) store.fetchParticipatingBlogs();
});
</script>

<style scoped lang="sass">
.forward
  font-weight: bold

.muted
  color: $text-muted

.error
  color: $accent-red
</style>
