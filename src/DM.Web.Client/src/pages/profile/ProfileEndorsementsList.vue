<script setup lang="ts">
/**
 * The recommendations of a profile on ProfileTestimonialsFrame, received or
 * written, on TestimonialCard. In "written" every author is the owner, so the
 * "Автор" sort is relabelled "Получатель"; the server sorts that scope by the
 * recipient and the value sent stays "author".
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
import type {
  EndorsementsSubpageName,
  TestimonialSide,
} from "./profileSubpages";

const props = defineProps<{
  username: string;
  /** Received: recommendations about this user; written: the ones he wrote. */
  mode: TestimonialSide;
  /** The route of the page, for the paging links. */
  routeName: EndorsementsSubpageName;
}>();

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

/** The card of a review of the site names its author, whom a removed account leaves out. */
function signed(
  e: UserEndorsement,
): e is UserEndorsement & Pick<WebsiteTestimonial, "author"> {
  return !!e.author;
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
        v-if="signed(item)"
        :testimonial="item"
        :search-query="searchQuery"
        :about="item.targetUser"
      />
    </template>
  </ProfileTestimonialsFrame>
</template>
