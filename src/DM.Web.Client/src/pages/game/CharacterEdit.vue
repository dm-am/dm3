<script setup lang="ts">
/**
 * CharacterEdit — edit an existing character and drive its lifecycle status
 * (accept / decline / retire-as-dead / exile / leave / resurrect) plus
 * soft-delete.
 *
 * The schema-driven CharacterForm (features/edit-character) handles the field
 * edits and persists via gameApi.updateCharacter. Status transitions and delete
 * are separate actions below the form: they call gameApi.changeCharacterStatus /
 * deleteCharacter and never round-trip the character's (server-rendered) BbCode
 * attribute values.
 *
 * When the acting user can edit more than one character in the game, a selector
 * switches between them; without a valid :characterId the first is preselected.
 *
 * Gating: master/assistant may manage any character (accept/decline/exile/dead/
 * resurrect, delete NPCs); the owner may leave the game or delete their own
 * character, and leaving is final. The backend is the final authority.
 */
import { computed, onMounted, ref, watch } from "vue";
import { htmlToBbcode } from "@/shared/lib/utils/bbcode";
import { useRoute, useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import {
  useGameDetailsStore,
  characterStatusLabel,
  gameApi,
  GameStatus,
  type Character,
  type CharacterStatusTransition,
} from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import { CharacterForm } from "@/features/edit-character";
import { Select } from "@/shared/ui/Select";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
import { ButtonRow } from "@/shared/ui/ButtonRow";
import type { GeneralError } from "@/shared/api/models/common";

const route = useRoute();
const router = useRouter();
const store = useGameDetailsStore();
const { game, characters, isMaster, isAssistant } = storeToRefs(store);
const { user } = storeToRefs(useAuthStore());

const gameId = computed(() => route.params.id as string);
const routeCharacterId = computed(() => route.params.characterId as string);

const canManage = computed(() => isMaster.value || isAssistant.value);

// An empty set until the game answers: the sheet arrives with the game.
const schema = computed(() => game.value?.schema ?? { specifications: [] });

onMounted(async () => {
  if (!game.value) await store.loadGame(gameId.value);
  if (!characters.value.length) await store.loadCharacters(gameId.value);
});

// The application this page has just declined, kept on the page for its status
// to be read.
//
// A declined application is not a character anybody edits, and it leaves the
// set below. Dropping it the moment the server answers used to leave the reader
// on somebody else's sheet or on "Нет персонажей, доступных для
// редактирования" - the selector, the status line and the buttons all
// reshuffled, and the one thing none of them said was what became of the
// application. A plate in the corner said it for three seconds and named no
// one, over a page that had just changed character.
//
// So the seventh transition is answered the way the other six are: the sheet
// stays, and the status line under "Статус персонажа" reads the new status.
// The knowledge belongs to this press, not to the roster: the reader switching
// to another character releases it, and a reload starts without it, the same
// as the receipts the two-factor blocks leave behind. Whether the application
// is still declined is the roster's word, not this ref's - the filter reads
// both.
const justDeclinedId = ref<string | null>(null);

// Characters the acting user may edit: staff edit everything except declined
// applications; a player edits only their own.
const editableCharacters = computed<Character[]>(() =>
  characters.value.filter((c) => {
    const id = c.id as unknown as string;
    if (c.status === "Declined" && id !== justDeclinedId.value) return false;
    return canManage.value || c.author?.id === user.value?.id;
  }),
);

const selectOptions = computed(() =>
  editableCharacters.value.map((c) => ({
    value: c.id as unknown as string,
    label: c.name + (c.isNpc ? " (NPC)" : ""),
  })),
);

const selected = computed<Character | undefined>(() => {
  const byRoute = editableCharacters.value.find(
    (c) => (c.id as unknown as string) === routeCharacterId.value,
  );
  return byRoute ?? editableCharacters.value[0];
});

const selectedId = computed(() => selected.value?.id as unknown as string);

// Keep the URL in sync with the effective selection (preselect-first case).
watch([selected, editableCharacters], () => {
  if (selected.value && selectedId.value !== routeCharacterId.value) {
    router.replace({
      name: "game-character-edit",
      params: { id: gameId.value, characterId: selectedId.value },
    });
  }
});

function switchCharacter(id: string) {
  // Leaving the declined application is what lets it go from the selector.
  justDeclinedId.value = null;
  router.replace({
    name: "game-character-edit",
    params: { id: gameId.value, characterId: id },
  });
}

const isOwner = computed(
  () => !!selected.value?.author && selected.value.author.id === user.value?.id,
);

// --- Full character for the form ----------------------------------------------
// The list endpoint returns characters without attributes, so the form is fed
// from the single-character AuthorEdit fetch: non-BBCode raw values in `value`,
// BBCode rendered round-trip HTML in `valueBbText`. Without this seed, saving
// would drop every attribute the user did not retype.
const detail = ref<Character | null>(null);
const detailLoading = ref(false);
const detailError = ref(false);

watch(
  selectedId,
  async (id) => {
    detail.value = null;
    detailError.value = false;
    if (!id) return;
    detailLoading.value = true;
    const { data, error } = await gameApi.getCharacterForEdit(id);
    // Ignore a late response for a character the user has switched away from.
    if (selectedId.value !== id) return;
    detailLoading.value = false;
    if (error || !data?.resource) {
      detailError.value = true;
      return;
    }
    detail.value = data.resource;
  },
  { immediate: true },
);

// Raw initial values keyed by specification id for CharacterForm.
//
// valueBbText comes from the AuthorEdit audience, which returns HTML carrying
// data-bb-* attributes rather than the source: BbConverter calls RenderHtml for
// every audience except plain text. The editor takes and returns BBCode, so it
// escapes incoming HTML and hands it back as the field's text on save.
// htmlToBbcode is the reverse half of that same round trip.
const initialValues = computed<Record<string, string>>(() => {
  const map: Record<string, string> = {};
  for (const attr of detail.value?.attributes ?? []) {
    const bbText = attr.valueBbText as unknown as string | undefined;
    map[attr.id as unknown as string] =
      attr.value ?? (bbText ? htmlToBbcode(bbText) : "");
  }
  return map;
});

// The game's blacklist closes the viewer's own application: an edit of it is
// applying again. The sheet of a character in play or out of it stays theirs,
// and so do taking the character out of the game and deleting it. The form
// goes without a word in its place.
const applicationClosed = computed(() => {
  const c = detail.value;
  return (
    !!game.value?.viewerBlacklisted &&
    !!c?.author &&
    c.author.id === user.value?.id &&
    (c.status === "UnderReview" || c.status === "Declined")
  );
});

// Whether the acting user may edit the character's fields (mirrors the
// backend CharacterIntention.Edit rule): the owner while the game is active,
// unless it is an application the blacklist closes; master/assistant only for
// NPCs or characters that allow editByMaster. Everyone else on this page still
// gets the status actions below.
const canEditFields = computed(() => {
  const c = detail.value;
  if (!c) return false;
  const owned = !!c.author && c.author.id === user.value?.id;
  if (owned && game.value?.status === GameStatus.Active)
    return !applicationClosed.value;
  return canManage.value && (!!c.isNpc || !!c.privacy?.editByMaster);
});

// --- Status / delete actions -------------------------------------------------

interface CharacterAction {
  key: string;
  label: string;
  /**
   * The confirmation this action asks for, absent when it runs on the press.
   * The three parts are one wording, not three: the heading asks the question
   * with the verb the confirm button carries, and the sentence names the one
   * consequence of saying yes. The row button keeps its own longer label,
   * which names the object the roster is showing.
   */
  confirm?: { title: string; message: string; action: string };
  /** The sentence for a refusal the server did not word. */
  failure: string;
  /**
   * Sends the request and answers with the refusal, if any. Handed the flags
   * of the confirmation window when the action is asked in one: the window
   * prints the refusal itself, and the interceptor keeps quiet about it.
   */
  run: (options?: RefusalFlags) => Promise<{ error: GeneralError | null }>;
}

/** The refusal flags of a request sent from a window. */
type RefusalFlags = Parameters<typeof gameApi.deleteCharacter>[1];

const STATUS_FAILED = "Не удалось изменить статус персонажа";

const busy = ref(false);
const pending = ref<CharacterAction | null>(null);

// The status in words, under the heading that names it.
//
// Without this line the section said what it was about and never what it held:
// the only trace of a transition was one button leaving the row and another
// taking its place, and a status outlives the press that set it by weeks. The
// caption is the entity's own (the card and the character's sheet print the
// same one), so the lifecycle is not renamed here.
//
// Active is the exception the entity leaves empty on purpose — in a roster a
// caption under every name says nothing — and a section titled "Статус
// персонажа" cannot go blank for the commonest status of all. "В игре" is the
// word the profile games table already gives it, not a new one.
//
// Declined is the other one the entity leaves empty: a declined application is
// drawn on no roster and no card, so the entity has no caption for it. This
// page draws one, for the one press that just made it so (justDeclinedId), and
// the words are the screen's own - the button reads "Отклонить заявку".
const statusLabel = computed<string | null>(() => {
  const c = selected.value;
  if (!c) return null;
  if (c.status === "Active") return "В игре";
  if (c.status === "Declined") return "Заявка отклонена";
  return characterStatusLabel(c);
});

async function applyStatus(
  transition: CharacterStatusTransition,
  options?: RefusalFlags,
): Promise<{ error: GeneralError | null }> {
  const c = selected.value;
  if (!c) return { error: null };
  busy.value = true;
  const { error } = await gameApi.changeCharacterStatus(
    c.id as unknown as string,
    transition,
    options,
  );
  busy.value = false;
  if (error) return { error };
  // No plate: every transition is answered by the line above the buttons. The
  // roster comes back carrying the new status and the section reads it out -
  // including a declined application, which is pinned to the page before the
  // roster is asked, because the moment the roster answers is the moment the
  // filter would otherwise drop it and move the page to another sheet.
  justDeclinedId.value =
    transition === "Decline" ? (c.id as unknown as string) : null;
  await store.loadCharacters(gameId.value);
  return { error: null };
}

/** A transition as the request of an action, with the sentence for its refusal. */
const transitionTo = (transition: CharacterStatusTransition) => ({
  failure: STATUS_FAILED,
  run: (options?: RefusalFlags) => applyStatus(transition, options),
});

async function doDelete(
  options?: RefusalFlags,
): Promise<{ error: GeneralError | null }> {
  const c = selected.value;
  if (!c) return { error: null };
  busy.value = true;
  const { error } = await gameApi.deleteCharacter(
    c.id as unknown as string,
    options,
  );
  busy.value = false;
  if (error) return { error };
  // No plate: the deletion is the whole of what the reader is left looking at.
  // The roster is fetched again without the character (the endpoint drops
  // removed ones), and this page — the sheet of the character that no longer
  // exists — is left for that roster. Reload first, navigate second, so the
  // list is already short by one card when it appears instead of showing the
  // deleted one for a beat.
  await store.loadCharacters(gameId.value);
  router.push({ name: "game-characters", params: { id: gameId.value } });
  return { error: null };
}

const actions = computed<CharacterAction[]>(() => {
  const c = selected.value;
  if (!c) return [];
  const list: CharacterAction[] = [];

  if (c.status === "UnderReview" && canManage.value) {
    list.push({
      key: "accept",
      label: "Принять в игру",
      ...transitionTo("Accept"),
    });
    list.push({
      key: "decline",
      label: "Отклонить заявку",
      confirm: {
        title: "Отклонить заявку?",
        message: "Персонаж не попадет в игру",
        action: "Отклонить",
      },
      ...transitionTo("Decline"),
    });
  }

  if (c.status === "Active") {
    if (canManage.value) {
      list.push({
        key: "dead",
        label: "Отметить погибшим",
        confirm: {
          title: "Отметить персонажа погибшим?",
          message: "Персонаж погибнет и выйдет из игры",
          action: "Отметить",
        },
        ...transitionTo("Kill"),
      });
      list.push({
        key: "exile",
        label: "Изгнать из игры",
        confirm: {
          title: "Изгнать персонажа из игры?",
          message: "Персонаж выйдет из игры",
          action: "Изгнать",
        },
        ...transitionTo("Exile"),
      });
    }
    if (isOwner.value && !c.isNpc) {
      list.push({
        key: "leave",
        label: "Покинуть игру",
        confirm: {
          title: "Покинуть игру?",
          message: "Персонаж выйдет из игры без возможности вернуться",
          action: "Покинуть",
        },
        ...transitionTo("Leave"),
      });
    }
  }

  // Mirrors the backend rule: a lead undoes only his own move, so staff bring
  // back the character they killed or exiled, each by its own transition. What
  // the player took out of the game nobody brings back, whatever else marks it.
  if (c.status === "Retired" && canManage.value && !c.isPlayerLeft) {
    if (c.isDead) {
      list.push({
        key: "resurrect",
        label: "Вернуть в игру",
        ...transitionTo("Resurrect"),
      });
    } else if (c.isPlayerExiled) {
      list.push({
        key: "readmit",
        label: "Вернуть в игру",
        ...transitionTo("Readmit"),
      });
    }
  }

  // Delete: the owner deletes their own character; staff delete NPCs.
  if (isOwner.value || (canManage.value && c.isNpc)) {
    list.push({
      key: "delete",
      label: c.isNpc ? "Удалить NPC" : "Удалить персонажа",
      confirm: {
        title: c.isNpc ? "Удалить NPC?" : "Удалить персонажа?",
        message: c.isNpc
          ? "NPC будет удален без возможности восстановления"
          : "Персонаж будет удален без возможности восстановления",
        action: "Удалить",
      },
      failure: "Не удалось удалить персонажа",
      run: doDelete,
    });
  }

  return list;
});

/**
 * A refused transition that asks nothing first, under the row of buttons it
 * was pressed in; the status line above keeps the status the server still
 * has. It used to be a plate.
 */
const statusResult = useActionResult();

async function trigger(action: CharacterAction) {
  statusResult.clear();
  if (action.confirm) {
    pending.value = action;
    return;
  }
  const { error } = await action.run();
  if (error) statusResult.refuse(error, action.failure);
}

// The request of the confirmation. The window waits for it, prints a refusal in
// itself and closes on success alone.
async function confirmPending() {
  return pending.value?.run({ ownsRefusal: true, ownsRateLimit: true });
}

function onSaved() {
  // No plate: the sheet answers with its own submit button, which reads
  // "Сохранено" and goes inert until something in the form changes again.
  // The button belongs to CharacterForm, so that is where the answer lives.
  store.loadCharacters(gameId.value);
}

function onCancel() {
  router.push({ name: "game-characters", params: { id: gameId.value } });
}
</script>

<template>
  <div class="character-edit">
    <secondary-text v-if="!editableCharacters.length" class="empty">
      Нет персонажей, доступных для редактирования
    </secondary-text>

    <template v-else-if="selected">
      <div v-if="selectOptions.length > 1" class="character-switch">
        <label class="switch-label" for="character-switch">Персонаж:</label>
        <Select
          id="character-switch"
          :model-value="selectedId"
          :options="selectOptions"
          @update:model-value="(v) => switchCharacter(v as string)"
        />
      </div>

      <secondary-text v-if="detailLoading"
        >Загрузка персонажа...</secondary-text
      >
      <secondary-text v-else-if="detailError" class="error">
        Не удалось загрузить персонажа
      </secondary-text>
      <!-- Only for whoever may edit the sheet. Anybody else here (the master
           at a player's character the player keeps to themselves, the owner at
           an application the blacklist closes) has the status below and no
           form, and no line about the form either: what the reader may not
           do is not on the page. -->
      <CharacterForm
        v-else-if="detail && canEditFields"
        :key="selectedId"
        :schema="schema"
        :character="detail"
        :character-id="selectedId"
        :game-id="game?.id ?? gameId"
        :initial-name="detail.name"
        :initial-values="initialValues"
        @saved="onSaved"
        @cancel="onCancel"
      />

      <!-- The section stands whether or not the character has a transition
           left: exiling a player's character takes the last button out of the
           row, and that is exactly the moment the status has to stay legible. -->
      <section class="status-section">
        <block-title>Статус персонажа</block-title>
        <secondary-text v-if="statusLabel" class="status-value">
          {{ statusLabel }}
        </secondary-text>
        <!-- Every transition on every width: one line where they fit, one
             under another where they do not (ButtonRow). -->
        <ButtonRow v-if="actions.length" class="status-actions">
          <button
            v-for="action in actions"
            :key="action.key"
            type="button"
            class="status-btn"
            :disabled="busy"
            @click="trigger(action)"
          >
            {{ action.label }}
          </button>
        </ButtonRow>
        <ActionResult :result="statusResult" />
      </section>
    </template>

    <ConfirmDialog
      :show="!!pending"
      :title="pending?.confirm?.title ?? ''"
      :message="pending?.confirm?.message ?? ''"
      :confirm-label="pending?.confirm?.action"
      :failure="pending?.failure"
      :action="confirmPending"
      @update:show="(shown) => !shown && (pending = null)"
    />
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

.character-edit
  max-width: $grid-step * 200

.empty
  display: block

.error
  color: $accent-red

.character-switch
  display: flex
  align-items: center
  gap: $small
  margin-bottom: $medium

// A caption of a field of the page: +field-caption, as FormField draws it.
.switch-label
  +field-caption

.status-section
  margin-top: $big
  padding: $medium
  background-color: $bg-element

// A row of buttons (ButtonRow): the transitions in one line at every width.
.status-actions
  margin-top: $small

// The shared secondary button, a dangerous transition included: that one
// asks first, and the question carries the weight of the act.
.status-btn
  font-size: $secondary-font-size
  +button
</style>
