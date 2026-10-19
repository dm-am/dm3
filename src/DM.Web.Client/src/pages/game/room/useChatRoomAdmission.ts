import { computed, ref, type Ref } from "vue";
import { storeToRefs } from "pinia";
import {
  RoomAccessPolicy,
  RoomAccessType,
  useGameDetailsStore,
  type Room,
  type RoomAccess,
} from "@/entities/game";
import { useAuthStore } from "@/entities/user";

// A character that left the game keeps its rows but writes through none.
function writesThrough(access: RoomAccess, me: string | undefined): boolean {
  if (access.policy !== RoomAccessPolicy.Full) return false;
  if (!me) return false;
  const character = access.character;
  if (character)
    return character.status === "Active" && character.author?.username === me;
  return access.user?.username === me;
}

/**
 * Who reads a chat room of a game and who writes in it, and the refusals the
 * server gives to its reads.
 */
export function useChatRoomAdmission(room: Ref<Room | null>) {
  const { isMaster, isAssistant } = storeToRefs(useGameDetailsStore());
  const { user } = storeToRefs(useAuthStore());

  // The list answers canView false for a closed room the viewer may not enter.
  const roomOpen = computed(() => !!room.value && room.value.canView !== false);

  // A closed room still open to a guest is the list of a session that ran out.
  const guestReads = computed(
    () =>
      !user.value &&
      roomOpen.value &&
      room.value?.access !== RoomAccessType.Private,
  );

  const readsRoom = computed(
    () => roomOpen.value && (!!user.value || guestReads.value),
  );

  // 403 and 404 of the messages: the room went out of reach after the list.
  const serverRefused = ref(false);

  // 401 of the messages: a guest, or a session that ran out.
  const denied = ref(false);

  // A signed-in viewer is turned away by a closed room with a 404 before any
  // rule of the room is asked (RoomAvailable), so a 404 here is a refusal.
  function takeRefusal(status: number | undefined): boolean {
    if (status === 401) {
      denied.value = true;
      return true;
    }
    if (status === 403 || status === 404) {
      serverRefused.value = true;
      return true;
    }
    return false;
  }

  // Each refusal belongs to the read it came with.
  function forgetRefusals() {
    denied.value = false;
    serverRefused.value = false;
  }

  // Openness is about reading; writing is RoomIntentionResolver.SendMessage.
  const mayWrite = computed(
    () =>
      isMaster.value ||
      isAssistant.value ||
      (room.value?.accesses ?? []).some((access) =>
        writesThrough(access, user.value?.username),
      ),
  );

  const invitesSignIn = computed(
    () =>
      roomOpen.value && (denied.value || (!user.value && !guestReads.value)),
  );

  return {
    roomOpen,
    readsRoom,
    mayWrite,
    invitesSignIn,
    serverRefused,
    denied,
    takeRefusal,
    forgetRefusals,
  };
}

export type ChatRoomAdmission = ReturnType<typeof useChatRoomAdmission>;
