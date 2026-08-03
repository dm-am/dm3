<script setup lang="ts">
/**
 * ReceivedReviewsPage — a user's "Полученные оценки" page.
 * A thin wrapper over the shared `RatedPostsList` scoped to this user as the
 * post AUTHOR: posts they wrote that received at least one review.
 */
import { computed } from "vue";
import { ErrorPage } from "@/shared/ui/ErrorPage";
import type { RatedPostsScope } from "@/entities/game";
import { RatedPostsList } from "@/widgets/rated-posts";
import ProfileSubpageHeader from "./ProfileSubpageHeader.vue";
import { useProfileSubpage } from "./useProfileSubpage";

const { username, canonicalUsername, notFound, profileLink } =
  useProfileSubpage("Полученные оценки постов");

const scope = computed<RatedPostsScope>(() => ({
  kind: "author",
  username: username.value,
}));

const pagingTo = computed(() => ({
  name: "received-reviews" as const,
  params: { username: username.value },
}));
</script>

<template>
  <ErrorPage v-if="notFound" :code="404" />
  <div v-else class="received-reviews-page">
    <ProfileSubpageHeader
      label="Полученные оценки постов"
      :username="canonicalUsername"
    >
      Посты игрока
      <router-link :to="profileLink">{{ canonicalUsername }}</router-link
      >, оцененные хотя бы раз другими участниками сообщества
    </ProfileSubpageHeader>

    <RatedPostsList
      :scope="scope"
      :paging-to="pagingTo"
      empty-text="У пользователя пока нет оцененных постов"
      hide-author-filter
    />
  </div>
</template>

<style scoped lang="sass">
.received-reviews-page
  width: 100%
</style>
