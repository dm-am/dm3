<script setup lang="ts">
/**
 * GivenReviewsPage — a user's "Поставленные оценки" page.
 * A thin wrapper over the shared `RatedPostsList` scoped to this user as the
 * REVIEWER: other users' posts where they left at least one review (see the
 * `reviewerUsername` backend filter in PostsQuery).
 */
import { computed } from "vue";
import { ErrorPage } from "@/shared/ui/ErrorPage";
import type { RatedPostsScope } from "@/entities/game";
import { RatedPostsList } from "@/widgets/rated-posts";
import ProfileSubpageHeader from "./ProfileSubpageHeader.vue";
import { useProfileSubpage } from "./useProfileSubpage";

const { username, canonicalUsername, notFound, profileLink } =
  useProfileSubpage("Поставленные оценки постов");

const scope = computed<RatedPostsScope>(() => ({
  kind: "reviewer",
  username: username.value,
}));

const pagingTo = computed(() => ({
  name: "given-reviews" as const,
  params: { username: username.value },
}));
</script>

<template>
  <ErrorPage v-if="notFound" :code="404" />
  <div v-else class="given-reviews-page">
    <ProfileSubpageHeader
      label="Поставленные оценки постов"
      :username="canonicalUsername"
    >
      Чужие посты, которые оценил игрок
      <router-link :to="profileLink">{{ canonicalUsername }}</router-link>
    </ProfileSubpageHeader>

    <RatedPostsList
      :scope="scope"
      :paging-to="pagingTo"
      empty-text="Пользователь пока никого не оценивал"
      hide-author-filter
    />
  </div>
</template>

<style scoped lang="sass">
.given-reviews-page
  width: 100%
</style>
