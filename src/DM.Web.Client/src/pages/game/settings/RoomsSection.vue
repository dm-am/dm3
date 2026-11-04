<script setup lang="ts">
/**
 * The rooms of a game as DM2 manages them (Rooms.aspx): the form of a new room,
 * and one table of every room edited in its cells and sent by one "Сохранить".
 * Creating and deleting a room go at once.
 */
import { computed, onMounted, ref } from "vue";
import { storeToRefs } from "pinia";
import {
  useGameDetailsStore,
  roomApi,
  RoomType,
  RoomAccessType,
  type Room,
} from "@/entities/game";
import { UserAutocomplete } from "@/entities/user";
import { Select } from "@/shared/ui/Select";
import Button from "@/shared/ui/Button/Button.vue";
import { RemoveButton } from "@/shared/ui/RemoveButton";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { SettingsSection } from "@/shared/ui/SettingsSection";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { useSavedState } from "@/shared/lib/composables/useSavedState";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
import { ErrorState } from "@/shared/ui/ErrorState";
import NewRoomForm from "./rooms/NewRoomForm.vue";
import { roomId, useRoomDrafts } from "./rooms/roomDrafts";
import { ROOM_TITLE_MAX_LENGTH } from "@/shared/lib/constants/game";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

const store = useGameDetailsStore();
const { game, rooms, roomsError, characters } = storeToRefs(store);

const accessOptions = [
  { value: RoomAccessType.Open, label: "Открытая" },
  { value: RoomAccessType.Private, label: "Закрытая" },
];
const ARCHIVED = "archived";
const ACTIVE = "active";
const activityOptions = [
  { value: ARCHIVED, label: "Архивная" },
  { value: ACTIVE, label: "Активная" },
];

// An empty list before the answer is no news: the table waits for one.
const answered = ref(rooms.value.length > 0);

onMounted(async () => {
  if (!game.value) return;
  if (!characters.value.length) store.loadCharacters(game.value.publicId);
  if (!rooms.value.length) await store.loadRooms(game.value.publicId);
  answered.value = true;
});

async function reloadRooms() {
  if (game.value) await store.loadRooms(game.value.publicId);
}

const {
  drafts,
  draftOf,
  writerOptions,
  writerLines,
  readerLines,
  offersWriter,
  offersReader,
  hasWriters,
  personQuery,
  personNames,
  addPerson,
  rebuildDrafts,
  sendDrafts,
} = useRoomDrafts(rooms, characters);

const saving = ref(false);
const saveResult = useActionResult();

const { saved, label: saveLabel, remember } = useSavedState(() => drafts);

// After a refusal the list is read again, so a second press sends the rest.
async function saveAll() {
  if (saving.value || saved.value) return;
  saving.value = true;
  saveResult.clear();
  const failed = await sendDrafts();
  if (failed)
    saveResult.refuse(
      failed.error,
      `Не удалось сохранить комнату "${failed.room.title}"`,
    );
  await reloadRooms();
  if (!failed) {
    rebuildDrafts();
    remember();
  }
  saving.value = false;
}

const pendingDelete = ref<Room | null>(null);

// The window prints its own refusal, so the interceptor keeps quiet.
async function confirmDelete() {
  const room = pendingDelete.value;
  if (!room) return;
  const result = await roomApi.deleteRoom(roomId(room), OWNS_REFUSAL);
  if (!result.error) void reloadRooms();
  return result;
}

const typeLabel = (room: Room) =>
  room.type === RoomType.Chat ? "Чат" : "Посты";

const hasRooms = computed(() => rooms.value.length > 0);
</script>

<template>
  <SettingsSection>
    <BlockTitle>Создать новую комнату</BlockTitle>
    <NewRoomForm />

    <BlockTitle>Настройки комнат</BlockTitle>
    <!-- DM2's ten columns do not fit side by side: the lists go to a second
         row, and the number spans both. -->
    <table class="rooms-table">
      <thead>
        <tr>
          <th scope="col">#</th>
          <th scope="col">Название</th>
          <th scope="col">Тип</th>
          <th scope="col">Доступ</th>
          <th scope="col">Порядок</th>
          <th scope="col">Сообщений</th>
          <th scope="col">Действия</th>
        </tr>
      </thead>
      <tbody v-if="!answered" class="rooms-waiting" aria-busy="true">
        <tr v-for="i in 2" :key="i">
          <td colspan="7"><div class="row-skeleton" /></td>
        </tr>
      </tbody>
      <tbody v-else-if="roomsError">
        <tr>
          <td colspan="7">
            <ErrorState :message="roomsError" :retry="reloadRooms" />
          </td>
        </tr>
      </tbody>
      <tbody v-else-if="!hasRooms">
        <tr>
          <td colspan="7" class="rooms-empty">
            <SecondaryText>Комнат пока нет</SecondaryText>
          </td>
        </tr>
      </tbody>
      <tbody
        v-for="room in rooms"
        :key="room.id"
        class="room"
        :data-room="room.roomNumber"
      >
        <tr>
          <td rowspan="2" class="col-number">{{ room.roomNumber }}</td>
          <td class="col-title">
            <input
              v-model="draftOf(room).title"
              type="text"
              :maxlength="ROOM_TITLE_MAX_LENGTH"
              :aria-label="`Название комнаты ${room.roomNumber}`"
            />
          </td>
          <td class="col-type">{{ typeLabel(room) }}</td>
          <td class="col-access">
            <Select
              :model-value="draftOf(room).access"
              :options="accessOptions"
              :aria-label="`Доступ комнаты ${room.roomNumber}`"
              @update:model-value="
                (v) => (draftOf(room).access = v as RoomAccessType)
              "
            />
          </td>
          <td class="col-order">
            <input
              v-model="draftOf(room).order"
              type="text"
              inputmode="numeric"
              maxlength="3"
              :aria-label="`Порядок комнаты ${room.roomNumber}`"
            />
          </td>
          <td class="col-messages">{{ room.totalPostsCount ?? 0 }}</td>
          <td class="col-actions">
            <RemoveButton @click="pendingDelete = room">Удалить</RemoveButton>
          </td>
        </tr>
        <tr>
          <td colspan="6" class="col-details">
            <div class="grants">
              <template v-if="hasWriters(room)">
                <span class="grants-caption">Игроки:</span>
                <div class="grants-list col-writers">
                  <label
                    v-for="option in writerOptions(room)"
                    :key="option.id"
                    class="choice"
                  >
                    <input
                      v-model="draftOf(room).writers"
                      type="checkbox"
                      :value="option.id"
                    />
                    {{ option.name }}
                  </label>
                  <label
                    v-for="line in writerLines(room)"
                    :key="line.accessId ?? line.username"
                    class="choice"
                  >
                    <input v-model="line.kept" type="checkbox" />
                    {{ line.name }}
                  </label>
                  <UserAutocomplete
                    v-if="offersWriter(room)"
                    class="person-field"
                    :model-value="personQuery[roomId(room)] ?? ''"
                    :exclude="personNames(room)"
                    placeholder="Разрешить писать в чате"
                    @update:model-value="(name) => addPerson(room, name)"
                  />
                </div>
              </template>
              <template v-if="offersReader(room) || readerLines(room).length">
                <span class="grants-caption">Читатели:</span>
                <div class="grants-list col-readers">
                  <label
                    v-for="line in readerLines(room)"
                    :key="line.accessId ?? line.username"
                    class="choice"
                  >
                    <input v-model="line.kept" type="checkbox" />
                    {{ line.name }}
                  </label>
                  <UserAutocomplete
                    v-if="offersReader(room)"
                    class="person-field"
                    :model-value="personQuery[roomId(room)] ?? ''"
                    :exclude="personNames(room)"
                    placeholder="Добавить читателя"
                    @update:model-value="(name) => addPerson(room, name)"
                  />
                </div>
              </template>
              <span class="grants-caption">Активность:</span>
              <div class="grants-list col-activity">
                <Select
                  :model-value="draftOf(room).archived ? ARCHIVED : ACTIVE"
                  :options="activityOptions"
                  :aria-label="`Активность комнаты ${room.roomNumber}`"
                  @update:model-value="
                    (v) => (draftOf(room).archived = v === ARCHIVED)
                  "
                />
              </div>
            </div>
            <div class="room-settings">
              <label class="choice">
                <input v-model="draftOf(room).diceEnabled" type="checkbox" />
                Поддержка кубика
              </label>
              <label class="choice">
                <input
                  v-model="draftOf(room).viewDiceResults"
                  type="checkbox"
                />
                Показывать всем результаты бросков
              </label>
              <label class="choice">
                <input
                  v-model="draftOf(room).viewPrivateText"
                  type="checkbox"
                />
                Показывать всем скрытые блоки
              </label>
              <label
                v-if="draftOf(room).access === RoomAccessType.Private"
                class="choice"
              >
                <input
                  v-model="draftOf(room).hiddenWithoutAccess"
                  type="checkbox"
                />
                Скрывать от тех, у кого нет доступа
              </label>
            </div>
          </td>
        </tr>
      </tbody>
    </table>

    <template v-if="hasRooms">
      <ActionResult :result="saveResult" />
      <Button
        type="button"
        variant="primary"
        class="save-all"
        :loading="saving"
        :disabled="saved"
        @click="saveAll"
      >
        {{ saveLabel }}
      </Button>
    </template>

    <ConfirmDialog
      :show="!!pendingDelete"
      title="Удалить комнату?"
      message="Комната будет удалена без возможности восстановления"
      confirm-label="Удалить"
      failure="Не удалось удалить комнату"
      :action="confirmDelete"
      @update:show="(shown) => !shown && (pendingDelete = null)"
    />
  </SettingsSection>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *
@use "@/assets/styles/FormRows" as *
@use "@/assets/styles/Tables" as *
@use "@/assets/styles/Skeleton" as *

.choice
  +choice

// In the look of DataTable, the cells as tight as DM2's: wider, they would cut
// the names in their fields (rooms-settings-fit.spec.ts).
.rooms-table
  width: 100%
  border-collapse: separate
  border-spacing: $table-gap
  background-color: $border

  th
    text-align: center
    vertical-align: middle
    +table-header
    padding: $table-cell-padding-v $grid-step

  td
    vertical-align: middle
    +table-row
    padding: $table-cell-padding-v $grid-step

  th,
  td
    hyphens: none
    overflow-wrap: normal

.col-number,
.col-type,
.col-order,
.col-messages,
.col-actions
  text-align: center

.col-order input
  width: $grid-step * 10
  box-sizing: border-box

// Squeezed by the name, the list cut "Закрытая" to its first letter.
.col-access,
.col-activity
  :deep(.select-control)
    min-width: $grid-step * 26

.col-activity
  :deep(.select-control)
    width: auto

.col-title
  width: 100%

  input
    width: 100%
    min-width: $grid-step * 25
    box-sizing: border-box

// The captions are as wide as "Активность:" in every room, so the lists of
// all the rooms start at one line.
.grants
  display: grid
  grid-template-columns: minmax(19 * $grid-step, auto) minmax(0, 1fr)
  align-items: baseline
  gap: $small $medium

.grants-caption
  +field-caption

.grants-list
  display: flex
  flex-wrap: wrap
  align-items: center
  gap: $tiny $medium

.person-field
  flex: 0 1 $grid-step * 55
  min-width: $grid-step * 40

.room-settings
  display: flex
  flex-wrap: wrap
  gap: $tiny $medium

.grants + .room-settings
  margin-top: $small

.rooms-empty
  padding: $medium

// A room's line of fields, as tall as its controls.
.row-skeleton
  height: $control-height
  +skeleton-shimmer

.save-all
  margin-top: $medium
</style>
