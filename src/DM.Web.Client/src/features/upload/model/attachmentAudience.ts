import type { PostAttachment, RoomAccess } from "@/entities/game";
import type { UploadAudience } from "@/shared/api/models/common/upload";
import { privateRecipientsLine } from "@/shared/lib/bbcode/bbcodePrivateRecipients";

/** A character the author can show a file to: "Вася" by its checkbox. */
export interface AudienceCandidate {
  id: string;
  name: string;
}

/** A file chosen in a form and sent with the post or the edit, with who sees it. */
export interface PendingAttachment {
  file: File;
  audience: UploadAudience;
}

/** The default of a new file: every reader of the post, as files have always been. */
export function everyReader(): UploadAudience {
  return { visibleTo: "All", characterIds: [] };
}

/**
 * The characters of the room a file can be shown to: the players' ones let in,
 * not the author's own and not the NPCs, as a hidden block names them. The
 * server takes any character let in; the leads see every file anyway.
 */
export function audienceCandidates(
  accesses: readonly RoomAccess[] | undefined,
  author: string | undefined,
): AudienceCandidate[] {
  const seen = new Set<string>();
  const candidates: AudienceCandidate[] = [];
  for (const access of accesses ?? []) {
    const character = access.character;
    if (!character || character.isNpc || seen.has(character.id)) continue;
    if (author && character.author?.username === author) continue;
    seen.add(character.id);
    candidates.push({ id: character.id, name: character.name });
  }
  return candidates;
}

/** The mark of a file as the server answered it: the characters chosen, or every reader. */
export function audienceOf(
  attachment: Pick<PostAttachment, "addressees">,
): UploadAudience {
  const chosen = attachment.addressees;
  return chosen
    ? { visibleTo: "Selected", characterIds: chosen.map((c) => c.id) }
    : everyReader();
}

/**
 * The boxes of an attached file: the room's characters, then those its mark
 * names who have left the room, by the name the mark keeps. Only the file's
 * own mark brings one back: unchecked, he is no character of the room to
 * choose again.
 */
export function attachmentCandidates(
  candidates: readonly AudienceCandidate[],
  attachment: Pick<PostAttachment, "addressees">,
): AudienceCandidate[] {
  const inRoom = new Set(candidates.map((c) => c.id));
  const gone = (attachment.addressees ?? []).filter((c) => !inRoom.has(c.id));
  return [...candidates, ...gone];
}

/** The names of the checked ones, in the order of the boxes. */
function chosenNames(
  candidates: readonly AudienceCandidate[],
  characterIds: readonly string[],
): string[] {
  const chosen = new Set(characterIds);
  return candidates.filter((c) => chosen.has(c.id)).map((c) => c.name);
}

/** "DungeonMaster, Assistant, Вася" for the checked ones, in the order of the boxes. */
export function audienceRecipients(
  candidates: readonly AudienceCandidate[],
  characterIds: readonly string[],
): string {
  return privateRecipientsLine(chosenNames(candidates, characterIds));
}

/**
 * The word of a file's line that opens its choice: "видят все", or "видят:
 * Вася, Петя". A mark naming no character is the author and the leads, as a
 * bare hidden block: "видят: DungeonMaster, Assistant".
 */
export function audienceLabel(
  candidates: readonly AudienceCandidate[],
  audience: UploadAudience,
): string {
  if (audience.visibleTo === "All") return "видят все";
  const names = chosenNames(candidates, audience.characterIds);
  return `видят: ${names.length ? names.join(", ") : privateRecipientsLine([])}`;
}
