<script setup lang="ts">
// "Меню игры": every row is offered only to the viewer the server would let through.
import { computed, ref, watch } from "vue";
import { storeToRefs } from "pinia";
import { useRouter } from "vue-router";
import {
  useGameDetailsStore,
  GamePremoderationTransition,
} from "@/entities/game";
import { GameStatusButtons, GameJoinActions } from "@/features/game-actions";
import {
  useAuthStore,
  userIsAdmin,
  userIsMentor,
  userIsSeniorModerator,
} from "@/entities/user";
import type { GeneralError } from "@/shared/api/models/common";
import { useExpandableSection } from "@/shared/lib/composables/useExpandableSection";
import SidebarBlock from "./SidebarBlock.vue";
import SidebarCounter from "./SidebarCounter.vue";
import SidebarSectionTitle from "./SidebarSectionTitle.vue";
import SidebarSkeleton from "./SidebarSkeleton.vue";
import GameRoomLink from "./GameRoomLink.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

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

// The roles answer for the session the game was read in: a sign-out ends them at once.
const signedIn = computed(() => !!user.value);
const isMaster = computed(() => signedIn.value && servedMaster.value);
const isAssistant = computed(() => signedIn.value && servedAssistant.value);
const canManage = computed(() => signedIn.value && servedManage.value);

// The master and the assistants run the game and its notepad; the game's mentor does not.
const canEdit = computed(() => isMaster.value || isAssistant.value);

// The master's move alone, and only while the game awaits edits, as the server rules.
const canSubmitForApproval = computed(
  () => isMaster.value && game.value?.premoderationStatus === "AwaitingEdits",
);

const router = useRouter();

// The public id, or the route's own param until the game resolves.
const publicId = computed(() => game.value?.publicId ?? props.gameId);

// Archived rooms open by a spoiler, a member of "Развернуть все" while there are any.
const showArchived = ref(false);
const archivedZoneRef = ref<HTMLElement | null>(null);
const { toggle: toggleArchived, zoneBindings: archivedZoneBindings } =
  useExpandableSection({
    el: archivedZoneRef,
    model: showArchived,
    registryEnabled: () => archivedRooms.value.length > 0,
    label: "GamePanelArchivedRooms",
  });

// The whole label in one interpolation, so the space before it stays the row's own.
const archivedToggleLabel = computed(() =>
  showArchived.value ? "(скрыть)" : "(показать)",
);

// The characters drive "Редактировать неигрового персонажа".
watch(
  () => game.value?.id,
  (id) => {
    if (!id) return;
    if (!characters.value.length) store.loadCharacters(id);
  },
  { immediate: true },
);

// "Редактировать неигрового персонажа" opens the first NPC of the game.
const npcs = computed(() => characters.value.filter((c) => c.isNpc));
const firstNpcId = computed(() => npcs.value[0]?.id ?? null);

// Anybody signed in but the master may subscribe or apply.
const canActOnGame = computed(() => signedIn.value && !isMaster.value);

const isGlobalMentor = computed(() => userIsMentor(user.value));
const isGlobalSeniorModerator = computed(() =>
  userIsSeniorModerator(user.value),
);
const isGlobalAdmin = computed(() => userIsAdmin(user.value));

// One dialog for the moderation moves; the request is its action.
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

// One table, so the row, the question and the request name the same move.
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
      error: await store.changePremoderation(t, OWNS_REFUSAL),
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
      const error = await store.deleteGame(OWNS_REFUSAL);
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
      error: await store.resetRecruitment(OWNS_REFUSAL),
    }),
  };
}

const runMod = () => pendingMod.value?.run();
</script>

<template>
  <SidebarBlock token="GamePanel" title="Меню игры">
    <SidebarSkeleton v-if="gameLoading && !game" :lines="8" />

    <li v-else-if="gameError">
      <SecondaryText class="error">{{ gameError }}</SecondaryText>
    </li>

    <template v-else-if="game">
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

      <template v-if="canManage || canEdit">
        <SidebarSectionTitle>Управление игрой</SidebarSectionTitle>
        <!-- The settings admit the curating mentor too -->
        <li v-if="canManage" class="link">
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
              >Создать неигрового персонажа</router-link
            >
          </li>
          <li v-if="firstNpcId" class="link">
            <span class="muted" aria-hidden="true">- </span>
            <router-link
              :to="{
                name: 'game-character-edit',
                params: { id: publicId, characterId: firstNpcId },
              }"
              >Редактировать неигрового персонажа</router-link
            >
          </li>
          <GameStatusButtons />
        </template>
        <!-- Not inside canEdit: an assistant edits the game but does not declare it ready -->
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
        <li v-if="canEdit" class="link">
          <span class="muted" aria-hidden="true">- </span>
          <router-link :to="{ name: 'game-notepad', params: { id: publicId } }"
            >Заметки игры</router-link
          >
        </li>
      </template>

      <template v-if="canActOnGame">
        <SidebarSectionTitle>Действия с игрой</SidebarSectionTitle>
        <GameJoinActions />
      </template>

      <template v-if="isGlobalMentor">
        <SidebarSectionTitle>Модерация игры</SidebarSectionTitle>
        <!-- The verdict both ways, legal from every status -->
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
@use "@/assets/styles/Links" as *

// Rooms nest by an indent of the "- " prefix's width, on the first line alone, as the old site wraps.
.room-list
  list-style: none
  margin: 0
  padding: 0

  > li
    text-indent: $small

// The spoiler beside the group row; the gap is the row's own space, so it copies.
.archived-toggle
  +inline-link-button

  &
    font-weight: normal

.link
  display: block

.error
  color: $accent-red

// Words of the menu like the links; deleting is not coloured, its question carries the weight.
.strip-action
  +inline-link-button
</style>
