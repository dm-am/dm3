<script setup lang="ts">
import { computed, watch } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useCommunityStore } from "@/stores/community";
import { useUserStore } from "@/stores/user";
import { extractNumberParam } from "@/router";
import { UserActivityFilter, UserRole } from "@/api/models/community";

const route = useRoute();
const { fetchUsers } = useCommunityStore();
const { user } = storeToRefs(useUserStore());

const filter = computed(() => (route.meta.filter as string) ?? "Active");

const isSeniorModeratorOrAbove = computed(() =>
  user.value?.roles?.some((r) =>
    [UserRole.Admin, UserRole.SeniorModerator].includes(r)
  ) ?? false
);

function doFetch() {
  fetchUsers(
    extractNumberParam(route.params.n),
    filter.value as UserActivityFilter
  );
}

watch([() => route.params.n, () => route.meta.filter], doFetch, { immediate: true });
</script>

<template>
  <page-title>Сообщество</page-title>

  <nav class="community-tabs">
    <router-link :to="{ name: 'community' }" class="tabs-link">
      Активные
    </router-link>
    <router-link :to="{ name: 'community-all' }" class="tabs-link">
      Все
    </router-link>
    <router-link
      v-if="isSeniorModeratorOrAbove"
      :to="{ name: 'community-pending' }"
      class="tabs-link"
    >
      Неактивированные
    </router-link>
  </nav>

  <router-view />
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.community-tabs
  margin-bottom: $medium

  .tabs-link
    display: inline-block
    margin-right: $medium
    text-transform: uppercase
    font-weight: bold
    color: $link-nav
    text-decoration: none

    &:hover
      color: $link-nav-hover
      text-decoration: underline

    &.router-link-exact-active
      color: $text
      text-decoration: none
      cursor: default
</style>
