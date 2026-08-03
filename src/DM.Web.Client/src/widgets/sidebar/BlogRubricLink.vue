<script setup lang="ts">
// Sidebar row for a single blog rubric, used by the rubric list in
// BlogPanel (mirrors GameRoomLink). Layout:
//
//   - {title} (N/A)
//
// where N is the current user's unread-publication count and A the unread-
// comment count for the rubric (dev doc 4.2.1.4). Both values link to the
// rubric feed. The dev doc also specifies a lock icon for restricted rubrics;
// the backend Rubric DTO carries no access data yet, so that is not rendered.
import { computed } from "vue";
import type { Rubric } from "@/entities/blog";
import { CounterPair } from "@/shared/ui/CounterPair";

const props = withDefaults(
  defineProps<{
    rubric: Rubric;
    /** Blog id (public id or GUID) used to build the feed route params. */
    blogId: string;
    prefix?: string;
  }>(),
  { prefix: "- " },
);

// Rubric pages are the publication feed filtered by rubric
// (URL_STRUCTURE: /blogs/{publicId}/feed?rubric=...).
const to = computed(() => ({
  name: "blog-feed",
  params: { id: props.blogId },
  query: { rubric: props.rubric.id },
}));

// N = unread publications, A = unread comments (dev doc 4.2.1.4).
const unreadPublications = computed(
  () => props.rubric.unreadPublicationsCount ?? 0,
);
const unreadComments = computed(() => props.rubric.unreadCommentsCount ?? 0);
</script>

<template>
  <li class="link">
    <span v-if="prefix" class="muted" aria-hidden="true">{{ prefix }}</span>
    <router-link class="title" :to="to">{{ rubric.title }}</router-link
    >{{ " "
    }}<CounterPair
      class="counters"
      :first-value="unreadPublications"
      :first-to="to"
      :first-label="`Непрочитанных публикаций: ${unreadPublications}`"
      :second-value="unreadComments"
      :second-to="to"
      :second-label="`Непрочитанных комментариев: ${unreadComments}`"
    />
  </li>
</template>

<style scoped lang="sass">
.link
  display: block

.muted
  color: $text-muted
</style>
