<script setup lang="ts">
// Left-sidebar "Меню игры" panel for a game page. Mounted by LeftSidebar
// on any /game/:id route (route.meta.gameZone). It is the single home for
// per-game navigation and management, driven entirely by the shared
// useGameDetailsStore (the game page loads game + rooms; this panel adds
// the characters slice and surfaces the mutation actions).
//
// Menu order:
//   "Активные комнаты" / "Архивные комнаты" / "Информация" / "Обсуждение" /
//   "Персонажи" / "Рецензии" / "Оцененные посты" / "Управление игрой" /
//   "Действия с игрой" / "Модерация игры"
//
// Rooms nest under their group row and drop their own "- " prefix; the
// indent is the nesting. Every navigation row carries a counter, zero
// included, and a counter is grey down to its brackets (SidebarCounter).
// Where the server keeps an unread counter that is what the row shows
// (rooms, discussion, characters); reviews have no unread counter on the
// wire, so those rows show the totals it does send.
//
// "Обсуждение" is a row for everybody who sees the game: the discussion is
// read by all of them, empty or not, whoever may write in it.
//
// Role gates (never a single blanket "moderator" gate):
//   "Управление игрой" — the header shows for Master/Assistant/Mentor.
//     "Настройки" is Master/Assistant/Mentor and "Заметки игры" narrower
//     still — Master/Assistant, the two the notepad resolver admits; the
//     rest of the edit items ("Создать NPC", "Редактировать NPC", the status
//     buttons) are Master/Assistant only. "Отправить на проверку" is narrower
//     still — the master alone, and only while the game is on edits
//     (GameIntention.SubmitForApproval plus the premoderation machine's one
//     legality condition).
//   "Действия с игрой" — any authenticated user except the master
//     (subscribe / apply-to-join, via features/game-actions).
//   "Модерация игры" — the premoderation verdict is Mentor+ (global) and is
//     two rows, both legal from every status; delete-others-game is
//     SeniorModerator+, reset-recruitment is Admin-only.
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
import { UserRole, type GeneralError } from "@/shared/api/models/common";
import { useExpandableSection } from "@/shared/lib/composables/useExpandableSection";
import SidebarBlock from "./SidebarBlock.vue";
import SidebarCounter from "./SidebarCounter.vue";
import SidebarSectionTitle from "./SidebarSectionTitle.vue";
import SidebarSkeleton from "./SidebarSkeleton.vue";
import GameRoomLink from "./GameRoomLink.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";

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
  characters,
  isMaster: servedMaster,
  isAssistant: servedAssistant,
  canManage: servedManage,
} = storeToRefs(store);

const { user } = storeToRefs(useAuthStore());

// The roles in the game are the server's answer for whoever held the session
// when the game was fetched, and the menu must not outlive that session. A 401
// on any request ends it at once: the auth store drops the viewer and the
// header turns to "Вход". The game shell refetches the game for the guest, but
// that is a round trip away and may not land at all, and until it does every
// row below was still offered to a guest: "Управление игрой", "Настройки", the
// status buttons. So the menu asks who is signed in before it reads the roles.
const signedIn = computed(() => !!user.value);
const isMaster = computed(() => signedIn.value && servedMaster.value);
const isAssistant = computed(() => signedIn.value && servedAssistant.value);
const canManage = computed(() => signedIn.value && servedManage.value);

// Master/assistant may run the game itself: NPCs and the status transitions.
// The game mentor ("Наставник" — helper to a novice master) is not one of them.
const canEdit = computed(() => isMaster.value || isAssistant.value);

// Asking a mentor to look at the game is the master's move and nobody else's:
// the server admits GameIntention.SubmitForApproval for the master only, and
// the premoderation machine refuses the move from any status but AwaitingEdits.
// Both halves are repeated here, because a row the server would answer 403 or
// 400 to is a promise the panel had no right to make.
const canSubmitForApproval = computed(
  () => isMaster.value && game.value?.premoderationStatus === "AwaitingEdits",
);

// The settings page is wider than that, and the link has to match it or the
// audience it was widened for reaches the page only by typing the URL:
// GameIntention.EditSettings admits the curating mentor, because helping a
// newbie shape the game is what the curator is for. Senior moderation is
// admitted by the same intention and still gets no row here — staff powers are
// not interface copy, and "Модерация игры" below is where they live.
const canEditSettings = computed(() => canManage.value);

// "Заметки игры" — the notepad of the game, shared by the master and the
// assistants and hidden from everyone else. Narrower than the settings row
// above: NotepadIntentionResolver answers this notepad with HasEditAccess, so
// the curating mentor is refused, and the row used to offer a mentor a page
// the API answered 403 to.
const canUseNotepad = computed(() => isMaster.value || isAssistant.value);

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

// The spoiler's whole label, brackets included, so the button holds one
// interpolation and nothing else: a text node of pure whitespace is dropped
// when the template is compiled, one that carries a bracket too is not, and
// the space before "(показать)" would then depend on how the tag was wrapped.
// It is written once, in the row itself, as a non-breaking space.
const archivedToggleLabel = computed(() =>
  showArchived.value ? "(скрыть)" : "(показать)",
);

// Characters are loaded (only when not already in the store) to drive the
// "Редактировать NPC" panel item.
watch(
  () => game.value?.id,
  (id) => {
    if (!id) return;
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
const canActOnGame = computed(() => signedIn.value && !isMaster.value);

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

// Confirmed moderation actions share a single dialog, and the request is the
// dialog's action: it waits shut for the answer, prints a refusal in itself
// and closes on success alone.
interface ModAction {
  title: string;
  message: string;
  confirmLabel: string;
  /** What the window says when the server named nothing. */
  failure: string;
  run: () => Promise<{ error: GeneralError | null }>;
}
const pendingMod = ref<ModAction | null>(null);

/** The window prints the refusal, so the interceptor keeps quiet about it. */
const DIALOG_OWNS_REFUSAL = { ownsRefusal: true, ownsRateLimit: true } as const;

// The three premoderation moves, each with the wording of its own dialog. One
// table, because the row, the question and the request have to name the same
// move: two of them differ only in which way the verdict goes.
const PREMOD_PROMPTS: Record<
  GamePremoderationTransition,
  { title: string; message: string; confirmLabel: string }
> = {
  [GamePremoderationTransition.SetApproved]: {
    title: "Одобрить игру?",
    message: "Игра станет видна всем",
    confirmLabel: "Одобрить",
  },
  [GamePremoderationTransition.SetAwaitingEdits]: {
    title: "Вернуть игру на доработку?",
    message: "Автор сможет доработать игру и отправить ее на проверку заново",
    confirmLabel: "Вернуть",
  },
  [GamePremoderationTransition.SubmitForApproval]: {
    title: "Отправить игру на проверку?",
    message: "Игра уйдет наставнику на проверку",
    confirmLabel: "Отправить",
  },
};

function askPremod(t: GamePremoderationTransition) {
  pendingMod.value = {
    ...PREMOD_PROMPTS[t],
    failure: "Не удалось изменить премодерацию",
    run: async () => ({
      error: await store.changePremoderation(t, DIALOG_OWNS_REFUSAL),
    }),
  };
}

function askDelete() {
  pendingMod.value = {
    title: "Удалить игру?",
    message: "Игра будет удалена без возможности восстановления",
    confirmLabel: "Удалить",
    failure: "Не удалось удалить игру",
    run: async () => {
      const error = await store.deleteGame(DIALOG_OWNS_REFUSAL);
      if (!error) router.push({ name: "games" });
      return { error };
    },
  };
}

function askResetRecruitment() {
  pendingMod.value = {
    title: "Сбросить дату набора?",
    message: "Дата начала набора очистится",
    confirmLabel: "Сбросить",
    failure: "Не удалось сбросить дату набора",
    run: async () => ({
      error: await store.resetRecruitment(DIALOG_OWNS_REFUSAL),
    }),
  };
}

const runMod = () => pendingMod.value?.run();
</script>

<template>
  <SidebarBlock token="GamePanel" title="Меню игры">
    <!-- 1. loading -->
    <SidebarSkeleton v-if="gameLoading && !game" :lines="8" />

    <!-- 2. error -->
    <li v-else-if="gameError">
      <SecondaryText class="error">{{ gameError }}</SecondaryText>
    </li>

    <!-- 3. content -->
    <template v-else-if="game">
      <!-- "Активные комнаты" — the group row; active post-rooms and chat
           rooms follow it indented, in one list. -->
      <li class="link">
        <span class="muted" aria-hidden="true">- </span>Активные комнаты
      </li>
      <SidebarSkeleton v-if="roomsLoading && !rooms.length" :lines="3" />
      <li v-else-if="roomsError">
        <SecondaryText class="error">{{ roomsError }}</SecondaryText>
      </li>
      <li v-else-if="activeRooms.length">
        <ul class="room-list">
          <GameRoomLink
            v-for="room in activeRooms"
            :key="room.id"
            :room="room"
            :game-public-id="publicId"
            prefix=""
          />
        </ul>
      </li>
      <li v-else><SecondaryText>Комнат пока нет</SecondaryText></li>

      <!-- "Архивные комнаты" — the same group row, with an inline
           "(показать)/(скрыть)" spoiler link; rooms appear below when
           expanded. -->
      <template v-if="archivedRooms.length">
        <li class="link">
          <span class="muted" aria-hidden="true">- </span>Архивные
          комнаты&nbsp;<button
            type="button"
            class="archived-toggle"
            :aria-expanded="showArchived"
            @click="toggleArchived()"
          >
            {{ archivedToggleLabel }}
          </button>
        </li>
        <li>
          <div
            ref="archivedZoneRef"
            class="expand-zone"
            v-bind="archivedZoneBindings"
          >
            <ul v-if="showArchived" class="room-list">
              <GameRoomLink
                v-for="room in archivedRooms"
                :key="room.id"
                :room="room"
                :game-public-id="publicId"
                prefix=""
              />
            </ul>
          </div>
        </li>
      </template>

      <!-- Game navigation — a flat list of links (like the old site's game
           actions strip), no "Разделы"/"Обсуждение" sub-headings. -->
      <li class="link">
        <span class="muted" aria-hidden="true">- </span>
        <router-link :to="{ name: 'game', params: { id: publicId } }"
          >Информация</router-link
        >
      </li>
      <li class="link">
        <span class="muted" aria-hidden="true">- </span>
        <router-link :to="{ name: 'game-comments', params: { id: publicId } }"
          >Обсуждение</router-link
        ><SidebarCounter :value="game.unreadCommentsCount" />
      </li>
      <li class="link">
        <span class="muted" aria-hidden="true">- </span>
        <router-link :to="{ name: 'game-characters', params: { id: publicId } }"
          >Персонажи</router-link
        ><SidebarCounter :value="game.unreadCharactersCount" />
      </li>
      <li class="link">
        <span class="muted" aria-hidden="true">- </span>
        <router-link :to="{ name: 'game-reviews', params: { id: publicId } }"
          >Рецензии</router-link
        ><SidebarCounter :value="game.gameReviewsCount" />
      </li>
      <li class="link">
        <span class="muted" aria-hidden="true">- </span>
        <router-link
          :to="{ name: 'game-post-reviews', params: { id: publicId } }"
          >Оцененные посты</router-link
        ><SidebarCounter :value="game.postReviewsCount" />
      </li>

      <!-- "Управление игрой" section: settings and the notepad for the leads
           and the curating mentor, the rest for the leads alone. -->
      <template v-if="canEditSettings || canEdit || canUseNotepad">
        <SidebarSectionTitle>Управление игрой</SidebarSectionTitle>
        <li v-if="canEditSettings" class="link">
          <span class="muted" aria-hidden="true">- </span>
          <router-link :to="{ name: 'game-settings', params: { id: publicId } }"
            >Настройки</router-link
          >
        </li>
        <template v-if="canEdit">
          <li class="link">
            <span class="muted" aria-hidden="true">- </span>
            <router-link
              :to="{
                name: 'game-character-create',
                params: { id: publicId },
                query: { npc: '1' },
              }"
              >Создать NPC</router-link
            >
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
              >Редактировать NPC</router-link
            >
          </li>
          <!-- Status transition buttons (Master/Assistant) -->
          <GameStatusButtons variant="strip" />
        </template>
        <!-- The master's own premoderation move. Not inside the canEdit block:
             an assistant edits the game but does not declare it ready. -->
        <li v-if="canSubmitForApproval" class="link">
          <span class="muted" aria-hidden="true">- </span>
          <button
            type="button"
            class="strip-action"
            @click="askPremod(GamePremoderationTransition.SubmitForApproval)"
          >
            Отправить на проверку
          </button>
        </li>
        <li v-if="canUseNotepad" class="link">
          <span class="muted" aria-hidden="true">- </span>
          <router-link :to="{ name: 'game-notepad', params: { id: publicId } }"
            >Заметки игры</router-link
          >
        </li>
      </template>

      <!-- "Действия с игрой" section (any authed except master) -->
      <template v-if="canActOnGame">
        <SidebarSectionTitle>Действия с игрой</SidebarSectionTitle>
        <GameJoinActions variant="strip" />
      </template>

      <!-- "Модерация игры" section (global roles) -->
      <template v-if="showModeration">
        <SidebarSectionTitle>Модерация игры</SidebarSectionTitle>
        <!-- The verdict, both ways. Legal from every status on the server, so
             neither row is hidden by the game's current premoderation state. -->
        <template v-if="isGlobalMentor">
          <li class="link">
            <span class="muted" aria-hidden="true">- </span>
            <button
              type="button"
              class="strip-action"
              @click="askPremod(GamePremoderationTransition.SetApproved)"
            >
              Одобрить игру
            </button>
          </li>
          <li class="link">
            <span class="muted" aria-hidden="true">- </span>
            <button
              type="button"
              class="strip-action"
              @click="askPremod(GamePremoderationTransition.SetAwaitingEdits)"
            >
              Вернуть на доработку
            </button>
          </li>
        </template>
        <li v-if="isGlobalSeniorModerator" class="link">
          <span class="muted" aria-hidden="true">- </span>
          <button type="button" class="strip-action" @click="askDelete">
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
    <li v-else><SecondaryText>Игра не найдена</SecondaryText></li>

    <ConfirmDialog
      :show="!!pendingMod"
      :title="pendingMod?.title ?? ''"
      :message="pendingMod?.message ?? ''"
      :confirm-label="pendingMod?.confirmLabel"
      :failure="pendingMod?.failure"
      :action="runMod"
      @update:show="(open: boolean) => !open && (pendingMod = null)"
    />
  </SidebarBlock>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

// Rooms nest under their group row: the indent is the nesting, which is why
// the room rows carry no "- " prefix of their own.
//
// The indent is the width of that prefix, so a room's lock starts exactly
// where the text of a top level row does and the titles follow one gap after
// it. Measured in the browser at the sidebar's own size, "- " renders 8.3 to
// 8.8 pixels wide, which $small is; the previous $medium set the whole list
// two dashes deep and left the locks hanging past the column.
//
// The indent is the first line's alone: a row that wraps, a long title or
// the writers of a closed room, goes on under the start of the menu line, as
// the old site wraps "[Военно-полевой госпиталь] - Никита, Север".
.room-list
  list-style: none
  margin: 0
  padding: 0

  > li
    text-indent: $small

// "(показать)/(скрыть)" spoiler toggle for archived rooms — an inline link
// beside the group row (normal weight, so nothing in the row reads bold).
// The gap before it is the row's own non-breaking space, not a margin: the
// row has to copy with that space in it.
.archived-toggle
  +inline-link-button

  &
    font-weight: normal

.link
  display: block

.muted
  color: $text-muted

.error
  color: $accent-red

// Strip actions are words of the menu like the links around them, and they
// trigger a store mutation. Deleting is not coloured: it asks first, and the
// question carries the weight of the act.
.strip-action
  +inline-link-button
</style>
