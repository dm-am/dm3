<script setup lang="ts">
/**
 * ProfileBestPostSection - displays a user's highest-rated post of all time.
 *
 * Uses the shared featured-mode GamePost component with its own
 * TruncatedContent, and fetches data via useRatedPostsStore (which wraps
 * the generic posts endpoint with author + rating-sort + take=1).
 */
import { onMounted, watch } from "vue";
import type { Username } from "@/shared/api/models/community";
import { GamePost } from "@/widgets/game-post";
import { GamePostSkeleton } from "@/shared/ui/Skeleton";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { ErrorState } from "@/shared/ui/ErrorState";
import { useRatedPostsStore } from "@/entities/game";

const props = defineProps<{
  username: Username;
}>();

const store = useRatedPostsStore();

function fetchBestPost(force = false) {
  return store.fetchBestPostOfUser(props.username, force);
}

onMounted(() => fetchBestPost());
watch(
  () => props.username,
  () => fetchBestPost(),
);
</script>

<template>
  <section class="profile-best-post">
    <GamePost
      v-if="store.bestPostOfUser(username)"
      :post="store.bestPostOfUser(username)!"
      show-navigation
      truncatable
    />
    <ErrorState
      v-else-if="store.bestPostErrorOf(username)"
      :message="store.bestPostErrorOf(username)!"
      :retry="() => fetchBestPost(true)"
    />
    <SecondaryText v-else-if="store.isLoadedBestPostOf(username)">
      Нет оцененных постов
    </SecondaryText>
    <GamePostSkeleton v-else />
  </section>
</template>
