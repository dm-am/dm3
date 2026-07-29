<script setup lang="ts">
/**
 * PublicationCard — the presentational card for a blog publication.
 *
 * This is the component consumers must render for a publication; the
 * publication is its own entity with its own (future) visual design.
 * TEMPORARY implementation, by product decision (2026-07-11): until that
 * design lands, the publication must look one-to-one like a forum topic
 * card, so internally this delegates to TopicCard. When the publication
 * design arrives, replace the internals HERE — consumers (profile best
 * publication, future blog pages) keep rendering PublicationCard and
 * TopicCard stays purely a forum concern.
 *
 * Graceful degradation instead of fake data: the title is plain text (no
 * standalone publication page route exists yet and the blog page is still
 * a stub), the comments count is plain text, likes render as the static
 * tooltip indicator, and like/warn actions are not rendered.
 */
import type { Publication } from "@/entities/blog";
import { TopicCard } from "@/features/topic/@x/publication";

withDefaults(
  defineProps<{
    publication: Publication;
    /** Enable content truncation (for embedded/spotlight contexts). */
    truncatable?: boolean;
  }>(),
  {
    truncatable: false,
  },
);
</script>

<template>
  <!-- publishedUtc is the moment readers care about; createdUtc is the
       draft-creation fallback for data published before the field existed. -->
  <TopicCard
    :title="publication.title"
    :content-html="publication.content"
    :author="publication.author"
    :created-utc="publication.publishedUtc ?? publication.createdUtc"
    :modified-utc="publication.modifiedUtc"
    :comments-count="publication.commentCount"
    :likes="publication.likes"
    :truncatable="truncatable"
  />
</template>
