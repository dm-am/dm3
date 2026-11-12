<template>
  <SidebarBlock token="OwnedBlogs" title="Мои блоги">
    <SidebarSkeleton
      v-if="store.participatingBlogsLoading && !store.participatingBlogs"
      :lines="skeletonLines"
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
        :always-show-counters="true"
      />
    </template>
    <li ref="rowsEnd" aria-hidden="true">
      <DashSeparator spacing="none" width="75%" />
    </li>
    <SidebarForwardLink :to="{ name: 'blogs' }">Все блоги</SidebarForwardLink>
    <!-- Anybody signed in may create a blog, and the block mounts only for them -->
    <SidebarForwardLink :to="{ name: 'create-blog' }"
      >Создать блог</SidebarForwardLink
    >
  </SidebarBlock>
</template>

<script setup lang="ts">
import SidebarBlock from "./SidebarBlock.vue";
import SidebarSkeleton from "./SidebarSkeleton.vue";
import SidebarForwardLink from "./SidebarForwardLink.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import BlogLink from "./BlogLink.vue";
import { DashSeparator } from "@/shared/ui/DashSeparator";
import { useBlogsStore } from "@/entities/blog";
import { useAuthStore } from "@/entities/user";
import { onMounted, ref } from "vue";
import { useViewerChange } from "@/shared/lib/composables/useViewerChange";
import { useRememberedLines } from "./rememberedLines";

const userStore = useAuthStore();
const store = useBlogsStore();

const rowsEnd = ref<HTMLElement | null>(null);

const skeletonLines = useRememberedLines(
  "OwnedBlogs",
  3,
  () => store.participatingBlogs?.length ?? null,
  rowsEnd,
);

// onMounted, not an immediate watch: that fired before the user store was hydrated.
onMounted(() => {
  if (userStore.user?.username) {
    store.fetchParticipatingBlogs();
  }
});

// Reset in both directions, then load for the viewer who is here now.
useViewerChange((username) => {
  store.resetParticipatingBlogs();
  if (username) store.fetchParticipatingBlogs();
});
</script>

<style scoped lang="sass">
.error
  color: $accent-red
</style>
