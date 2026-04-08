<template>
  <SidebarBlock token="OwnedBlogs">
    <template #title>Мои блоги</template>
    <SidebarSkeleton v-if="store.participatingBlogsLoading && !store.participatingBlogs" :lines="3" />
    <SecondaryText v-else-if="store.participatingBlogsError" class="error">
      {{ store.participatingBlogsError.title || "Ошибка загрузки" }}
    </SecondaryText>
    <template v-else-if="!store.participatingBlogs || store.participatingBlogs.length === 0">
      <SecondaryText>У вас нет блогов</SecondaryText>
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
    <div class="separator">
      - - - - - - - - - - - - - - - - - - - - - - - - - -
    </div>
    <div>
      <span class="muted">- </span>
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
import { useUserStore } from "@/entities/user";
import { watch } from "vue";

const userStore = useUserStore();
const store = useBlogsStore();

// Watch for user login/logout
watch(
  () => userStore.user?.username,
  (newUsername, oldUsername) => {
    if (newUsername && newUsername !== oldUsername) {
      store.fetchParticipatingBlogs();
    } else if (!newUsername && oldUsername) {
      store.resetParticipatingBlogs();
    }
  },
  { immediate: true },
);
</script>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.separator
  color: $text-muted

.forward
  font-weight: bold

.muted
  color: $text-muted

.error
  color: $accent-red
</style>
