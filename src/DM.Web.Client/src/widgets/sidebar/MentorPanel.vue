<script setup lang="ts">
/**
 * MentorPanel — left-sidebar "Панель наставника" (product doc 4.2.1.6).
 *
 * Shown at the very top of the LEFT sidebar, site-wide, ONLY for mentors
 * (Mentor+ role) that actually curate something: the doc hides the panel
 * when both curated lists are empty, so nothing (not even a skeleton) is
 * rendered until the data confirms a non-empty list — the panel's very
 * existence is data-driven.
 *
 * Curated games: participating games (shared games store, deduped fetch)
 *   where the current user has the "Moderator" participation flag — the
 *   API serializes GameParticipation flags (Owner/Authority/Player/Reader/
 *   Moderator), NOT GameRole names, and Moderator is the game-mentor flag
 *   (see entities/game/model/detailsStore.ts). Draft/Active only; server order
 *   (activatedUtc ?? createdUtc desc — GameRef carries no createdUtc, so
 *   the server default is the closest match to the doc's "created desc").
 *   Rows mirror the sidebar GameLink idiom: "- " prefix, title with hover
 *   tooltip, always-visible "(N/A)" unread counters, red "★" wait marker
 *   with the "Вашего хода ждут: ..." tooltip (aggregated from room
 *   pendencies, same source GameRoomLink uses).
 *
 * Curated blogs: the blog DTOs carry no participation flags, so mentorship
 *   is resolved per candidate blog via GET blogs/{id}/users?role=mentor
 *   (candidates = participating Draft/Active blogs not authored/assisted by
 *   the current user). Rows reuse the sidebar BlogLink (no wait markers on
 *   blogs per the doc); sorted by createdUtc desc.
 */
import { computed, onMounted, ref, watch } from "vue";
import { storeToRefs } from "pinia";
import SidebarBlock from "./SidebarBlock.vue";
import BlogLink from "./BlogLink.vue";
import {
  GameLink,
  GameStatus,
  gameApi,
  useGameDisplay,
  useGamesStore,
  type GameRef,
} from "@/entities/game";
import { useBlogsStore, type BlogRef } from "@/entities/blog";
import { GameParticipation } from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import { UserRole } from "@/shared/api/models/common";
import type { ListEnvelope } from "@/shared/api/models/common";
import { Api } from "@/shared/api";
import { CounterPair } from "@/shared/ui/CounterPair";
import { Tooltip } from "@/shared/ui/Tooltip";

// Wait marker glyph, mirrored from GameRoomLink.
const STAR = "★";

// Safety caps for the per-item detail requests (mentor lists are short;
// the caps only guard against pathological data).
const MAX_PENDENCY_GAMES = 10;
const MAX_BLOG_CANDIDATES = 20;

const userStore = useAuthStore();
const { user } = storeToRefs(userStore);

const gamesStore = useGamesStore();
const blogsStore = useBlogsStore();

const me = computed(() => user.value?.username ?? null);

// The panel exists only for Mentor+ roles (site-wide role, GamePanel gate).
const isMentorRole = computed(() => {
  const role = user.value?.role;
  if (!role) return false;
  return [
    UserRole.Mentor,
    UserRole.Moderator,
    UserRole.SeniorModerator,
    UserRole.Admin,
  ].includes(role);
});

// --- Curated games (participation flag, Draft/Active only) ---

const isDraftOrActive = (status: GameStatus | BlogRef["status"]) =>
  status === GameStatus.Draft || status === GameStatus.Active;

const isGameMentor = (g: GameRef) =>
  g.participation?.includes(GameParticipation.Moderator) ?? false;

const mentorGames = computed<GameRef[]>(
  () =>
    gamesStore.participatingGames?.filter(
      (g) => isGameMentor(g) && isDraftOrActive(g.status),
    ) ?? [],
);

const {
  getUnreadPosts,
  getUnreadComments,
  formatUnreadPostsTooltip,
  formatUnreadCommentsTooltip,
} = useGameDisplay();

// --- Wait markers: character names awaiting the current user, per game ---

const pendingNamesByGame = ref<Record<string, string[]>>({});
let pendencyGeneration = 0;

async function loadPendencies(games: GameRef[]) {
  const generation = ++pendencyGeneration;
  const username = me.value;
  if (!username) {
    pendingNamesByGame.value = {};
    return;
  }

  const results: Record<string, string[]> = {};
  await Promise.all(
    games.slice(0, MAX_PENDENCY_GAMES).map(async (game) => {
      const { data } = await gameApi.getRooms(game.id);
      if (!data) return;
      const names = data.resources
        .flatMap((room) => room.pendencies ?? [])
        .filter((p) => !p.fulfilledUtc && p.waitingFor?.username === username)
        .map((p) => p.characterName);
      if (names.length) results[game.id] = names;
    }),
  );

  // A newer run may have started while awaiting — drop stale results.
  if (generation === pendencyGeneration) {
    pendingNamesByGame.value = results;
  }
}

function starTooltip(gameId: string): string {
  const names = pendingNamesByGame.value[gameId] ?? [];
  return `Вашего хода ждут: ${names.join(", ")}`;
}

// immediate: the store may already hold cached data on mount (60s TTL,
// shared with OwnedGames) — waiting for a change would skip that case.
watch(mentorGames, (games) => loadPendencies(games), { immediate: true });

// --- Curated blogs (per-blog mentor lookup, see the header comment) ---

interface BlogUserEntry {
  user?: { username?: string };
  role: string;
}

const mentorBlogIds = ref<Set<string>>(new Set());
let blogGeneration = 0;

const blogCandidates = computed<BlogRef[]>(() => {
  const username = me.value;
  if (!username) return [];
  return (
    blogsStore.participatingBlogs?.filter(
      (b) =>
        isDraftOrActive(b.status) &&
        b.author?.username !== username &&
        !(b.assistants ?? []).some((a) => a?.username === username),
    ) ?? []
  );
});

async function resolveMentorBlogs(candidates: BlogRef[]) {
  const generation = ++blogGeneration;
  const username = me.value;
  if (!username || !candidates.length) {
    mentorBlogIds.value = new Set();
    return;
  }

  const ids = new Set<string>();
  await Promise.all(
    candidates.slice(0, MAX_BLOG_CANDIDATES).map(async (blog) => {
      const { data } = await Api.get<ListEnvelope<BlogUserEntry>>(
        `blogs/${blog.id}/users`,
        { role: "mentor" },
      );
      // The ?role=mentor filter already narrows the list to the mentor (if
      // any); the wire role value is PascalCase ("Mentor",
      // nameof(BlogRole.Mentor) in BlogUserApiService), so compare
      // case-insensitively as defense in depth.
      const isMine = data?.resources.some(
        (entry) =>
          entry.role?.toLowerCase() === "mentor" &&
          entry.user?.username === username,
      );
      if (isMine) ids.add(blog.id);
    }),
  );

  if (generation === blogGeneration) {
    mentorBlogIds.value = ids;
  }
}

// immediate for the same cached-store reason as the games watcher above.
watch(blogCandidates, (candidates) => resolveMentorBlogs(candidates), {
  immediate: true,
});

// Doc: blogs sorted by blogCreatedUtc desc.
const mentorBlogs = computed<BlogRef[]>(() =>
  blogCandidates.value
    .filter((b) => mentorBlogIds.value.has(b.id))
    .slice()
    .sort((a, b) => (b.createdUtc ?? "").localeCompare(a.createdUtc ?? "")),
);

// --- Data loading (shared stores dedupe repeated fetches) ---

function fetchAll() {
  gamesStore.fetchParticipatingGames();
  blogsStore.fetchParticipatingBlogs();
}

onMounted(() => {
  if (isMentorRole.value) fetchAll();
});

watch(
  () => [user.value?.username, isMentorRole.value] as const,
  ([username, mentor], [oldUsername]) => {
    if (!oldUsername && username && mentor) {
      // Login-without-reload path; the stores' own reset on logout is
      // handled by OwnedGames/OwnedBlogs which mount for every user.
      fetchAll();
    }
    if (!username) {
      pendingNamesByGame.value = {};
      mentorBlogIds.value = new Set();
    }
  },
);

// The doc hides the panel unless something is actually curated.
const visible = computed(
  () =>
    isMentorRole.value &&
    (mentorGames.value.length > 0 || mentorBlogs.value.length > 0),
);
</script>

<template>
  <SidebarBlock v-if="visible" token="MentorPanel">
    <template #title>Наставничество</template>

    <!-- "Курируемые игры" section -->
    <template v-if="mentorGames.length">
      <div class="section-title">Курируемые игры</div>
      <li v-for="game in mentorGames" :key="game.id" class="link">
        <span class="muted" aria-hidden="true">- </span>
        <GameLink :game="game" highlight-new muted-closed />{{ " "
        }}<CounterPair
          class="counters"
          :first-value="getUnreadPosts(game)"
          :first-to="{
            name: 'game-first-unread-post',
            params: { id: game.id },
          }"
          :first-label="formatUnreadPostsTooltip(getUnreadPosts(game))"
          :second-value="getUnreadComments(game)"
          :second-to="{
            name: 'game-first-unread-comment',
            params: { id: game.id },
          }"
          :second-label="formatUnreadCommentsTooltip(getUnreadComments(game))"
        />
        <Tooltip
          v-if="pendingNamesByGame[game.id]?.length"
          :text="starTooltip(game.id)"
        >
          <span class="star" aria-label="Ожидается ваш ход">{{ STAR }}</span>
        </Tooltip>
      </li>
    </template>

    <!-- "Курируемые блоги" section -->
    <template v-if="mentorBlogs.length">
      <div class="section-title">Курируемые блоги</div>
      <BlogLink
        v-for="blog in mentorBlogs"
        :key="blog.id"
        :blog="blog"
        :counters="true"
        :always-show-counters="true"
      />
    </template>
  </SidebarBlock>
</template>

<style scoped lang="sass">
// Section headers mirror GamePanel's .section-title idiom.
.section-title
  margin: $small 0 $tiny
  font-size: $secondary-font-size
  font-weight: bold
  color: $text-muted
  text-transform: uppercase
  letter-spacing: 0.3px

.link
  display: block

.muted
  color: $text-muted

.star
  color: $accent-red
  margin-left: 4px
  cursor: default
</style>
