<template>
  <SidebarBlock token="ActiveBlogs">
    <template #title>Активные блоги</template>
    <SidebarSkeleton
      v-if="store.activeBlogs === null && !store.activeBlogsError"
      :lines="5"
    />
    <SecondaryText v-else-if="store.activeBlogs === null">
      Не удалось загрузить
    </SecondaryText>
    <SecondaryText v-else-if="store.activeBlogs.length === 0">
      Активных блогов пока нет
    </SecondaryText>
    <BlogLink
      v-else
      v-for="blog in store.activeBlogs"
      :key="blog.id"
      :blog="blog"
      :counters="true"
      :always-show-counters="!userStore.user"
    />
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
import { onMounted } from "vue";

const store = useBlogsStore();
const userStore = useUserStore();

onMounted(() => store.fetchActiveBlogs());
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
