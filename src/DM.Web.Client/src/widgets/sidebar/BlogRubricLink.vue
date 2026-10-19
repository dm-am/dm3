<script setup lang="ts">
// A rubric row of BlogPanel: "- {title} (N/A)", unread publications and comments.
import { computed } from "vue";
import { useBlogDisplay, type Rubric } from "@/entities/blog";
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

// A rubric's page is the publication feed filtered by it.
const to = computed(() => ({
  name: "blog-feed",
  params: { id: props.blogId },
  query: { rubric: props.rubric.id },
}));

const unreadPublications = computed(
  () => props.rubric.unreadPublicationsCount ?? 0,
);
const unreadComments = computed(() => props.rubric.unreadCommentsCount ?? 0);

// A guest gets totals from the server, not unread counts; the words say which, as for a blog.
const { formatUnreadPublicationsTooltip, formatUnreadCommentsTooltip } =
  useBlogDisplay();
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
      :first-label="formatUnreadPublicationsTooltip(unreadPublications)"
      :second-value="unreadComments"
      :second-to="to"
      :second-label="formatUnreadCommentsTooltip(unreadComments)"
    />
  </li>
</template>

<style scoped lang="sass">
.link
  display: block
</style>
