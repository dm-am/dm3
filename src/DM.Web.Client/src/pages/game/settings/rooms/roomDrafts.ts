/**
 * The drafts of the table of rooms until "Сохранить", and the requests that
 * send each room what differs: the room, the grants taken off, the grants
 * added, then the moves "Порядок" asks for.
 */
import { nextTick, reactive, watch, type Ref } from "vue";
import {
  roomApi,
  RoomType,
  RoomAccessType,
  RoomAccessPolicy,
  type Character,
  type Room,
  type RoomAccess,
  type RoomAccessGrant,
} from "@/entities/game";
import type { GeneralError } from "@/shared/api/models/common";

export function roomId(room: Room): string {
  return room.id;
}

// A person let in, or a character let in to read.
interface GrantLine {
  // None for a grant added here.
  accessId?: string;
  characterId?: string;
  username?: string;
  name: string;
  policy: RoomAccessPolicy;
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
  // The characters ticked in "Игроки".
  writers: string[];
  lines: GrantLine[];
  order: string;
  // The place the room stood at when "Порядок" was filled.
  orderShown: string;
}

type RoomDrafts = Record<string, RoomDraft>;

const characterIdOf = (access: RoomAccess): string | undefined =>
  access.character?.id;

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
    writers: accesses.flatMap((a) => {
      const id = characterIdOf(a);
      return id && a.policy === RoomAccessPolicy.Full ? [id] : [];
    }),
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

// The drafts of the other rooms stay as typed when a room comes or goes.
function syncDrafts(drafts: RoomDrafts, list: Room[]) {
  const present = new Set(list.map(roomId));
  for (const id of Object.keys(drafts)) {
    if (!present.has(id)) delete drafts[id];
  }
  list.forEach((room, index) => {
    const draft = drafts[roomId(room)];
    if (!draft) {
      drafts[roomId(room)] = draftFrom(room, index + 1);
    } else if (draft.order === draft.orderShown) {
      // A place nobody typed over follows the room.
      draft.order = draft.orderShown = String(index + 1);
    }
  });
}

function rebuildDrafts(drafts: RoomDrafts, list: Room[]) {
  for (const id of Object.keys(drafts)) delete drafts[id];
  syncDrafts(drafts, list);
}

function askedPlace(draft: RoomDraft): number | null {
  const typed = draft.order.trim();
  if (typed === draft.orderShown) return null;
  const place = Number(typed);
  return Number.isInteger(place) && place >= 1 ? place : null;
}

/**
 * The rooms nobody renumbered keep their order among themselves; the others go
 * in at their places, first to last, a place past the end being the end.
 */
function orderAsked(
  list: Room[],
  draftOf: (room: Room) => RoomDraft,
): { order: Room[]; renumbered: Room[] } {
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
 * Moves of the server's "after that room" (previousRoomId). The renumbered
 * rooms alone give the order; the pass over every room after them finds
 * nothing to move.
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

// Every character in play, and any other with a grant, so it can be taken off.
function writerOptions(
  room: Room,
  characters: Character[],
): { id: string; name: string }[] {
  const options = new Map<string, string>();
  for (const character of characters) {
    if (character.status === "Active")
      options.set(character.id, character.name);
  }
  for (const access of room.accesses ?? []) {
    const id = characterIdOf(access);
    if (id && access.policy === RoomAccessPolicy.Full && !options.has(id))
      options.set(id, access.character?.name ?? "");
  }
  return [...options].map(([id, name]) => ({ id, name }));
}

// Posts are written as a character (RoomIntentionResolver.SendMessage).
function writesInRoom(room: Room, line: GrantLine): boolean {
  return (
    !!line.username &&
    room.type === RoomType.Chat &&
    line.policy === RoomAccessPolicy.Full
  );
}

function writerLines(room: Room, draft: RoomDraft): GrantLine[] {
  return draft.lines.filter((line) => writesInRoom(room, line));
}

// A character ticked in "Игроки" is not a reader as well.
function readerLines(room: Room, draft: RoomDraft): GrantLine[] {
  return draft.lines.filter(
    (line) =>
      !writesInRoom(room, line) &&
      (!line.characterId || !draft.writers.includes(line.characterId)),
  );
}

// Null where a grant by name gives nothing: an open room of posts.
function personPolicy(room: Room, draft: RoomDraft): RoomAccessPolicy | null {
  if (draft.access === RoomAccessType.Private) return RoomAccessPolicy.ReadOnly;
  return room.type === RoomType.Chat ? RoomAccessPolicy.Full : null;
}

function personNames(draft: RoomDraft): string[] {
  return draft.lines
    .map((line) => line.username)
    .filter((name): name is string => !!name);
}

function takePerson(
  draft: RoomDraft,
  username: string,
  policy: RoomAccessPolicy,
) {
  const taken = draft.lines.find(
    (line) => line.username?.toLowerCase() === username.toLowerCase(),
  );
  if (taken) taken.kept = true;
  else draft.lines.push({ username, name: username, policy, kept: true });
}

function roomPatch(room: Room, draft: RoomDraft): Partial<Room> | null {
  const settings = {
    viewPrivateText: draft.viewPrivateText,
    viewDiceResults: draft.viewDiceResults,
    diceEnabled: draft.diceEnabled,
    hiddenWithoutAccess: draft.hiddenWithoutAccess,
  };
  const title = draft.title;
  const settingsChanged = (
    Object.keys(settings) as (keyof typeof settings)[]
  ).some((key) => (room.settings?.[key] ?? false) !== settings[key]);
  const changed =
    (title.trim() !== "" && title !== room.title) ||
    draft.access !== (room.access ?? RoomAccessType.Open) ||
    draft.archived !== !!room.isArchived ||
    settingsChanged;
  if (!changed) return null;
  return {
    // An emptied name is not sent: the room keeps the one it has.
    ...(title.trim() ? { title } : {}),
    access: draft.access,
    isArchived: draft.archived,
    // The block is written whole: a setting left out of it would be cleared.
    settings,
  };
}

function keeps(draft: RoomDraft, access: RoomAccess): boolean {
  const characterId = characterIdOf(access);
  if (characterId && access.policy === RoomAccessPolicy.Full)
    return draft.writers.includes(characterId);
  if (characterId && draft.writers.includes(characterId)) return false;
  return draft.lines.some((line) => line.accessId === access.id && line.kept);
}

function additions(room: Room, draft: RoomDraft): RoomAccessGrant[] {
  const writing = new Set(
    (room.accesses ?? [])
      .filter((a) => a.policy === RoomAccessPolicy.Full)
      .map(characterIdOf)
      .filter(Boolean),
  );
  const made: RoomAccessGrant[] = draft.writers
    .filter((id) => !writing.has(id))
    .map((id) => ({ character: { id }, policy: RoomAccessPolicy.Full }));
  for (const line of draft.lines) {
    if (line.accessId || !line.kept || !line.username) continue;
    made.push({
      user: { username: line.username },
      // The server reads a missing policy as no access (RoomMapper).
      policy: line.policy,
    });
  }
  return made;
}

// The first refusal stops it and is answered with its room.
async function sendDrafts(
  list: Room[],
  drafts: RoomDrafts,
  draftOf: (room: Room) => RoomDraft,
): Promise<{ room: Room; error: GeneralError } | null> {
  for (const room of list) {
    const draft = drafts[roomId(room)];
    if (!draft) continue;
    const patch = roomPatch(room, draft);
    if (patch) {
      const { error } = await roomApi.updateRoom(roomId(room), patch);
      if (error) return { room, error };
    }
    // Taken off first: a reader made a writer loses the grant to read first.
    for (const access of room.accesses ?? []) {
      if (keeps(draft, access)) continue;
      const { error } = await roomApi.deleteRoomAccess(access.id);
      if (error) return { room, error };
    }
    for (const grant of additions(room, draft)) {
      const { error } = await roomApi.createRoomAccess(roomId(room), grant);
      if (error) return { room, error };
    }
  }
  for (const { room, after } of movesTo(list, orderAsked(list, draftOf))) {
    const { error } = await roomApi.updateRoom(roomId(room), {
      previousRoomId: after,
    });
    if (error) return { room, error };
  }
  return null;
}

export function useRoomDrafts(
  rooms: Ref<Room[]>,
  characters: Ref<Character[]>,
) {
  const drafts = reactive<RoomDrafts>({});
  watch(rooms, (list) => syncDrafts(drafts, list), { immediate: true });

  function draftOf(room: Room): RoomDraft {
    return (
      drafts[roomId(room)] ?? draftFrom(room, rooms.value.indexOf(room) + 1)
    );
  }

  const offersWriter = (room: Room) =>
    personPolicy(room, draftOf(room)) === RoomAccessPolicy.Full;
  const offersReader = (room: Room) =>
    personPolicy(room, draftOf(room)) === RoomAccessPolicy.ReadOnly;

  const personQuery = reactive<Record<string, string>>({});

  async function addPerson(room: Room, username: string) {
    const id = roomId(room);
    personQuery[id] = username;
    const policy = personPolicy(room, draftOf(room));
    if (!username || !policy) return;
    takePerson(draftOf(room), username, policy);
    // Emptied in the same turn, the field would keep the name on display.
    await nextTick();
    personQuery[id] = "";
  }

  return {
    drafts,
    draftOf,
    writerOptions: (room: Room) => writerOptions(room, characters.value),
    writerLines: (room: Room) => writerLines(room, draftOf(room)),
    readerLines: (room: Room) => readerLines(room, draftOf(room)),
    offersWriter,
    offersReader,
    hasWriters: (room: Room) =>
      writerOptions(room, characters.value).length > 0 ||
      writerLines(room, draftOf(room)).length > 0 ||
      offersWriter(room),
    personQuery,
    personNames: (room: Room) => personNames(draftOf(room)),
    addPerson,
    rebuildDrafts: () => rebuildDrafts(drafts, rooms.value),
    sendDrafts: () => sendDrafts([...rooms.value], drafts, draftOf),
  };
}
