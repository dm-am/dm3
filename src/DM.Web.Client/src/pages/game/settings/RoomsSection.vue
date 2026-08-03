<script setup lang="ts">
/**
 * RoomsSection — full room management for a game: create rooms, rename them,
 * edit per-room settings (private text / dice / menu visibility), switch access
 * type, manage the per-room access grants (characters and readers) for Private
 * rooms, and archive / unarchive / delete rooms.
 *
 * Create + archive go through the store (which re-syncs the active/archived
 * split); the remaining mutations call gameApi directly and reload the rooms
 * slice afterwards, matching the settings-page pattern.
 */
import { computed, onMounted, reactive, ref } from "vue";
import { storeToRefs } from "pinia";
import {
  useGameDetailsStore,
  gameApi,
  RoomType,
  RoomAccessType,
  RoomAccessPolicy,
  type Room,
  type RoomAccess,
  type CreateRoomInput,
} from "@/entities/game";
import { Form, FormField } from "@/shared/ui/Form";
import { Select } from "@/shared/ui/Select";
import { UserAutocomplete } from "@/entities/user";
import Button from "@/shared/ui/Button/Button.vue";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { useToast } from "@/shared/lib/composables/useToast";
import { notifyFailure } from "@/shared/lib/errors";

const store = useGameDetailsStore();
const { game, rooms, activeRooms, archivedRooms, characters } =
  storeToRefs(store);
const toast = useToast();

const typeOptions = [
  { value: RoomType.Default, label: "Посты" },
  { value: RoomType.Chat, label: "Чат" },
];
const accessOptions = [
  { value: RoomAccessType.Open, label: "Открытая" },
  { value: RoomAccessType.Private, label: "Приватная" },
];
const policyOptions = [
  { value: RoomAccessPolicy.Full, label: "Может писать" },
  { value: RoomAccessPolicy.ReadOnly, label: "Только чтение" },
];

const characterOptions = computed(() =>
  characters.value
    .filter((c) => c.status === "Active")
    .map((c) => ({ value: c.id as unknown as string, label: c.name })),
);

onMounted(() => {
  if (game.value) {
    if (!rooms.value.length)
      store.loadRooms(game.value.publicId ?? game.value.id);
    if (!characters.value.length)
      store.loadCharacters(game.value.publicId ?? game.value.id);
  }
});

// --- Create ---------------------------------------------------------------

const create = reactive({
  title: "",
  type: RoomType.Default,
  accessType: RoomAccessType.Open,
  viewPrivateText: false,
  viewDiceResults: false,
  diceEnabled: false,
});
const creating = ref(false);

async function createRoom() {
  if (!create.title.trim()) return;
  creating.value = true;
  const payload: CreateRoomInput = {
    title: create.title.trim(),
    type: create.type,
    accessType: create.accessType,
    viewPrivateText: create.viewPrivateText,
    viewDiceResults: create.viewDiceResults,
    diceEnabled: create.diceEnabled,
  };
  const error = await store.createRoom(payload);
  creating.value = false;
  if (error) {
    notifyFailure(error, "Не удалось создать комнату");
    return;
  }
  toast.success("Комната создана");
  create.title = "";
}

// --- Per-room editing -----------------------------------------------------

const expandedId = ref<string | null>(null);
const draft = reactive({
  title: "",
  accessType: RoomAccessType.Open,
  viewPrivateText: false,
  viewDiceResults: false,
  diceEnabled: false,
  hiddenWithoutAccess: false,
});
const savingRoom = ref(false);
const newReader = ref("");
const grantCharacterId = ref("");
const grantPolicy = ref<RoomAccessPolicy>(RoomAccessPolicy.Full);
const pendingDelete = ref<Room | null>(null);

function roomId(room: Room): string {
  return room.id as unknown as string;
}

function toggle(room: Room) {
  const id = roomId(room);
  if (expandedId.value === id) {
    expandedId.value = null;
    return;
  }
  expandedId.value = id;
  draft.title = room.title;
  draft.accessType = room.access ?? RoomAccessType.Open;
  draft.viewPrivateText = room.settings?.viewPrivateText ?? false;
  draft.viewDiceResults = room.settings?.viewDiceResults ?? false;
  draft.diceEnabled = room.settings?.diceEnabled ?? false;
  draft.hiddenWithoutAccess = room.settings?.hiddenWithoutAccess ?? false;
  newReader.value = "";
  grantCharacterId.value = "";
}

async function reloadRooms() {
  if (game.value) await store.loadRooms(game.value.publicId ?? game.value.id);
}

async function saveRoom(room: Room) {
  savingRoom.value = true;
  const { error } = await gameApi.updateRoom(roomId(room), {
    title: draft.title.trim(),
    access: draft.accessType,
    settings: {
      viewPrivateText: draft.viewPrivateText,
      viewDiceResults: draft.viewDiceResults,
      diceEnabled: draft.diceEnabled,
      // Sent even for an Open room, where the toggle is not shown: the block is
      // written whole, so leaving it out would clear the setting the master put
      // on the room before he opened it.
      hiddenWithoutAccess: draft.hiddenWithoutAccess,
    },
  } as Partial<Room>);
  savingRoom.value = false;
  if (error) {
    notifyFailure(error, "Не удалось сохранить комнату");
    return;
  }
  toast.success("Комната сохранена");
  await reloadRooms();
}

async function archive(room: Room) {
  const error = await store.archiveRoom(roomId(room));
  if (error) notifyFailure(error, "Не удалось архивировать комнату");
}

async function unarchive(room: Room) {
  const { error } = await gameApi.unarchiveRoom(roomId(room));
  if (error) {
    notifyFailure(error, "Не удалось вернуть комнату из архива");
    return;
  }
  await reloadRooms();
}

async function confirmDelete() {
  const room = pendingDelete.value;
  pendingDelete.value = null;
  if (!room) return;
  const { error } = await gameApi.deleteRoom(roomId(room));
  if (error) {
    notifyFailure(error, "Не удалось удалить комнату");
    return;
  }
  toast.success("Комната удалена");
  if (expandedId.value === roomId(room)) expandedId.value = null;
  await reloadRooms();
}

// --- Access grants --------------------------------------------------------

async function addReader(room: Room) {
  if (!newReader.value.trim()) return;
  const { error } = await gameApi.createRoomAccess(roomId(room), {
    user: { username: newReader.value.trim() },
  } as unknown as Partial<RoomAccess>);
  if (error) {
    notifyFailure(error, "Не удалось добавить читателя");
    return;
  }
  newReader.value = "";
  await reloadRooms();
}

async function addCharacterGrant(room: Room) {
  if (!grantCharacterId.value) return;
  const { error } = await gameApi.createRoomAccess(roomId(room), {
    character: { id: grantCharacterId.value },
    policy: grantPolicy.value,
  } as unknown as Partial<RoomAccess>);
  if (error) {
    notifyFailure(error, "Не удалось добавить доступ персонажу");
    return;
  }
  grantCharacterId.value = "";
  await reloadRooms();
}

async function removeAccess(access: RoomAccess) {
  const { error } = await gameApi.deleteRoomAccess(access.id);
  if (error) {
    notifyFailure(error, "Не удалось удалить доступ");
    return;
  }
  await reloadRooms();
}

function accessLabel(access: RoomAccess): string {
  if (access.character) return access.character.name;
  if (access.user) return access.user.username;
  return "—";
}
</script>

<template>
  <section class="settings-section">
    <BlockTitle>Управление комнатами</BlockTitle>

    <!-- Room list -->
    <div v-if="rooms.length" class="rooms-list">
      <template
        v-for="room in [...activeRooms, ...archivedRooms]"
        :key="room.id"
      >
        <div class="room-row">
          <button type="button" class="room-head" @click="toggle(room)">
            <span class="room-title">{{ room.title }}</span>
            <span v-if="room.isArchived" class="room-tag room-tag--archived"
              >архив</span
            >
            <span class="room-tag">
              {{ room.type === RoomType.Chat ? "чат" : "посты" }}
            </span>
            <span class="room-tag">
              {{
                room.access === RoomAccessType.Private
                  ? "приватная"
                  : "открытая"
              }}
            </span>
          </button>

          <!-- Animated tool reveal (CSS-only .expand-fold, unified tempo,
               both directions). The editor stays mounted — the fold's clip
               row (0fr) hides it visually and `inert` keeps closed copies
               out of the tab order and accessibility tree. -->
          <div
            class="expand-fold"
            :class="{ open: expandedId === roomId(room) }"
          >
            <div class="expand-fold-clip" :inert="expandedId !== roomId(room)">
              <div class="room-editor">
                <FormField label="Название" :name="`room-title-${room.id}`">
                  <input
                    :id="`room-title-${room.id}`"
                    v-model="draft.title"
                    type="text"
                    maxlength="200"
                  />
                </FormField>
                <FormField label="Тип доступа">
                  <Select
                    :model-value="draft.accessType"
                    :options="accessOptions"
                    @update:model-value="
                      (v) => (draft.accessType = v as RoomAccessType)
                    "
                  />
                </FormField>
                <!-- Only a closed room has anybody to hide from: an open one is
                     listed to everybody by its access type alone. -->
                <FormField v-if="draft.accessType === RoomAccessType.Private">
                  <label class="checkbox-label">
                    <input
                      v-model="draft.hiddenWithoutAccess"
                      type="checkbox"
                    />
                    Скрывать комнату от тех, у кого нет доступа
                  </label>
                </FormField>
                <FormField>
                  <label class="checkbox-label">
                    <input v-model="draft.viewPrivateText" type="checkbox" />
                    Приватный текст виден всем
                  </label>
                </FormField>
                <FormField>
                  <label class="checkbox-label">
                    <input v-model="draft.viewDiceResults" type="checkbox" />
                    Результаты бросков видны всем
                  </label>
                </FormField>
                <FormField>
                  <label class="checkbox-label">
                    <input v-model="draft.diceEnabled" type="checkbox" />
                    Броски кубиков включены
                  </label>
                </FormField>

                <!-- Access grants (Private rooms) -->
                <div
                  v-if="draft.accessType === RoomAccessType.Private"
                  class="accesses"
                >
                  <div class="accesses-label">Доступы</div>
                  <ul v-if="room.accesses?.length" class="access-list">
                    <li
                      v-for="a in room.accesses"
                      :key="a.id"
                      class="access-item"
                    >
                      <span>{{ accessLabel(a) }}</span>
                      <span v-if="a.policy" class="access-policy">
                        {{
                          a.policy === RoomAccessPolicy.Full
                            ? "пишет"
                            : "читает"
                        }}
                      </span>
                      <button
                        type="button"
                        class="remove-btn"
                        @click="removeAccess(a)"
                      >
                        Удалить
                      </button>
                    </li>
                  </ul>
                  <SecondaryText v-else>Доступов нет</SecondaryText>

                  <div class="grant-row">
                    <UserAutocomplete
                      v-model="newReader"
                      placeholder="Читатель"
                    />
                    <Button
                      type="button"
                      :disabled="!newReader.trim()"
                      @click="addReader(room)"
                    >
                      Добавить читателя
                    </Button>
                  </div>
                  <div class="grant-row">
                    <Select
                      :model-value="grantCharacterId"
                      :options="characterOptions"
                      placeholder="Персонаж"
                      @update:model-value="
                        (v) => (grantCharacterId = v as string)
                      "
                    />
                    <Select
                      :model-value="grantPolicy"
                      :options="policyOptions"
                      @update:model-value="
                        (v) => (grantPolicy = v as RoomAccessPolicy)
                      "
                    />
                    <Button
                      type="button"
                      :disabled="!grantCharacterId"
                      @click="addCharacterGrant(room)"
                    >
                      Добавить персонажа
                    </Button>
                  </div>
                </div>

                <div class="room-actions">
                  <Button
                    type="button"
                    :loading="savingRoom"
                    @click="saveRoom(room)"
                  >
                    Сохранить
                  </Button>
                  <Button
                    v-if="!room.isArchived"
                    type="button"
                    @click="archive(room)"
                  >
                    В архив
                  </Button>
                  <Button v-else type="button" @click="unarchive(room)">
                    Из архива
                  </Button>
                  <button
                    type="button"
                    class="delete-btn"
                    @click="pendingDelete = room"
                  >
                    Удалить
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>
      </template>
    </div>
    <SecondaryText v-else>Комнат пока нет</SecondaryText>

    <!-- Create room -->
    <div class="create-room">
      <div class="create-label">Новая комната</div>
      <Form
        :valid="create.title.trim().length > 0"
        :loading="creating"
        action="Создать"
        @submit="createRoom"
      >
        <FormField label="Название" name="new-room-title">
          <input
            id="new-room-title"
            v-model="create.title"
            type="text"
            maxlength="200"
          />
        </FormField>
        <FormField label="Тип">
          <Select
            :model-value="create.type"
            :options="typeOptions"
            @update:model-value="(v) => (create.type = v as RoomType)"
          />
        </FormField>
        <FormField label="Тип доступа">
          <Select
            :model-value="create.accessType"
            :options="accessOptions"
            @update:model-value="
              (v) => (create.accessType = v as RoomAccessType)
            "
          />
        </FormField>
        <FormField>
          <label class="checkbox-label">
            <input v-model="create.diceEnabled" type="checkbox" />
            Броски кубиков включены
          </label>
        </FormField>
      </Form>
    </div>

    <ConfirmDialog
      :show="!!pendingDelete"
      title="Удаление комнаты"
      message="Удалить комнату? Действие необратимо."
      confirm-label="Удалить"
      danger
      @confirm="confirmDelete"
      @cancel="pendingDelete = null"
    />
  </section>
</template>

<style scoped lang="sass">
.settings-section
  margin-bottom: $big
  padding: $medium
  background-color: $bg-element
  border-radius: $border-radius

.rooms-list
  display: flex
  flex-direction: column
  gap: $tiny

.room-row
  border: 1px solid $border
  border-radius: $border-radius
  overflow: hidden

.room-head
  display: flex
  align-items: center
  gap: $small
  width: 100%
  padding: $small
  border: none
  background: none
  font: inherit
  text-align: left
  cursor: pointer

  &:hover
    background-color: $bg-element-accent

.room-title
  flex: 1
  color: $text

.room-tag
  font-size: $tertiary-font-size
  color: $text-muted
  text-transform: uppercase

// Archive is a state, not an access descriptor — a subtle chip sets it apart
// from the adjacent "приватная/открытая" tag.
.room-tag--archived
  padding: 1px $tiny
  border-radius: $border-radius
  background-color: $bg-element-accent

.room-editor
  padding: $small $medium $medium
  border-top: 1px solid $border
  background-color: $bg-element-accent

.checkbox-label
  display: flex
  align-items: center
  gap: $small
  cursor: pointer

.accesses
  margin: $small 0
  padding-top: $small
  border-top: 1px solid $border

.accesses-label
  color: $text-muted
  font-size: $secondary-font-size
  margin-bottom: $small

.access-list
  list-style: none
  display: flex
  flex-direction: column
  gap: $tiny
  margin-bottom: $small

.access-item
  display: flex
  align-items: center
  gap: $small

.access-policy
  color: $text-muted
  font-size: $tertiary-font-size

.grant-row
  display: flex
  align-items: flex-start
  gap: $small
  flex-wrap: wrap
  margin-top: $small

.room-actions
  display: flex
  align-items: center
  gap: $small
  margin-top: $medium

.remove-btn,
.delete-btn
  padding: 0
  border: none
  background: none
  font: inherit
  font-size: $secondary-font-size
  color: $accent-red
  cursor: pointer

  &:hover
    text-decoration: underline

.create-room
  margin-top: $big
  padding-top: $medium
  border-top: 1px solid $border

.create-label
  color: $text-muted
  font-size: $secondary-font-size
  margin-bottom: $small
</style>
