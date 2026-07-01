<script setup lang="ts">
/**
 * ProfileBestPublication — the user's most-liked published publication
 * across every blog they author. Sibling to ProfileBestPost but for
 * blogs: same role (spotlight inside the relevant profile tab), same
 * empty / loading affordances (a GamePostSkeleton while loading, matching
 * ProfileBestPost).
 *
 * Backend contract: `GET /v1/users/{username}/best-publication` returns
 * an envelope with `resource: Publication | null`. `null` is the success
 * case for "no publications yet" — the API doesn't 404 in that case, so
 * we branch on the resource being null instead of catching errors.
 *
 * Rendering: shows the title (plain text — the standalone publication page
 * route doesn't exist yet, so a router-link would 404), the blog it's
 * published in (by name when the API provides `blogTitle`; never a raw
 * GUID fragment), like count and the server-rendered preview HTML.
 */
import { onMounted, ref, watch, computed } from "vue";
import { blogApi } from "@/entities/blog";
import type { Publication } from "@/entities/blog";
import { GamePostSkeleton } from "@/shared/ui/Skeleton";
import { SecondaryText } from "@/shared/ui/Layout";
import HumanDate from "@/shared/ui/Date/HumanDate.vue";

const props = defineProps<{
  username: string;
}>();

const publication = ref<Publication | null>(null);
const loaded = ref(false);
const loading = ref(false);

async function fetchBest(username: string) {
  loading.value = true;
  loaded.value = false;
  try {
    const { data } = await blogApi.getUserBestPublication(username);
    publication.value = data?.resource ?? null;
  } finally {
    loading.value = false;
    loaded.value = true;
  }
}

onMounted(() => fetchBest(props.username));
watch(
  () => props.username,
  (next) => fetchBest(next),
);

// `blogTitle` is being added to the publication DTO (backend join). Read it
// defensively until the type lands — we only render the blog reference when
// a human-readable title is present, never a truncated GUID.
const blogTitle = computed<string | null>(() => {
  const p = publication.value as (Publication & { blogTitle?: string }) | null;
  const title = p?.blogTitle?.trim();
  return title ? title : null;
});
</script>

<template>
  <section class="profile-best-publication">
    <GamePostSkeleton v-if="loading && !loaded" />

    <SecondaryText v-else-if="!publication"> Нет публикаций </SecondaryText>

    <!-- Rendered as a post/topic card (dashed border, justified body) so a
         publication reads visually the same as posts and topics site-wide. -->
    <article v-else class="publication">
      <div class="publication-title">{{ publication.title }}</div>

      <!-- Inline flow with literal " · " separators so a copied selection reads
           "в блоге «X» · 16.06.2026 · ♥ 3" with clean spacing. -->
      <div class="publication-meta">
        <template v-if="blogTitle"
          ><span class="meta-blog">в блоге «{{ blogTitle }}»</span
          >{{ " · " }}</template
        ><HumanDate
          :date="publication.publishedUtc ?? publication.createdUtc"
          format="DD.MM.YYYY"
        />{{ " · "
        }}<span class="meta-likes" :class="{ muted: !publication.likes.length }"
          >♥ {{ publication.likes.length }}</span
        >
      </div>

      <div v-if="publication.preview" class="publication-content">
        {{ publication.preview }}
      </div>
      <div v-else class="publication-content" v-html="publication.content" />
    </article>
  </section>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.profile-best-publication
  display: flex
  flex-direction: column
  gap: $small

// Card chrome matching game posts / forum topics: dashed border + element bg.
.publication
  padding: $small
  background-color: $bg-element
  border: 1px dashed $border

.publication-title
  font-weight: 700
  font-size: 1.1em
  color: $text

.publication-meta
  margin-top: $tiny
  font-size: $secondary-font-size
  color: $text-muted

.meta-likes.muted
  opacity: 0.6

.publication-content
  margin-top: $small
  color: $text
  line-height: 1.5
  text-align: justify
  hyphens: auto
  -webkit-hyphens: auto
</style>
