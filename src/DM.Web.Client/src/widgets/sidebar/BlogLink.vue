<script setup lang="ts">
import { useBlogDisplay, type Blog, type BlogRef } from "@/entities/blog";
import { Tooltip } from "@/shared/ui/Tooltip";
import { CounterPair } from "@/shared/ui/CounterPair";
import { computed, ref } from "vue";

const props = withDefaults(
  defineProps<{
    blog: Blog | BlogRef;
    counters: boolean;
    alwaysShowCounters?: boolean;
    prefix?: string;
    /** Show author and assistants */
    showAuthors?: boolean;
  }>(),
  {
    prefix: "- ",
    showAuthors: false,
  },
);

const {
  buildTooltip,
  getUnreadPublications,
  getUnreadComments,
  formatUnreadPublicationsTooltip,
  formatUnreadCommentsTooltip,
  isNew,
} = useBlogDisplay();

// Closed blog: muted grey until hover. Muted takes priority over "new".
const isClosedBlog = computed(() => props.blog.status === "Closed");

// Blog is "new" if activated < 7 days ago
const isNewBlog = computed(() => !isClosedBlog.value && isNew(props.blog));

// Prefer the short public id for the URL; fall back to the GUID (the blog
// route is publicId-tolerant on the backend).
const blogRoute = computed(() => ({
  name: "blog",
  params: { id: props.blog.publicId ?? props.blog.id },
}));
const hovered = ref(false);
const showCounters = computed(
  () => props.counters && (props.alwaysShowCounters || hovered.value),
);

// Counter values using shared composable
const publicationsCount = computed(() => getUnreadPublications(props.blog));
const commentsCount = computed(() => getUnreadComments(props.blog));

// Tooltips using shared composable
const publicationsTooltip = computed(() =>
  formatUnreadPublicationsTooltip(publicationsCount.value),
);
const commentsTooltip = computed(() =>
  formatUnreadCommentsTooltip(commentsCount.value),
);
const blogTooltip = computed(() => buildTooltip(props.blog));
</script>

<template>
  <li class="link" @mouseenter="hovered = true" @mouseleave="hovered = false">
    <span class="muted" aria-hidden="true">{{ prefix }}</span>
    <Tooltip :text="blogTooltip">
      <router-link
        :to="blogRoute"
        :class="{ 'new-item': isNewBlog, 'closed-item': isClosedBlog }"
        >{{ blog.title }}</router-link
      > </Tooltip
    >{{ " "
    }}<CounterPair
      v-if="showCounters"
      class="counters"
      :first-value="publicationsCount"
      :first-to="blogRoute"
      :first-label="publicationsTooltip"
      :second-value="commentsCount"
      :second-to="blogRoute"
      :second-label="commentsTooltip"
    />
  </li>
</template>

<style scoped lang="sass">
.link
  display: block

.muted
  color: $text-muted

.new-item
  color: $accent-green
  &:hover
    color: $accent-green-hover

// Closed blogs: muted grey; on hover — the regular link behavior.
.closed-item
  color: $text-muted
  &:hover
    color: $link-hover

.counters
  transition: opacity 0.15s ease
</style>
