<script setup lang="ts">
/**
 * ProfileBestPost - displays a user's highest-rated post of all time.
 *
 * Uses the shared featured-mode GamePost component with its own
 * TruncatedContent, and fetches data via useRatedPostsStore (which wraps
 * the generic posts endpoint with author + rating-sort + take=1).
 */
import { onMounted, watch } from "vue";
import type { Username } from "@/shared/api/models/community";
import { GamePost } from "@/pages/game";
import { GamePostSkeleton } from "@/shared/ui/Skeleton";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import { useRatedPostsStore } from "@/entities/game";

const props = defineProps<{
  username: Username;
}>();

const store = useRatedPostsStore();

onMounted(() => store.fetchBestPostOfUser(props.username));
watch(
  () => props.username,
  (next) => store.fetchBestPostOfUser(next),
);
</script>

<template>
  <section class="profile-best-post">
    <BlockTitle>Лучший пост за все время</BlockTitle>
    <GamePost
      v-if="store.bestPostOfUser(username)"
      :post="store.bestPostOfUser(username)!"
      show-navigation
      truncatable
    />
    <SecondaryText v-else-if="store.isLoadedBestPostOf(username)">
      Нет оцененных постов
    </SecondaryText>
    <GamePostSkeleton v-else />
  </section>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"

.profile-best-post
  margin-bottom: $medium
</style>
