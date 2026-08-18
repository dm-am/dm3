<script setup lang="ts">
/**
 * ProfileSubpage — the one component behind the six list subpages of a
 * profile: received/given post ratings, endorsements and game reviews. The
 * router points all six routes here and names the subpage in the `subpage`
 * prop (the key is the route's own name); everything the six wrappers used to
 * spell separately — the 404, the header, the lead phrase around the profile
 * link and the list underneath — is a row in SUBPAGES.
 *
 * The lead phrase is stored in two halves around the link, spaces and
 * punctuation included, so the copy stays exactly what the six pages
 * rendered.
 *
 * Because all six routes resolve to this component, navigating between two
 * subpages swaps props without a remount — so everything here derives from
 * `cfg` reactively, and the lists themselves already reset on a mode/scope
 * change.
 */
import { computed } from "vue";
import { ErrorPage } from "@/shared/ui/ErrorPage";
import { RatedPostsList } from "@/widgets/rated-posts";
import ProfileSubpageHeader from "./ProfileSubpageHeader.vue";
import ProfileGameReviewsList from "./ProfileGameReviewsList.vue";
import ProfileEndorsementsList from "./ProfileEndorsementsList.vue";
import { useProfileSubpage } from "./useProfileSubpage";

/** What stands under the header: which list component, scoped how. */
type SubpageList =
  | {
      kind: "game-reviews";
      mode: "received" | "written";
      routeName: "received-game-reviews" | "given-game-reviews";
    }
  | {
      kind: "endorsements";
      mode: "received" | "written";
      routeName: "received-endorsements" | "given-endorsements";
    }
  | { kind: "rated-posts"; scope: "author" | "reviewer"; emptyText: string };

interface SubpageConfig {
  /** The H1 part before the colon; also the document-title segment. */
  label: string;
  /** Lead phrase before the profile link, trailing space included. */
  before: string;
  /** Lead phrase after the profile link, leading punctuation included. */
  after: string;
  list: SubpageList;
}

const SUBPAGES: Record<
  | "received-reviews"
  | "given-reviews"
  | "received-endorsements"
  | "given-endorsements"
  | "received-game-reviews"
  | "given-game-reviews",
  SubpageConfig
> = {
  // The post-rating pair: the shared RatedPostsList scoped to this user as
  // the post author (received) or as the reviewer (given). The author filter
  // is hidden because the scope already fixes the author.
  "received-reviews": {
    label: "Полученные оценки постов",
    before: "Посты игрока ",
    after: ", оцененные хотя бы раз другими участниками сообщества",
    list: {
      kind: "rated-posts",
      scope: "author",
      emptyText: "У пользователя пока нет оцененных постов",
    },
  },
  "given-reviews": {
    label: "Поставленные оценки постов",
    before: "Чужие посты, которые оценил игрок ",
    after: "",
    list: {
      kind: "rated-posts",
      scope: "reviewer",
      emptyText: "Пользователь пока никого не оценивал",
    },
  },
  "received-endorsements": {
    label: "Полученные рекомендации",
    before: "Что участники сообщества пишут об игроке ",
    after: "",
    list: {
      kind: "endorsements",
      mode: "received",
      routeName: "received-endorsements",
    },
  },
  "given-endorsements": {
    label: "Написанные рекомендации",
    before: "Что игрок ",
    after: " пишет о других участниках сообщества",
    list: {
      kind: "endorsements",
      mode: "written",
      routeName: "given-endorsements",
    },
  },
  // Reviews of whole games, not of posts — the profile shows the two pairs as
  // different counters.
  "received-game-reviews": {
    label: "Полученные рецензии на игры",
    before: "Что участники сообщества пишут об играх, которые ведет ",
    after: "",
    list: {
      kind: "game-reviews",
      mode: "received",
      routeName: "received-game-reviews",
    },
  },
  "given-game-reviews": {
    label: "Написанные рецензии на игры",
    before: "Что игрок ",
    after: " пишет об играх сообщества",
    list: {
      kind: "game-reviews",
      mode: "written",
      routeName: "given-game-reviews",
    },
  },
};

const props = defineProps<{
  /** Which subpage this route is — the key is the route's own name. */
  subpage: keyof typeof SUBPAGES;
}>();

const cfg = computed(() => SUBPAGES[props.subpage]);

const { username, canonicalUsername, notFound, profileLink } =
  useProfileSubpage(() => cfg.value.label);

// Pre-narrowed union members, so the template branches without casts.
const gameReviews = computed(() => {
  const list = cfg.value.list;
  return list.kind === "game-reviews" ? list : null;
});
const endorsements = computed(() => {
  const list = cfg.value.list;
  return list.kind === "endorsements" ? list : null;
});
const ratedPosts = computed(() => {
  const list = cfg.value.list;
  return list.kind === "rated-posts" ? list : null;
});

const pagingTo = computed(() => ({
  name: props.subpage,
  params: { username: username.value },
}));
</script>

<template>
  <ErrorPage v-if="notFound" :code="404" />
  <div v-else class="profile-subpage">
    <ProfileSubpageHeader :label="cfg.label" :username="canonicalUsername">
      {{ cfg.before
      }}<router-link :to="profileLink">{{ canonicalUsername }}</router-link
      >{{ cfg.after }}
    </ProfileSubpageHeader>

    <ProfileGameReviewsList
      v-if="gameReviews"
      :username="username"
      :mode="gameReviews.mode"
      :route-name="gameReviews.routeName"
    />
    <ProfileEndorsementsList
      v-else-if="endorsements"
      :username="username"
      :mode="endorsements.mode"
      :route-name="endorsements.routeName"
    />
    <RatedPostsList
      v-else-if="ratedPosts"
      :scope="{ kind: ratedPosts.scope, username }"
      :paging-to="pagingTo"
      :empty-text="ratedPosts.emptyText"
      hide-author-filter
    />
  </div>
</template>

<style scoped lang="sass">
.profile-subpage
  width: 100%
</style>
