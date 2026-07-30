<script setup lang="ts">
// Left-sidebar "Действия" panel for a game page. Mounted by LeftSidebar
// on any /game/:id route (route.meta.gameZone). It is the single home for
// per-game navigation and management, driven entirely by the shared
// useGameDetailsStore (the game page loads game + rooms; this panel adds
// the chat-rooms slice and surfaces the mutation actions).
//
// Section order follows the product doc:
//   {title} / "Активные комнаты" / "Архивные комнаты" / "Обсуждение" (N) /
//   "Разделы" / "Управление игрой" / "Действия с игрой" / "Модерация игры"
//
// Role gates (never a single blanket "moderator" gate):
//   "Управление игрой" — the header shows for Master/Assistant/Mentor, but
//     the edit items ("Настройки", "Создать NPC", status buttons) are
//     Master/Assistant only; "Блокнот мастера" is Master/Assistant/Mentor.
//   "Действия с игрой" — any authenticated user except the master
//     (subscribe / apply-to-join, via features/game-actions).
//   "Модерация игры" — premoderation is Mentor+ (global),
//     delete-others-game is SeniorModerator+, reset-recruitment is
//     Admin-only.
//
// Four data-states per UI_STANDARDS: skeleton (initial load), error,
// empty (game not found), content.
import { computed, ref, watch } from "vue";
import { storeToRefs } from "pinia";
import { useRouter } from "vue-router";
import {
  useGameDetailsStore,
  GamePremoderationTransition,
} from "@/entities/game";
import { GameStatusButtons, GameJoinActions } from "@/features/game-actions";
import { useAuthStore } from "@/entities/user";
import { UserRole } from "@/shared/api/models/common";
import { useExpandableSection } from "@/shared/lib/composables";
import SidebarBlock from "./SidebarBlock.vue";
import SidebarSkeleton from "./SidebarSkeleton.vue";
import GameRoomLink from "./GameRoomLink.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { notifyFailure } from "@/shared/lib/errors";

const props = defineProps<{ gameId: string }>();

const store = useGameDetailsStore();
const {
  game,
  gameLoading,
  gameError,
  rooms,
  roomsLoading,
  roomsError,
  activeRooms,
  archivedRooms,
  chatRooms,
  characters,
  isMaster,
  isAssistant,
  canManage,
} = storeToRefs(store);

// Master/assistant may edit the game (settings, NPCs, status). The game
// mentor ("Наставник" — helper to a novice master) gets the management header
// and the notepad for oversight, but not the edit items.
const canEdit = computed(() => isMaster.value || isAssistant.value);

// The game notepad is shared master/assistant/mentor scratch space (GLOSSARY:
// MasterNotepad) — hidden from players and readers.
const canUseNotepad = computed(() => canManage.value);

const { user } = storeToRefs(useAuthStore());
const router = useRouter();

// Routes use the game's public id (URL_STRUCTURE). Fall back to the raw
// route param before the game has resolved.
const publicId = computed(() => game.value?.publicId ?? props.gameId);

// Archived rooms are hidden by default, revealed by a spoiler toggle — a
// content expandable section (unified animation + the page-wide
// "Развернуть/Свернуть все" toggle, active only while archived rooms exist).
const showArchived = ref(false);
const archivedZoneRef = ref<HTMLElement | null>(null);
const { toggle: toggleArchived, zoneBindings: archivedZoneBindings } =
  useExpandableSection({
    el: archivedZoneRef,
    model: showArchived,
    registryEnabled: () => archivedRooms.value.length > 0,
    label: "GamePanelArchivedRooms",
  });

// Chat rooms are keyed by the game's GUID (the ChatRoom endpoint binds a
// Guid, unlike the publicId-tolerant room/notepad endpoints), so wait for
// the game to resolve before loading them. Characters are loaded too (only
// when not already in the store) to drive the "Редактировать NPC" /
// "Редактировать персонажа" panel items.
watch(
  () => game.value?.id,
  (id) => {
    if (!id) return;
    store.loadChatRooms(id);
    if (!characters.value.length) store.loadCharacters(id);
  },
  { immediate: true },
);

// NPCs that exist in the game (excludes rejected applications). "Редактировать
// NPC" targets the first one (doc 4.2.1.3: "предвыбран первый персонаж").
const npcs = computed(() =>
  characters.value.filter((c) => c.isNpc && c.status !== "Declined"),
);
const firstNpcId = computed(() => npcs.value[0]?.id ?? null);

// --- Actions with the game ---
// Any authenticated user except the master may act on the game (subscribe,
// apply to join). The master manages via the sections above instead.
const canActOnGame = computed(() => !!user.value && !isMaster.value);

// --- Moderation (global roles) ---

const globalRoles = computed<UserRole[]>(() => {
  const u = user.value;
  if (!u) return [];
  return u.role ? [u.role] : [];
});
const isGlobalMentor = computed(() =>
  globalRoles.value.some((r) =>
    [
      UserRole.Mentor,
      UserRole.Moderator,
      UserRole.SeniorModerator,
      UserRole.Admin,
    ].includes(r),
  ),
);
const isGlobalSeniorModerator = computed(() =>
  globalRoles.value.some((r) =>
    [UserRole.SeniorModerator, UserRole.Admin].includes(r),
  ),
);
const isGlobalAdmin = computed(() =>
  globalRoles.value.includes(UserRole.Admin),
);
const showModeration = computed(
  () =>
    isGlobalMentor.value ||
    isGlobalSeniorModerator.value ||
    isGlobalAdmin.value,
);

// Confirmed moderation actions share a single dialog.
interface ModAction {
  title: string;
  message: string;
  confirmLabel: string;
  danger?: boolean;
  run: () => Promise<void>;
}
const pendingMod = ref<ModAction | null>(null);

function askPremod(t: GamePremoderationTransition) {
  const send = t === GamePremoderationTransition.SendToPremoderation;
  pendingMod.value = {
    title: send ? "Премодерация" : "Выпуск из премодерации",
    message: send
      ? "Отправить игру на премодерацию?"
      : "Выпустить игру из премодерации?",
    confirmLabel: send ? "Отправить" : "Выпустить",
    run: async () => {
      const error = await store.changePremoderation(t);
      if (error) notifyFailure(error, "Не удалось изменить премодерацию");
    },
  };
}

function askDelete() {
  pendingMod.value = {
    title: "Удаление игры",
    message: "Удалить эту игру? Действие необратимо.",
    confirmLabel: "Удалить игру",
    danger: true,
    run: async () => {
      const error = await store.deleteGame();
      if (error) notifyFailure(error, "Не удалось удалить игру");
      else router.push({ name: "games" });
    },
  };
}

function askResetRecruitment() {
  pendingMod.value = {
    title: "Сброс даты набора",
    message: "Сбросить дату начала набора?",
    confirmLabel: "Сбросить",
    run: async () => {
      const error = await store.resetRecruitment();
      if (error) notifyFailure(error, "Не удалось сбросить дату набора");
    },
  };
}

async function confirmMod() {
  const action = pendingMod.value;
  pendingMod.value = null;
  if (action) await action.run();
}
</script>

<template>
  <SidebarBlock token="GamePanel">
    <template #title>
      <template v-if="game">{{ game.title }}</template>
      <template v-else>Игра</template>
    </template>

    <!-- 1. loading -->
    <SidebarSkeleton v-if="gameLoading && !game" :lines="8" />

    <!-- 2. error -->
    <SecondaryText v-else-if="gameError" class="error">
      {{ gameError }}
    </SecondaryText>

    <!-- 3. content -->
    <template v-else-if="game">
      <!-- "Активные комнаты" — active post-rooms and chat rooms in one list. -->
      <div class="section-title">Активные комнаты</div>
      <SidebarSkeleton v-if="roomsLoading && !rooms.length" :lines="3" />
      <SecondaryText v-else-if="roomsError" class="error">
        {{ roomsError }}
      </SecondaryText>
      <template v-else-if="activeRooms.length || chatRooms.length">
        <GameRoomLink
          v-for="room in activeRooms"
          :key="room.id"
          :room="room"
          :game-public-id="publicId"
        />
        <li v-for="chat in chatRooms" :key="chat.id" class="link">
          <span class="muted" aria-hidden="true">- </span>
          <span class="chat-title">{{ chat.title }}</span>
          <span v-if="chat.unreadCount > 0" class="unread"
            >&nbsp;({{ chat.unreadCount }})</span
          >
        </li>
      </template>
      <SecondaryText v-else>Комнат пока нет</SecondaryText>

      <!-- "Архивные комнаты" — a bold heading (not a list row) with an inline
           "(показать)/(скрыть)" spoiler link; rooms appear below when expanded. -->
      <template v-if="archivedRooms.length">
        <div class="section-title">
          Архивные комнаты<button
            type="button"
            class="archived-toggle"
            :aria-expanded="showArchived"
            @click="toggleArchived()"
          >
            ({{ showArchived ? "скрыть" : "показать" }})
          </button>
        </div>
        <div
          ref="archivedZoneRef"
          class="expand-zone"
          v-bind="archivedZoneBindings"
        >
          <ul v-if="showArchived" class="archived-list">
            <GameRoomLink
              v-for="room in archivedRooms"
              :key="room.id"
              :room="room"
              :game-public-id="publicId"
            />
          </ul>
        </div>
      </template>

      <!-- Game navigation — a flat list of links (like the old site's game
           actions strip), no "Разделы"/"Обсуждение" sub-headings. -->
      <li class="link">
        <span class="muted" aria-hidden="true">- </span>
        <router-link :to="{ name: 'game-comments', params: { id: publicId } }">
          Обсуждение<span v-if="game.unreadCommentsCount">
            ({{ game.unreadCommentsCount }})</span
          >
        </router-link>
      </li>
      <li class="link">
        <span class="muted" aria-hidden="true">- </span>
        <router-link :to="{ name: 'game-characters', params: { id: publicId } }"
          >Персонажи</router-link
        >
      </li>
      <li class="link">
        <span class="muted" aria-hidden="true">- </span>
        <router-link
          :to="{ name: 'game-post-reviews', params: { id: publicId } }"
          >Оцененные посты</router-link
        >
      </li>

      <!-- "Управление игрой" section (Master / Assistant edit items) -->
      <template v-if="canEdit || canUseNotepad">
        <div class="section-title">Управление игрой</div>
        <template v-if="canEdit">
          <li class="link">
            <span class="muted" aria-hidden="true">- </span>
            <router-link
              :to="{ name: 'game-settings', params: { id: publicId } }"
            >
              Настройки
            </router-link>
          </li>
          <li class="link">
            <span class="muted" aria-hidden="true">- </span>
            <router-link
              :to="{
                name: 'game-character-create',
                params: { id: publicId },
                query: { npc: '1' },
              }"
            >
              Создать NPC
            </router-link>
          </li>
          <!-- "Редактировать NPC" — shown only when at least one NPC exists;
               links to the first NPC's edit page (doc 4.2.1.3). -->
          <li v-if="firstNpcId" class="link">
            <span class="muted" aria-hidden="true">- </span>
            <router-link
              :to="{
                name: 'game-character-edit',
                params: { id: publicId, characterId: firstNpcId },
              }"
            >
              Редактировать NPC
            </router-link>
          </li>
          <!-- Status transition buttons (Master/Assistant) -->
          <GameStatusButtons variant="strip" />
        </template>
        <li v-if="canUseNotepad" class="link">
          <span class="muted" aria-hidden="true">- </span>
          <router-link :to="{ name: 'game-notepad', params: { id: publicId } }">
            Блокнот мастера
          </router-link>
        </li>
      </template>

      <!-- "Действия с игрой" section (any authed except master) -->
      <template v-if="canActOnGame">
        <div class="section-title">Действия с игрой</div>
        <GameJoinActions variant="strip" />
      </template>

      <!-- "Модерация игры" section (global roles) -->
      <template v-if="showModeration">
        <div class="section-title">Модерация игры</div>
        <template v-if="isGlobalMentor">
          <li class="link">
            <span class="muted" aria-hidden="true">- </span>
            <button
              type="button"
              class="strip-action"
              @click="
                askPremod(GamePremoderationTransition.SendToPremoderation)
              "
            >
              Отправить на премодерацию
            </button>
          </li>
          <li class="link">
            <span class="muted" aria-hidden="true">- </span>
            <button
              type="button"
              class="strip-action"
              @click="
                askPremod(GamePremoderationTransition.RemoveFromPremoderation)
              "
            >
              Выпустить из премодерации
            </button>
          </li>
        </template>
        <li v-if="isGlobalSeniorModerator" class="link">
          <span class="muted" aria-hidden="true">- </span>
          <button type="button" class="strip-action danger" @click="askDelete">
            Удалить игру
          </button>
        </li>
        <li v-if="isGlobalAdmin" class="link">
          <span class="muted" aria-hidden="true">- </span>
          <button
            type="button"
            class="strip-action"
            @click="askResetRecruitment"
          >
            Сбросить дату набора
          </button>
        </li>
      </template>
    </template>

    <!-- 4. empty / not found -->
    <SecondaryText v-else>Игра не найдена</SecondaryText>

    <ConfirmDialog
      :show="!!pendingMod"
      :title="pendingMod?.title ?? ''"
      :message="pendingMod?.message ?? ''"
      :confirm-label="pendingMod?.confirmLabel"
      :danger="pendingMod?.danger"
      @confirm="confirmMod"
      @cancel="pendingMod = null"
    />
  </SidebarBlock>
</template>

<style scoped lang="sass">
// Section headings read as content titles (bold, normal body colour + size,
// no uppercase, no leading dash) — the same look as a poll's title.
.section-title
  margin: $small 0 $tiny
  font-size: $font-size
  font-weight: bold
  color: $text

// "(показать)/(скрыть)" spoiler toggle for archived rooms — an inline link
// beside the bold heading (normal weight, so only the heading reads bold).
// Nested list inside the animated archived-rooms zone: the li rows carry
// their own .link class styles, the wrapper only resets list chrome.
.archived-list
  list-style: none
  margin: 0
  padding: 0

.archived-toggle
  margin-left: $tiny
  padding: 0
  border: none
  background: none
  font: inherit
  font-weight: normal
  color: $link
  cursor: pointer

  &:hover
    color: $link-hover
    text-decoration: underline

.unread
  color: $text-muted

.link
  display: block

.muted
  color: $text-muted

// Only the decorative "- " prefix (aria-hidden) is excluded from selection;
// informative muted text (unread counters) must stay selectable.
.muted[aria-hidden="true"]
  user-select: none

.chat-title
  color: $text

.error
  color: $accent-red

// Strip actions mimic a plain sidebar link but trigger a store mutation.
.strip-action
  padding: 0
  border: none
  background: none
  font: inherit
  color: $link
  cursor: pointer

  &:hover
    color: $link-hover
    text-decoration: underline

  &.danger
    color: $accent-red
</style>
