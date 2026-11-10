<script setup lang="ts">
/**
 * The one component behind the six list subpages of a profile, named by the
 * route in `subpage`; each is a row of SUBPAGES. Moving between two of them
 * swaps props without a remount, so everything derives from `cfg`.
 */
import { computed } from "vue";
import { ErrorPage } from "@/shared/ui/ErrorPage";
import { RatedPostsList } from "@/widgets/rated-posts";
import ProfileSubpageHeader from "./ProfileSubpageHeader.vue";
import ProfileGameReviewsList from "./ProfileGameReviewsList.vue";
import ProfileEndorsementsList from "./ProfileEndorsementsList.vue";
import { useProfileSubpage } from "./useProfileSubpage";
import type {
  EndorsementsSubpageName,
  GameReviewsSubpageName,
  ProfileSubpageName,
  TestimonialSide,
} from "./profileSubpages";

/** What stands under the header: which list component, scoped how. */
type SubpageList =
  | {
      kind: "game-reviews";
      mode: TestimonialSide;
      routeName: GameReviewsSubpageName;
    }
  | {
      kind: "endorsements";
      mode: TestimonialSide;
      routeName: EndorsementsSubpageName;
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

const SUBPAGES: Record<ProfileSubpageName, SubpageConfig> = {
  // Post ratings scoped to this user as the author (received) or the reviewer (given).
  "received-reviews": {
    label: "Полученные оценки постов",
    before: "Посты пользователя ",
    after: ", оцененные хотя бы раз другими участниками сообщества",
    list: {
      kind: "rated-posts",
      scope: "author",
      emptyText: "У пользователя пока нет оцененных постов",
    },
  },
  "given-reviews": {
    label: "Поставленные оценки постов",
    before: "Чужие посты, которые оценил пользователь ",
    after: "",
    list: {
      kind: "rated-posts",
      scope: "reviewer",
      emptyText: "Пользователь пока никого не оценивал",
    },
  },
  "received-endorsements": {
    label: "Полученные рекомендации",
    before: "Что участники сообщества пишут о пользователе ",
    after: "",
    list: {
      kind: "endorsements",
      mode: "received",
      routeName: "received-endorsements",
    },
  },
  "given-endorsements": {
    label: "Написанные рекомендации",
    before: "Что пользователь ",
    after: " пишет о других участниках сообщества",
    list: {
      kind: "endorsements",
      mode: "written",
      routeName: "given-endorsements",
    },
  },
  // Reviews of whole games, not of posts.
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
    before: "Что пользователь ",
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
