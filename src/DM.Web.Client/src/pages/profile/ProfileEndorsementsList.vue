<script setup lang="ts">
/**
 * ProfileEndorsementsList — the endorsements flavor of
 * `ProfileTestimonialsFrame`. Used by both pages: "Полученные рекомендации"
 * and "Написанные рекомендации".
 *
 * What is local here:
 *  - endpoint: `getUserEndorsements` (received) vs
 *    `getWrittenUserEndorsements` (written);
 *  - the card: `<TestimonialCard>` (the same component as on
 *    /about/testimonials — a single visual unit for all "testimonial-like"
 *    entities); both modes hand it the same `about` (the endorsement
 *    recipient — the server sends targetUser either way), so both pages read
 *    "<автор> о <получатель>" and the footer line is composed in exactly one
 *    place, inside the card;
 *  - the texts, including the "written" relabel of the "Автор" sort option to
 *    "Получатель": every item's author there IS the profile owner, so
 *    sorting/searching "by author" is meaningless, and the backend already
 *    silently sorts this scope by the counterparty (see
 *    UserEndorsementFilter.cs). Only the label, the hint and the search
 *    placeholder change; the sortBy value sent stays "author".
 * Everything else — filter, fetch, states, paging — is the frame.
 */
import { computed } from "vue";
import { userApi, type EndorsementsQuery } from "@/entities/user";
import type {
  UserEndorsement,
  Username,
  WebsiteTestimonial,
} from "@/shared/api/models/community";
import { TestimonialCard } from "@/entities/testimonial";
import ProfileTestimonialsFrame from "./ProfileTestimonialsFrame.vue";

const props = defineProps<{
  username: string;
  /**
   * "received" — endorsements received by this user (they are the recipient).
   * "written"  — endorsements written by this user (they are the author).
   * Determines which of userApi.getUserEndorsements /
   * getWrittenUserEndorsements is called, and (via `routeName` below)
   * where the pagination links lead.
   */
  mode: "received" | "written";
  /** The current page's route name — for Paging.to. */
  routeName: "received-endorsements" | "given-endorsements";
}>();

// Do not detach the methods — both calls go through
// `this.buildListParams(q)`, and a detached `const fn = userApi.getX` loses
// `this` and crashes with a TypeError.
function fetchPage(q: EndorsementsQuery) {
  return props.mode === "received"
    ? userApi.getUserEndorsements(props.username as Username, q)
    : userApi.getWrittenUserEndorsements(props.username as Username, q);
}

const emptyText = computed(() =>
  props.mode === "received"
    ? "У пользователя пока нет рекомендаций"
    : "Пользователь пока не писал рекомендаций",
);

const authorSortOverride = computed(() =>
  props.mode === "written"
    ? { label: "Получатель", hint: "По имени получателя" }
    : undefined,
);

const searchPlaceholder = computed(() =>
  props.mode === "written" ? "Поиск по тексту или получателю" : undefined,
);

/**
 * UserEndorsement and WebsiteTestimonial are structurally compatible
 * (id / author / text / createdUtc / modifiedUtc). `<TestimonialCard>`
 * reads only these fields — we project at the boundary instead of duplicating
 * the visuals.
 */
function asTestimonial(e: UserEndorsement): WebsiteTestimonial {
  return e as unknown as WebsiteTestimonial;
}
</script>

<template>
  <ProfileTestimonialsFrame
    :username="username"
    :mode="mode"
    :route-name="routeName"
    error-message="Не удалось загрузить рекомендации"
    :empty-text="emptyText"
    empty-filtered-text="Рекомендаций по заданным фильтрам не найдено"
    :author-sort-override="authorSortOverride"
    :search-placeholder="searchPlaceholder"
    :fetch-page="fetchPage"
  >
    <template #item="{ item, searchQuery }">
      <TestimonialCard
        :testimonial="asTestimonial(item)"
        :search-query="searchQuery"
        :about="item.targetUser"
      />
    </template>
  </ProfileTestimonialsFrame>
</template>
