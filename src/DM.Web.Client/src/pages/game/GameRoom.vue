<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useGameDetailsStore } from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import { useZoneSection } from "@/shared/lib/composables/useZoneSection";
import { useScrollToElement } from "@/shared/lib/composables/useScrollToElement";
import { gameApi, type DiceRollInput } from "@/entities/game";
import PagingWithSeparators from "@/shared/ui/Paging/PagingWithSeparators.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import { ErrorState } from "@/shared/ui/ErrorState";
import { SvgIcon } from "@/shared/ui/Icon";
import { symbols } from "@/shared/lib/utils/icons";
import { Select, type SelectOption } from "@/shared/ui/Select";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { composerDraftKey } from "@/shared/lib/utils/draftKey";
import { GamePost } from "@/widgets/game-post";
import { GamePostSkeleton } from "@/shared/ui/Skeleton";
import { notifyFailure } from "@/shared/lib/errors";

const route = useRoute();
const gameStore = useGameDetailsStore();
const userStore = useAuthStore();
const {
  game,
  rooms,
  currentRoom,
  posts,
  postsPaging,
  postsLoading,
  postsError,
  characters,
  isMaster,
  isAssistant,
} = storeToRefs(gameStore);
const { user } = storeToRefs(userStore);

const roomNum = computed(() => parseInt(route.params.num as string));
// The shared Paging widget writes the page as ?number= (codebase-wide
// query-key convention) — read the same key back.
function getPage(): number {
  const page = route.query.number;
  return page ? parseInt(page as string) || 1 : 1;
}

// Resolve the room off the store's rooms list by number so it stays fresh
// after loadRooms() re-syncs (pendency mutations replace the array, which
// would otherwise orphan the currentRoom reference set inside loadPosts).
const room = computed(
  () =>
    rooms.value.find((r) => r.roomNumber === roomNum.value) ??
    currentRoom.value,
);

// The room IS the section here, and its name is data — meta.section cannot
// spell it. Announced to the shell, which is the only place that writes a
// heading or a tab name in this zone.
useZoneSection(() => room.value?.title);

// Scroll to target element when posts are loaded
const postsLoaded = computed(
  () => posts.value.length > 0 && !postsLoading.value,
);
useScrollToElement(postsLoaded);

// Mark room as read when posts are loaded (for authenticated users)
watch(
  postsLoaded,
  async (loaded) => {
    if (loaded && userStore.user && currentRoom.value?.id) {
      try {
        await gameApi.markRoomAsRead(currentRoom.value.id as string);
      } catch {
        // Silently ignore - non-critical operation
      }
    }
  },
  { once: true },
);

const gameId = computed(() => route.params.id as string);

/** Position of a post in the whole room, not on the page it is shown on. */
function postNumber(index: number): number {
  const paging = postsPaging.value;
  if (!paging) return index + 1;
  return paging.skip + index + 1;
}

// Paging scrolls the posts list back into view (not the page top)
const postsListRef = ref<HTMLElement | null>(null);
function pagingAnchor(): HTMLElement | null {
  return postsListRef.value;
}

// One target for both paging blocks, above and below the posts.
const pagingTarget = computed(() => ({
  name: "game-room",
  params: { id: game.value?.publicId || game.value?.id, num: roomNum.value },
}));

/** Re-read the page of posts that is on screen (the error banner's retry). */
function reloadPosts() {
  return gameStore.loadPostsByRoomNumber(
    gameId.value,
    roomNum.value,
    getPage(),
  );
}

useFetchData(
  () => gameStore.loadPostsByRoomNumber(gameId.value, roomNum.value, getPage()),
  [
    {
      param: (p) => p.num,
      callback: () =>
        gameStore.loadPostsByRoomNumber(gameId.value, roomNum.value, 1),
    },
  ],
  [
    {
      query: (q) => q.number,
      callback: () =>
        gameStore.loadPostsByRoomNumber(gameId.value, roomNum.value, getPage()),
    },
  ],
);

// Characters back the composer's "post as" selector and the master turn
// controls. Loaded once per game id (independent of the posts page).
watch(
  gameId,
  (id) => {
    if (id) gameStore.loadCharacters(id);
  },
  { immediate: true },
);

// ───────────────────────────────────────────────────────────────────────────
// Post composer
// ───────────────────────────────────────────────────────────────────────────
const MASTER_VALUE = "__master__";
const canManageTurns = computed(() => isMaster.value || isAssistant.value);

const myActiveCharacters = computed(() =>
  characters.value.filter(
    (c) => c.status === "Active" && c.author?.username === user.value?.username,
  ),
);
// Every active character in the game — the pool a master marks as awaited.
const activeCharacters = computed(() =>
  characters.value.filter((c) => c.status === "Active"),
);
const activeNpcs = computed(() =>
  characters.value.filter((c) => c.status === "Active" && c.isNpc),
);

// Composer is offered to players with an active character and to the master /
// assistants (who may post as an NPC or as themselves).
const showComposer = computed(
  () =>
    !!user.value &&
    (myActiveCharacters.value.length > 0 || canManageTurns.value),
);

// "Post as" options: own active characters first, then (for hosts) NPCs and a
// plain master/assistant post with no character.
const postAsOptions = computed<SelectOption[]>(() => {
  const options: SelectOption[] = myActiveCharacters.value.map((c) => ({
    value: c.id as string,
    label: c.name,
  }));
  if (canManageTurns.value) {
    for (const npc of activeNpcs.value) {
      options.push({ value: npc.id as string, label: `${npc.name} (NPC)` });
    }
    options.push({ value: MASTER_VALUE, label: "От лица мастера" });
  }
  return options;
});

const selectedPostAs = ref("");
// Keep the selection valid as the options resolve / change.
watch(
  postAsOptions,
  (options) => {
    if (!options.some((o) => o.value === selectedPostAs.value)) {
      selectedPostAs.value = options[0]?.value ?? "";
    }
  },
  { immediate: true },
);

const gameText = ref("");
const metagameText = ref("");
const submitting = ref(false);
const composerError = ref<string | null>(null);
const gameEditorRef = ref<InstanceType<typeof BBCodeEditor> | null>(null);
const metaEditorRef = ref<InstanceType<typeof BBCodeEditor> | null>(null);

// Dice: the room decides whether rolls are allowed at all.
const diceEnabled = computed(() => room.value?.settings?.diceEnabled ?? false);
const diceRolls = ref<DiceRollInput[]>([]);
const diceSides = ref(20);
const diceCount = ref(1);
const diceBonus = ref(0);
const diceExplosion = ref(0);
const dicePublic = ref(true);
const diceComment = ref("");

function addDiceRoll() {
  const sides = Number(diceSides.value);
  if (!Number.isFinite(sides) || sides < 2) return;
  const count = Math.max(1, Math.trunc(Number(diceCount.value) || 1));
  const explosion = Math.max(0, Math.trunc(Number(diceExplosion.value) || 0));
  diceRolls.value.push({
    dice: Math.trunc(sides),
    count,
    bonus: Number(diceBonus.value) || undefined,
    explosion: explosion || undefined,
    public: dicePublic.value,
    comment: diceComment.value.trim() || undefined,
  });
  diceComment.value = "";
}

function removeDiceRoll(index: number) {
  diceRolls.value.splice(index, 1);
}

function formatDicePreview(roll: DiceRollInput): string {
  const count = roll.count && roll.count > 1 ? roll.count : "";
  const bonus = roll.bonus ? ` ${roll.bonus > 0 ? "+" : ""}${roll.bonus}` : "";
  const explode = roll.explosion ? ` (взрыв ${roll.explosion})` : "";
  const hidden = roll.public === false ? " (скрытый)" : "";
  return `${count}d${roll.dice}${bonus}${explode}${hidden}`;
}

// After a soft-delete lands, refresh the room + posts so paging / unread
// counters stay accurate (the deleted post drops out of the list).
async function handlePostDeleted() {
  await gameStore.loadRooms(gameId.value);
  await gameStore.loadPostsByRoomNumber(gameId.value, roomNum.value, getPage());
}

async function submitPost() {
  const text = gameText.value.trim();
  if (!text || submitting.value || !room.value?.id) return;
  submitting.value = true;
  composerError.value = null;

  const { error } = await gameApi.createPost(room.value.id as string, {
    characterId:
      selectedPostAs.value && selectedPostAs.value !== MASTER_VALUE
        ? selectedPostAs.value
        : undefined,
    gameText: text,
    metagameText: metagameText.value.trim() || undefined,
    diceRolls:
      diceEnabled.value && diceRolls.value.length ? diceRolls.value : undefined,
  });

  submitting.value = false;
  if (error) {
    composerError.value = "Не удалось отправить пост";
    return;
  }

  // Reset the composer and refresh the room + posts (rooms refresh clears the
  // fulfilled pendency / unread counters).
  gameText.value = "";
  metagameText.value = "";
  diceRolls.value = [];
  gameEditorRef.value?.clear();
  metaEditorRef.value?.clear();
  await gameStore.loadRooms(gameId.value);
  await gameStore.loadPostsByRoomNumber(gameId.value, roomNum.value, getPage());
}

// ───────────────────────────────────────────────────────────────────────────
// Master / assistant turn tracking (pendencies)
// ───────────────────────────────────────────────────────────────────────────
const pendencies = computed(() => room.value?.pendencies ?? []);
const newPendencyCharacterId = ref("");
const pendencyBusy = ref(false);

const pendencyOptions = computed<SelectOption[]>(() =>
  activeCharacters.value.map((c) => ({
    value: c.id as string,
    label: c.name,
  })),
);

watch(
  pendencyOptions,
  (options) => {
    if (!options.some((o) => o.value === newPendencyCharacterId.value)) {
      newPendencyCharacterId.value = options[0]?.value ?? "";
    }
  },
  { immediate: true },
);

async function addPendency() {
  if (!newPendencyCharacterId.value || pendencyBusy.value || !room.value?.id)
    return;
  pendencyBusy.value = true;
  try {
    const { error } = await gameApi.createPendency(room.value.id as string, {
      characterId: newPendencyCharacterId.value,
    });
    if (error) {
      notifyFailure(error, "Не удалось добавить ожидание хода");
      return;
    }
    await gameStore.loadRooms(gameId.value);
  } finally {
    pendencyBusy.value = false;
  }
}

async function dismissPendency(pendencyId: string) {
  if (pendencyBusy.value) return;
  pendencyBusy.value = true;
  try {
    const { error } = await gameApi.deletePendency(pendencyId);
    if (error) {
      notifyFailure(error, "Не удалось снять ожидание хода");
      return;
    }
    await gameStore.loadRooms(gameId.value);
  } finally {
    pendencyBusy.value = false;
  }
}
</script>

<template>
  <div class="game-room">
    <!-- Back link -->
    <router-link
      :to="{ name: 'game', params: { id: gameId } }"
      class="back-link"
    >
      <SvgIcon name="chevronLeft" />
      Назад к игре
    </router-link>

    <!-- Room header. An archived room says so beside its title, in the same
         marker the chat room uses — never as a suffix inside the room name. -->
    <block-title v-if="room"
      >{{ room.title
      }}<template v-if="room.isArchived"
        >{{ " " }}<span class="archived-tag">архив</span></template
      ></block-title
    >

    <!-- Error — an independent banner, the way the pulse feed draws one: a
         failed refetch keeps the posts that are already on screen instead of
         replacing them with a red line. -->
    <ErrorState
      v-if="postsError"
      class="error-banner"
      :message="postsError"
      :retry="reloadPosts"
    />

    <!-- Loading -->
    <GamePostSkeleton
      v-if="postsLoading && posts.length === 0"
      :count="3"
      :show-navigation="false"
    />

    <!-- Empty -->
    <div v-else-if="!postsError && posts.length === 0" class="posts-empty">
      <secondary-text>В этой комнате пока нет постов</secondary-text>
    </div>

    <!-- Posts list, between the two paging blocks -->
    <template v-else>
      <PagingWithSeparators
        v-if="postsPaging"
        :paging="postsPaging"
        :to="pagingTarget"
        :use-query="true"
        query-key="number"
        :scroll-anchor="pagingAnchor"
      />

      <div ref="postsListRef" class="posts-list">
        <game-post
          v-for="(post, index) in posts"
          :key="post.id"
          :post="post"
          :data-id="post.id"
          :number="postNumber(index)"
          editable
          @deleted="handlePostDeleted"
        />
      </div>

      <PagingWithSeparators
        v-if="postsPaging"
        :paging="postsPaging"
        :to="pagingTarget"
        :use-query="true"
        query-key="number"
        :scroll-anchor="pagingAnchor"
      />
    </template>

    <!-- Master / assistant turn tracking -->
    <section v-if="canManageTurns" class="turns">
      <div class="turns-title">Ожидание хода</div>
      <ul v-if="pendencies.length" class="pending-list">
        <li v-for="p in pendencies" :key="p.id" class="pending-item">
          <span class="pending-name">{{ p.characterName }}</span>
          <button
            type="button"
            class="pending-dismiss"
            :disabled="pendencyBusy"
            aria-label="Снять ожидание хода"
            @click="dismissPendency(p.id)"
          >
            {{ symbols.close }}
          </button>
        </li>
      </ul>
      <secondary-text v-else class="turns-empty">
        Сейчас ничьего хода не ждут
      </secondary-text>
      <div v-if="pendencyOptions.length" class="pending-add">
        <Select
          v-model="newPendencyCharacterId"
          :options="pendencyOptions"
          class="pending-select"
        />
        <button
          type="button"
          class="pending-add-btn"
          :disabled="pendencyBusy || !newPendencyCharacterId"
          @click="addPendency"
        >
          Ждать хода
        </button>
      </div>
    </section>

    <!-- Post composer -->
    <section v-if="showComposer" class="composer">
      <div class="composer-title">Новый пост</div>

      <div v-if="postAsOptions.length > 1" class="composer-as">
        <label class="composer-label">От лица:</label>
        <Select
          v-model="selectedPostAs"
          :options="postAsOptions"
          class="composer-as-select"
        />
      </div>

      <label class="composer-label">Игровой текст</label>
      <BBCodeEditor
        ref="gameEditorRef"
        v-model="gameText"
        context="post"
        placeholder="Игровой текст поста..."
        :draft-key="composerDraftKey('room', 'post', room?.id)"
        :disabled="submitting"
        :min-height="120"
        :is-moderator="canManageTurns"
      />

      <label class="composer-label">Метаигровой текст</label>
      <BBCodeEditor
        ref="metaEditorRef"
        v-model="metagameText"
        context="post"
        placeholder="Метаигровой комментарий (необязательно)..."
        :draft-key="composerDraftKey('room', 'metagame', room?.id)"
        :disabled="submitting"
        :min-height="60"
        :max-height="200"
        :is-moderator="canManageTurns"
      />

      <!-- Dice (only when the room allows them) -->
      <div v-if="diceEnabled" class="dice">
        <div class="dice-title">Броски кубиков</div>
        <ul v-if="diceRolls.length" class="dice-list">
          <li v-for="(roll, i) in diceRolls" :key="i" class="dice-chip">
            <span class="dice-chip-label">{{ formatDicePreview(roll) }}</span>
            <span v-if="roll.comment" class="dice-chip-comment">{{
              roll.comment
            }}</span>
            <button
              type="button"
              class="dice-chip-remove"
              aria-label="Убрать бросок"
              @click="removeDiceRoll(i)"
            >
              {{ symbols.close }}
            </button>
          </li>
        </ul>
        <div class="dice-add">
          <label class="dice-field">
            <input
              v-model.number="diceCount"
              type="number"
              min="1"
              class="dice-input dice-input-count"
              aria-label="Количество кубиков"
            />
            <span class="dice-field-label">d</span>
            <input
              v-model.number="diceSides"
              type="number"
              min="2"
              class="dice-input dice-input-sides"
              aria-label="Число граней кубика"
            />
          </label>
          <label class="dice-field">
            <span class="dice-field-label">мод.</span>
            <input
              v-model.number="diceBonus"
              type="number"
              class="dice-input dice-input-bonus"
              aria-label="Модификатор"
            />
          </label>
          <label class="dice-field">
            <span class="dice-field-label">взрыв</span>
            <input
              v-model.number="diceExplosion"
              type="number"
              min="0"
              class="dice-input dice-input-bonus"
              aria-label="Взрывающиеся кубики (число перебросов)"
            />
          </label>
          <label class="dice-check">
            <input v-model="dicePublic" type="checkbox" />
            <span>Показывать всем результаты</span>
          </label>
          <input
            v-model="diceComment"
            type="text"
            class="dice-input dice-input-comment"
            placeholder="Комментарий (необязательно)"
            aria-label="Комментарий к броску"
          />
          <button type="button" class="dice-add-btn" @click="addDiceRoll">
            Добавить бросок
          </button>
        </div>
      </div>

      <div v-if="composerError" class="composer-error">
        {{ composerError }}
      </div>

      <button
        type="button"
        class="composer-submit"
        :disabled="submitting || !gameText.trim()"
        @click="submitPost"
      >
        Отправить пост
      </button>
    </section>
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"

.game-room
  min-height: $grid-step * 50

.back-link
  display: inline-flex
  align-items: center
  gap: $tiny
  margin-bottom: $medium
  color: $link
  text-decoration: none

  &:hover
    text-decoration: underline

.posts-empty
  padding: $big

// Muted "архив" tag beside an archived room's title — the same marker the chat
// room draws, archive is conveyed by it and by the muted colour, never by a
// suffix inside the room name. The gap in front of it is the " " text node in
// the title, not a margin: a margin is drawn but not copied, and the title
// would reach the clipboard glued.
.archived-tag
  font-size: $secondary-font-size
  font-weight: normal
  color: $text-muted
  text-transform: uppercase

.error-banner
  margin-bottom: $medium

.posts-list
  display: flex
  flex-direction: column
  gap: $small

// ───────────────────────────────────────────────────────────────────────────
// Turn tracking
// ───────────────────────────────────────────────────────────────────────────
.turns
  margin-top: $big
  padding: $medium
  border: 1px dashed $border

.turns-title
  font-weight: bold
  color: $text-muted
  text-transform: uppercase
  font-size: $secondary-font-size
  letter-spacing: 0.3px
  margin-bottom: $small

.turns-empty
  display: block
  margin-bottom: $small

.pending-list
  list-style: none
  display: flex
  flex-wrap: wrap
  gap: $small
  margin: 0 0 $small

.pending-item
  display: inline-flex
  align-items: center
  gap: $tiny
  padding: $tiny $small
  background-color: $bg-element
  border: 1px solid $border
  border-radius: $border-radius

.pending-name
  color: $text

.pending-dismiss
  display: inline-flex
  align-items: center
  justify-content: center
  padding: 0 $tiny
  border: none
  background: transparent
  color: $text-muted
  font-size: $font-size
  line-height: 1
  cursor: pointer
  &:hover:not(:disabled)
    color: $accent-red
  &:disabled
    opacity: $disabled-opacity
    cursor: default

.pending-add
  display: flex
  align-items: center
  gap: $small

.pending-select
  max-width: $grid-step * 30

.pending-add-btn
  flex-shrink: 0
  +button

// ───────────────────────────────────────────────────────────────────────────
// Composer
// ───────────────────────────────────────────────────────────────────────────
.composer
  margin-top: $big
  padding: $medium
  border: 1px dashed $border

.composer-title
  font-weight: bold
  color: $text-muted
  text-transform: uppercase
  font-size: $secondary-font-size
  letter-spacing: 0.3px
  margin-bottom: $small

.composer-as
  display: flex
  align-items: center
  gap: $small
  margin-bottom: $small

.composer-as-select
  max-width: $grid-step * 30

.composer-label
  display: block
  margin: $small 0 $tiny
  color: $text-muted
  font-size: $secondary-font-size

.composer-error
  margin-top: $small
  color: $accent-red

.composer-submit
  margin-top: $medium
  +button

// ───────────────────────────────────────────────────────────────────────────
// Dice
// ───────────────────────────────────────────────────────────────────────────
.dice
  margin-top: $medium

.dice-title
  color: $text-muted
  font-size: $secondary-font-size
  margin-bottom: $small

.dice-list
  list-style: none
  display: flex
  flex-wrap: wrap
  gap: $small
  margin: 0 0 $small

.dice-chip
  display: inline-flex
  align-items: center
  gap: $tiny
  padding: $tiny $small
  background-color: $bg-element
  border: 1px solid $border
  border-radius: $border-radius

.dice-chip-label
  font-weight: bold
  color: $heading

.dice-chip-comment
  color: $text-muted
  font-style: italic

.dice-chip-remove
  display: inline-flex
  align-items: center
  justify-content: center
  padding: 0 $tiny
  border: none
  background: transparent
  color: $text-muted
  font-size: $font-size
  line-height: 1
  cursor: pointer
  &:hover
    color: $accent-red

.dice-add
  display: flex
  align-items: center
  flex-wrap: wrap
  gap: $small

.dice-field
  display: inline-flex
  align-items: center
  gap: $tiny

.dice-field-label
  color: $text-muted
  font-size: $secondary-font-size

.dice-input
  +input()

.dice-input-count,
.dice-input-sides,
.dice-input-bonus
  width: $grid-step * 10

.dice-check
  display: inline-flex
  align-items: center
  gap: $tiny
  color: $text-muted
  font-size: $secondary-font-size
  cursor: pointer

.dice-input-comment
  flex: 1
  min-width: $grid-step * 24

.dice-add-btn
  flex-shrink: 0
  +button
</style>
