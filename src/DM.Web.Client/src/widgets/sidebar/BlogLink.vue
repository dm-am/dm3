<script setup lang="ts">
import { useBlogDisplay, type Blog, type BlogRef } from "@/entities/blog";
import { Tooltip } from "@/shared/ui/Tooltip";
import { CounterPair } from "@/shared/ui/CounterPair";
import { computed, ref } from "vue";

const props = withDefaults(
  defineProps<{
    blog: Blog | BlogRef;
    alwaysShowCounters?: boolean;
    prefix?: string;
  }>(),
  {
    prefix: "- ",
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

const isClosedBlog = computed(() => props.blog.status === "Closed");

const isNewBlog = computed(() => isNew(props.blog));

const blogRoute = computed(() => ({
  name: "blog",
  params: { id: props.blog.publicId ?? props.blog.id },
}));
const hovered = ref(false);
const showCounters = computed(() => props.alwaysShowCounters || hovered.value);

const publicationsCount = computed(() => getUnreadPublications(props.blog));
const commentsCount = computed(() => getUnreadComments(props.blog));

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
@use "@/assets/styles/Links" as *

.link
  display: block

.new-item
  +link-tone($accent-green, $accent-green-hover)

.closed-item
  +muted-link
</style>
