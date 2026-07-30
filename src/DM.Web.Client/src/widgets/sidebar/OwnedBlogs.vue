<template>
  <SidebarBlock token="OwnedBlogs">
    <template #title>Мои блоги</template>
    <SidebarSkeleton
      v-if="store.participatingBlogsLoading && !store.participatingBlogs"
      :lines="3"
    />
    <SecondaryText v-else-if="store.participatingBlogsError" class="error">
      Не удалось загрузить
    </SecondaryText>
    <template
      v-else-if="
        !store.participatingBlogs || store.participatingBlogs.length === 0
      "
    >
      <SecondaryText>У вас пока нет блогов</SecondaryText>
    </template>
    <template v-else>
      <BlogLink
        v-for="blog in store.participatingBlogs"
        :key="blog.id"
        :blog="blog"
        :counters="true"
        :always-show-counters="true"
      />
    </template>
    <div class="separator" aria-hidden="true">
      - - - - - - - - - - - - - - - - - - - - - - - - - -
    </div>
    <div>
      <span class="muted" aria-hidden="true">- </span>
      <router-link class="forward" :to="{ name: 'blogs' }"
        >Все блоги</router-link
      >
    </div>
  </SidebarBlock>
</template>

<script setup lang="ts">
import SidebarBlock from "./SidebarBlock.vue";
import SidebarSkeleton from "./SidebarSkeleton.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import BlogLink from "./BlogLink.vue";
import { useBlogsStore } from "@/entities/blog";
import { useAuthStore } from "@/entities/user";
import { onMounted, watch } from "vue";

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

watch(
  () => userStore.user?.username,
  (newUsername, oldUsername) => {
    if (!oldUsername && newUsername) {
      store.fetchParticipatingBlogs();
    } else if (oldUsername && !newUsername) {
      store.resetParticipatingBlogs();
    }
  },
);
</script>

<style scoped lang="sass">
.separator
  color: $text-muted

.forward
  font-weight: bold

.muted
  color: $text-muted

.error
  color: $accent-red
</style>
