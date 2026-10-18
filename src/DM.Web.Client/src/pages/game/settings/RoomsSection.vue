<script setup lang="ts">
/**
 * RoomsSection — the rooms of a game, managed the way the old site manages
 * them (Rooms.aspx): the form of a new room on top, and under it one table of
 * every room, edited in its cells and saved by one button under it.
 *
 * A row is a room: its number, its name, its type, its access, its place in
 * the list ("Порядок"), how many messages it holds ("Сообщений") and its
 * removal. Under the row, in the old site's order of its columns, who writes
 * in it ("Игроки": the characters, and in a chat the people let in to write),
 * who is let in to read it ("Читатели") and its activity, and then the
 * settings of the room the old site keeps for the whole game: dice, the
 * results of the rolls and the private text shown to everybody, and for a
 * closed room whether it is listed to those who may not open it.
 *
 * Nothing in the table is sent until "Сохранить": the cells hold a draft of
 * every room, and the save sends each room what differs between its draft and
 * the room as the server has it, the room itself first, then the grants taken
 * off, then the grants added, and the moves "Порядок" asks for after every
 * room. The button answers as every save of the site
 * does, "Сохранено" and inert while the drafts still are what was sent
 * (useSavedState). A room is created and deleted at once: the first appears in
 * the table, the other goes from it, and the drafts of the rest stay as typed.
 */
import { computed, nextTick, onMounted, reactive, ref, watch } from "vue";
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
import { RemoveButton } from "@/shared/ui/RemoveButton";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { useSavedState } from "@/shared/lib/composables/useSavedState";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";

const store = useGameDetailsStore();
const { game, rooms, characters } = storeToRefs(store);

// The old site's words and its order of the choices.
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

onMounted(() => {
  if (game.value) {
    if (!rooms.value.length)
      store.loadRooms(game.value.publicId ?? game.value.id);
    if (!characters.value.length)
      store.loadCharacters(game.value.publicId ?? game.value.id);
  }
});

function roomId(room: Room): string {
  return room.id as unknown as string;
}

async function reloadRooms() {
  if (game.value) await store.loadRooms(game.value.publicId ?? game.value.id);
}

// --- Create ---------------------------------------------------------------

const create = reactive({
  title: "",
  type: RoomType.Default,
  // Closed, as the old site starts a new room.
  accessType: RoomAccessType.Private,
});
const creating = ref(false);

/** A refused "Создать", over the button of the form of the new room. */
const createResult = useActionResult();

async function createRoom() {
  if (!create.title.trim()) return;
  creating.value = true;
  createResult.clear();
  const payload: CreateRoomInput = {
    title: create.title.trim(),
    type: create.type,
    accessType: create.accessType,
    // The settings of a room are set in its row of the table, as the rest
    // of it is.
    viewPrivateText: false,
    viewDiceResults: false,
    diceEnabled: false,
  };
  const error = await store.createRoom(payload);
  creating.value = false;
  if (error) {
    createResult.refuse(error, "Не удалось создать комнату");
    return;
  }
  // The new row in the table below and the cleared name are the confirmation.
  create.title = "";
}

// --- The drafts of the table ------------------------------------------------

/**
 * A grant listed by its name: a person let in, or a character let in to read.
 * A person stands in "Игроки" where his grant lets him write and in
 * "Читатели" where it lets him read (writesInRoom).
 */
interface GrantLine {
  /** The grant on the server the line stands for; none for one added here. */
  accessId?: string;
  characterId?: string;
  username?: string;
  /** What the line says: the login, or the name of the character. */
  name: string;
  /** The policy the grant has, or is made with. */
  policy: RoomAccessPolicy;
  /** Ticked: the grant stays, or is made, on "Сохранить". */
  kept: boolean;
}

interface RoomDraft {
  title: string;
  access: RoomAccessType;
  archived: boolean;
  diceEnabled: boolean;
  viewDiceResults: boolean;
  viewPrivateText: boolean;
  hiddenWithoutAccess: boolean;
  /** The characters ticked in "Игроки": they write in the room. */
  writers: string[];
  /** Every other grant of the room, a line each. */
  lines: GrantLine[];
  /** What "Порядок" holds: the place of the room in the list, as typed. */
  order: string;
  /** The place the room stood at when the field was filled. */
  orderShown: string;
}

const drafts = reactive<Record<string, RoomDraft>>({});

const characterIdOf = (access: RoomAccess): string | undefined =>
  (access.character?.id as unknown as string | undefined) ?? undefined;

/** The room as the server has it, as a draft; place counts from one. */
function draftFrom(room: Room, place: number): RoomDraft {
  const accesses = room.accesses ?? [];
  return {
    order: String(place),
    orderShown: String(place),
    title: room.title,
    access: room.access ?? RoomAccessType.Open,
    archived: !!room.isArchived,
    diceEnabled: room.settings?.diceEnabled ?? false,
    viewDiceResults: room.settings?.viewDiceResults ?? false,
    viewPrivateText: room.settings?.viewPrivateText ?? false,
    hiddenWithoutAccess: room.settings?.hiddenWithoutAccess ?? false,
    writers: accesses
      .filter((a) => characterIdOf(a) && a.policy === RoomAccessPolicy.Full)
      .map((a) => characterIdOf(a)!),
    lines: accesses
      .filter((a) => !characterIdOf(a) || a.policy !== RoomAccessPolicy.Full)
      .map((a) => ({
        accessId: a.id,
        characterId: characterIdOf(a),
        username: a.user?.username,
        name: a.character?.name ?? a.user?.username ?? "",
        policy: a.policy ?? RoomAccessPolicy.ReadOnly,
        kept: true,
      })),
  };
}

/**
 * A room that came into the list gets its draft, one that left it loses its
 * own, and the drafts of the rest stay as they were typed: a room created or
 * deleted on the way does not take back what the reader changed elsewhere.
 */
function syncDrafts(list: Room[]) {
  const present = new Set(list.map(roomId));
  for (const id of Object.keys(drafts)) {
    if (!present.has(id)) delete drafts[id];
  }
  list.forEach((room, index) => {
    const draft = drafts[roomId(room)];
    if (!draft) {
      drafts[roomId(room)] = draftFrom(room, index + 1);
    } else if (draft.order === draft.orderShown) {
      // A place nobody typed over follows the room: one removed above it
      // moves it up the list.
      draft.order = draft.orderShown = String(index + 1);
    }
  });
}

/** Every draft read again from the server's rooms, after a save. */
function rebuildDrafts(list: Room[]) {
  for (const id of Object.keys(drafts)) delete drafts[id];
  syncDrafts(list);
}

watch(rooms, (list) => syncDrafts(list), { immediate: true });

function draftOf(room: Room): RoomDraft {
  return drafts[roomId(room)] ?? draftFrom(room, rooms.value.indexOf(room) + 1);
}

// --- "Порядок" --------------------------------------------------------------

/**
 * The place a draft asks for its room: the number typed in "Порядок" where it
 * differs from the place the room stands at, and none where it does not or
 * where what is typed is no place at all.
 */
function askedPlace(draft: RoomDraft): number | null {
  const typed = draft.order.trim();
  if (typed === draft.orderShown) return null;
  const place = Number(typed);
  return Number.isInteger(place) && place >= 1 ? place : null;
}

/**
 * The rooms in the order the drafts ask for, and which of them were given a
 * number. A number typed in "Порядок" puts its room at that place: the rooms
 * nobody renumbered keep their order among themselves, and the renumbered
 * ones are put in at their places from the first place to the last, a place
 * past the end being the end.
 */
function orderAsked(list: Room[]): { order: Room[]; renumbered: Room[] } {
  const asked = list
    .map((room, index) => ({ room, index, place: askedPlace(draftOf(room)) }))
    .filter(
      (entry): entry is { room: Room; index: number; place: number } =>
        entry.place !== null,
    )
    .sort((a, b) => a.place - b.place || a.index - b.index);
  const renumbered = asked.map((entry) => entry.room);
  const order = list.filter((room) => !renumbered.includes(room));
  for (const { room, place } of asked)
    order.splice(Math.min(place - 1, order.length), 0, room);
  return { order, renumbered };
}

/**
 * The moves that turn the list into the order asked, each the server's "put
 * this room right after that one" (previousRoomId, null for the first place),
 * sent one after another. The renumbered rooms go first, each after the room
 * that is to stand before it: that alone gives the order asked, one move a
 * room. The pass over every room after them is the proof of it and finds
 * nothing to move; a room already behind its neighbour is never sent.
 */
function movesTo(
  list: Room[],
  { order, renumbered }: { order: Room[]; renumbered: Room[] },
): { room: Room; after: string | null }[] {
  const current = list.map(roomId);
  const moves: { room: Room; after: string | null }[] = [];
  const settle = (room: Room) => {
    const id = roomId(room);
    const place = order.indexOf(room);
    const after = place === 0 ? null : roomId(order[place - 1]);
    const at = current.indexOf(id);
    if ((at === 0 ? null : current[at - 1]) === after) return;
    current.splice(at, 1);
    current.splice(after === null ? 0 : current.indexOf(after) + 1, 0, id);
    moves.push({ room, after });
  };
  order.filter((room) => renumbered.includes(room)).forEach(settle);
  order.forEach(settle);
  return moves;
}

// --- "Игроки" ---------------------------------------------------------------

/**
 * The characters a room can let write: every character in play, and any other
 * that holds a grant in the room already, so it can be taken off. The old
 * site's list of the column, every choice in sight and the ones made ticked.
 */
function writerOptions(room: Room): { id: string; name: string }[] {
  const options = new Map<string, string>();
  for (const character of characters.value) {
    if (character.status === "Active")
      options.set(character.id as unknown as string, character.name);
  }
  for (const access of room.accesses ?? []) {
    const id = characterIdOf(access);
    if (id && access.policy === RoomAccessPolicy.Full && !options.has(id))
      options.set(id, access.character?.name ?? "");
  }
  return [...options].map(([id, name]) => ({ id, name }));
}

function toggleWriter(room: Room, characterId: string, ticked: boolean) {
  const draft = draftOf(room);
  draft.writers = ticked
    ? [...draft.writers, characterId]
    : draft.writers.filter((id) => id !== characterId);
}

// --- People let in by name --------------------------------------------------

/**
 * A person whose grant lets him write: in a room of chat, by the policy "Full"
 * (RoomIntentionResolver.SendMessage). A room of posts is written in as a
 * character, so a person let into it by name reads it, whatever the policy.
 */
function writesInRoom(room: Room, line: GrantLine): boolean {
  return (
    !!line.username &&
    room.type === RoomType.Chat &&
    line.policy === RoomAccessPolicy.Full
  );
}

/** The people "Игроки" lists after the characters: they write in the chat. */
function writerLines(room: Room): GrantLine[] {
  return draftOf(room).lines.filter((line) => writesInRoom(room, line));
}

/**
 * The lines of "Читатели": the people let in to read, and a character let in
 * only to read. A character ticked in "Игроки" writes, and is not a reader as
 * well.
 */
function readerLines(room: Room): GrantLine[] {
  const draft = draftOf(room);
  return draft.lines.filter(
    (line) =>
      !writesInRoom(room, line) &&
      (!line.characterId || !draft.writers.includes(line.characterId)),
  );
}

/**
 * The policy a person let in by name is given, or null where the room has no
 * field for him. A closed room is read by him. An open room of chat is read by
 * everybody already, and there the grant lets him write. In an open room of
 * posts it would give nothing: a post is written as a character, and reading
 * is everybody's.
 */
function personPolicy(room: Room): RoomAccessPolicy | null {
  if (draftOf(room).access === RoomAccessType.Private)
    return RoomAccessPolicy.ReadOnly;
  return room.type === RoomType.Chat ? RoomAccessPolicy.Full : null;
}

/** The row of the field of a new person: the one his grant puts him in. */
const offersWriter = (room: Room) =>
  personPolicy(room) === RoomAccessPolicy.Full;
const offersReader = (room: Room) =>
  personPolicy(room) === RoomAccessPolicy.ReadOnly;

/** Whether a room has a row "Игроки" to show: a choice or a field in it. */
function hasWriters(room: Room): boolean {
  return (
    writerOptions(room).length > 0 ||
    writerLines(room).length > 0 ||
    offersWriter(room)
  );
}

/** What the field of a new person holds, per room. */
const personQuery = reactive<Record<string, string>>({});

/** The names a room has let in already, kept out of its field's list. */
function personNames(room: Room): string[] {
  return draftOf(room)
    .lines.map((line) => line.username)
    .filter((name): name is string => !!name);
}

async function addPerson(room: Room, username: string) {
  const id = roomId(room);
  personQuery[id] = username;
  const policy = personPolicy(room);
  if (!username || !policy) return;
  const draft = draftOf(room);
  const taken = draft.lines.find(
    (line) => line.username?.toLowerCase() === username.toLowerCase(),
  );
  if (taken) taken.kept = true;
  else draft.lines.push({ username, name: username, policy, kept: true });
  // The field empties once it has shown the name it took: emptied in the same
  // turn, it would never have held it, and would keep the name on display.
  await nextTick();
  personQuery[id] = "";
}

// --- Save -------------------------------------------------------------------

const saving = ref(false);
const saveResult = useActionResult();

const { saved, label: saveLabel, remember } = useSavedState(() => drafts);

/** The body of the room itself, or null where nothing in it changed. */
function roomPatch(room: Room, draft: RoomDraft): Partial<Room> | null {
  const settings = {
    viewPrivateText: draft.viewPrivateText,
    viewDiceResults: draft.viewDiceResults,
    diceEnabled: draft.diceEnabled,
    hiddenWithoutAccess: draft.hiddenWithoutAccess,
  };
  const title = draft.title.trim();
  const settingsChanged = (
    Object.keys(settings) as (keyof typeof settings)[]
  ).some((key) => (room.settings?.[key] ?? false) !== settings[key]);
  const changed =
    (title !== "" && title !== room.title) ||
    draft.access !== (room.access ?? RoomAccessType.Open) ||
    draft.archived !== !!room.isArchived ||
    settingsChanged;
  if (!changed) return null;
  return {
    // An emptied name is not sent: the room keeps the one it has.
    ...(title ? { title } : {}),
    access: draft.access,
    isArchived: draft.archived,
    // The block is written whole: a setting left out of it would be cleared.
    settings,
  };
}

/** Whether a grant the server has stays on "Сохранить". */
function keeps(draft: RoomDraft, access: RoomAccess): boolean {
  const characterId = characterIdOf(access);
  if (characterId && access.policy === RoomAccessPolicy.Full)
    return draft.writers.includes(characterId);
  if (characterId && draft.writers.includes(characterId)) return false;
  return draft.lines.some((line) => line.accessId === access.id && line.kept);
}

/** The grants "Сохранить" makes in a room. */
function additions(room: Room, draft: RoomDraft): Partial<RoomAccess>[] {
  const writing = new Set(
    (room.accesses ?? [])
      .filter((a) => a.policy === RoomAccessPolicy.Full)
      .map(characterIdOf)
      .filter(Boolean),
  );
  const made: Partial<RoomAccess>[] = draft.writers
    .filter((id) => !writing.has(id))
    .map(
      (id) =>
        ({
          character: { id },
          policy: RoomAccessPolicy.Full,
        }) as unknown as Partial<RoomAccess>,
    );
  for (const line of draft.lines) {
    if (line.accessId || !line.kept || !line.username) continue;
    made.push({
      user: { username: line.username },
      // Sent every time: the server reads a missing policy as no access at
      // all (RoomMapper.ToCreateRoomAccess). The one of the row the person
      // was added in, which is the one the row says he has (personPolicy).
      policy: line.policy,
    } as unknown as Partial<RoomAccess>);
  }
  return made;
}

/**
 * Sends every room what differs in its draft, in the order of the table. The
 * first refusal stops the save and is said under the table, naming the room;
 * the list is then read again, and what went through already is no longer a
 * difference, so a second press sends only the rest.
 */
async function saveAll() {
  if (saving.value || saved.value) return;
  saving.value = true;
  saveResult.clear();
  const failed = await sendDrafts();
  if (failed) {
    saveResult.refuse(
      failed.error,
      `Не удалось сохранить комнату "${failed.room.title}"`,
    );
    await reloadRooms();
    saving.value = false;
    return;
  }
  await reloadRooms();
  rebuildDrafts(rooms.value);
  remember();
  saving.value = false;
}

async function sendDrafts() {
  const list = [...rooms.value];
  for (const room of list) {
    const draft = drafts[roomId(room)];
    if (!draft) continue;
    const patch = roomPatch(room, draft);
    if (patch) {
      const { error } = await gameApi.updateRoom(roomId(room), patch);
      if (error) return { room, error };
    }
    // Taken off before anything is added: a character moved from "Читатели"
    // to "Игроки" loses the grant to read before it gets the one to write.
    for (const access of room.accesses ?? []) {
      if (keeps(draft, access)) continue;
      const { error } = await gameApi.deleteRoomAccess(access.id);
      if (error) return { room, error };
    }
    for (const grant of additions(room, draft)) {
      const { error } = await gameApi.createRoomAccess(roomId(room), grant);
      if (error) return { room, error };
    }
  }
  for (const { room, after } of movesTo(list, orderAsked(list))) {
    const { error } = await gameApi.updateRoom(roomId(room), {
      previousRoomId: after,
    });
    if (error) return { room, error };
  }
  return null;
}

// --- Delete -----------------------------------------------------------------

const pendingDelete = ref<Room | null>(null);

// The request of the confirmation. The window waits for it, prints a refusal in
// itself and closes on success alone, so the interceptor keeps quiet about it.
async function confirmDelete() {
  const room = pendingDelete.value;
  if (!room) return;
  const result = await gameApi.deleteRoom(roomId(room), {
    ownsRefusal: true,
    ownsRateLimit: true,
  });
  if (result.error) return result;
  void reloadRooms();
  return result;
}

const typeLabel = (room: Room) =>
  room.type === RoomType.Chat ? "Чат" : "Посты";

const hasRooms = computed(() => rooms.value.length > 0);
</script>

<template>
  <section class="settings-section">
    <BlockTitle>Создать новую комнату</BlockTitle>
    <Form
      :valid="create.title.trim().length > 0"
      :loading="creating"
      action="Создать"
      :refusal="createResult.refusal.value"
      @submit="createRoom"
    >
      <FormField class="row" label="Название:" name="new-room-title">
        <input
          id="new-room-title"
          v-model="create.title"
          type="text"
          maxlength="200"
        />
      </FormField>
      <FormField class="row" label="Тип:">
        <div class="choices">
          <label class="choice">
            <input
              v-model="create.type"
              type="radio"
              name="new-room-type"
              :value="RoomType.Default"
            />
            Посты
          </label>
          <label class="choice">
            <input
              v-model="create.type"
              type="radio"
              name="new-room-type"
              :value="RoomType.Chat"
            />
            Чат
          </label>
        </div>
      </FormField>
      <FormField class="row" label="Доступ:">
        <div class="choices">
          <label class="choice">
            <input
              v-model="create.accessType"
              type="radio"
              name="new-room-access"
              :value="RoomAccessType.Private"
            />
            Закрытая
          </label>
          <label class="choice">
            <input
              v-model="create.accessType"
              type="radio"
              name="new-room-access"
              :value="RoomAccessType.Open"
            />
            Открытая
          </label>
        </div>
      </FormField>
    </Form>

    <BlockTitle>Настройки комнат</BlockTitle>
    <!-- The old site's table, its columns of one value in one row and, under
         them, the lists of a room, its activity and its settings: the ten
         columns of the old site with the fields and lists of this one do not
         stand side by side in the column of the page, and the first row holds
         what does. The number spans both rows, which holds them together. -->
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
      <tbody v-if="!hasRooms">
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
              maxlength="200"
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
            <!-- Who writes and who reads: the characters, and after them the
                 people let in by name, each in the row his grant puts him
                 in. The field of a new person stands in the row of what it
                 gives him: reading a closed room, writing in an open chat. -->
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
                      type="checkbox"
                      :checked="draftOf(room).writers.includes(option.id)"
                      @change="
                        toggleWriter(
                          room,
                          option.id,
                          ($event.target as HTMLInputElement).checked,
                        )
                      "
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
                Показывать всем приватные сообщения
              </label>
              <!-- Only a closed room has anybody to hide from: an open one is
                 listed to everybody by its access alone. -->
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
  </section>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *
@use "@/assets/styles/FormRows" as *
@use "@/assets/styles/Tables" as *

.settings-section
  margin-bottom: $big
  padding: $medium
  background-color: $bg-element

// The form of a new room: the old site's rows, the caption on the left and
// the field beside it, as the other forms of a game are laid out. The row is
// the root of a FormField, which carries the scope of this section, so the
// selector needs no :deep of its own: the mixin's own :deep reaches the
// caption inside it.
.settings-section .row
  +form-row

// The choices of a row in one line, the old site's "(•) Закрытая ( ) Открытая".
.choices
  display: flex
  flex-wrap: wrap
  align-items: center
  gap: $small $medium
  min-height: $control-height

// A box or a dot and its words: the box at the first line of the words, and
// the words that wrap go on under themselves and not under the box. Its step
// from the top is the one centring gives it in one line, (line - box) / 2 with
// the line of the page (1.3, Reset.sass).
.choice
  display: flex
  align-items: flex-start
  gap: $small
  cursor: pointer

  input
    flex: none
    margin: calc((1.3em - #{$checkbox-size}) / 2) 0 0

// The table of the rooms in the look of the site's tables (DataTable): cells
// a pixel apart on the colour of a frame, the header on the accent of an
// element and the rows on an element. The cells stand a step in from their
// sides, as tight as the old site's: the columns keep the old site's widths,
// and the field of the name gets what they leave, as wide as its field.
.rooms-table
  width: 100%
  border-collapse: separate
  border-spacing: $table-gap
  background-color: $border

  th
    padding: $table-cell-padding-v $grid-step
    background-color: $bg-element-accent
    color: $text
    font-weight: bold
    text-align: center
    vertical-align: middle

  td
    padding: $table-cell-padding-v $grid-step
    background-color: $bg-element
    vertical-align: middle

  // Text in the table breaks between words only, a long name included.
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

// The place of a room, as narrow as the old site's field: three digits.
.col-order input
  width: $grid-step * 10
  box-sizing: border-box

// A list keeps the width of its longest choice: squeezed by the name, it cut
// "Закрытая" down to its first letter.
.col-access,
.col-activity
  :deep(.select-control)
    min-width: $grid-step * 26

// The activity under the lists keeps the width of its list, as it had in its
// column, and does not run across the row.
.col-activity
  :deep(.select-control)
    width: auto

// The name takes what the other columns leave it.
.col-title
  width: 100%

  input
    width: 100%
    min-width: $grid-step * 25
    box-sizing: border-box

// The lists of a room under its cells: the caption on the left, and its
// names in lines after it, wrapping as words do. The column of the captions
// is as wide in every room as the longest of them, "Активность:" (73 at the
// caption's size), so the lists of all the rooms start at one line of the
// table whatever captions a room has.
.grants
  display: grid
  grid-template-columns: minmax(19 * $grid-step, auto) minmax(0, 1fr)
  align-items: baseline
  gap: $small $medium

// A caption of a field of the page: +field-caption, as FormField draws it.
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

// The settings of a room under its lists, in one line where they fit.
.room-settings
  display: flex
  flex-wrap: wrap
  gap: $tiny $medium

.grants + .room-settings
  margin-top: $small

.rooms-empty
  padding: $medium

.save-all
  margin-top: $medium
</style>
