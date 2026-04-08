<script setup lang="ts">
import { useBlogDisplay, type Blog, type BlogRef } from "@/entities/blog";
import { Tooltip } from "@/shared/ui/Tooltip";
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

// Blog is "new" if activated < 7 days ago
const isNewBlog = computed(() => isNew(props.blog));

// TODO: Change to { name: 'blog', params: { id: props.blog.id } } when blog detail page exists
const blogRoute = computed(() => ({ name: "blogs" }));
const hovered = ref(false);
const showCounters = computed(
  () => props.counters && (props.alwaysShowCounters || hovered.value),
);

// Counter values using shared composable
const publicationsCount = computed(() => getUnreadPublications(props.blog));
const commentsCount = computed(() => getUnreadComments(props.blog));

// Tooltips using shared composable
const publicationsTooltip = computed(() => formatUnreadPublicationsTooltip(publicationsCount.value));
const commentsTooltip = computed(() => formatUnreadCommentsTooltip(commentsCount.value));
const blogTooltip = computed(() => buildTooltip(props.blog));
</script>

<template>
  <div class="link" @mouseenter="hovered = true" @mouseleave="hovered = false">
    <span class="muted" aria-hidden="true">{{ prefix }}</span>
    <Tooltip :text="blogTooltip">
      <router-link :to="blogRoute" :class="{ 'new-item': isNewBlog }">{{
        blog.title
      }}</router-link>
    </Tooltip>{{ " "
    }}<span v-if="showCounters" class="counters"
      ><span class="bracket">(</span
      ><Tooltip :text="publicationsTooltip"
        ><router-link :to="blogRoute" :aria-label="publicationsTooltip">{{
          publicationsCount
        }}</router-link></Tooltip
      ><span class="separator">/</span
      ><Tooltip :text="commentsTooltip"
        ><router-link :to="blogRoute" :aria-label="commentsTooltip">{{
          commentsCount
        }}</router-link></Tooltip
      ><span class="bracket">)</span
    ></span>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.muted
  color: $text-muted
  user-select: none

.new-item
  color: $accent-green
  &:hover
    color: $accent-green-hover

.counters
  transition: opacity 0.15s ease
  a
    color: $link
    &:hover
      color: $link-hover

.bracket,
.separator
  color: $text-muted
</style>
