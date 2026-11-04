/**
 * The draft: the text the editor keeps in localStorage until it is sent, under
 * the key of its subject (draftKey), for a week. The status line says whether
 * there is one to load and whether the last keystroke was kept.
 */
import { computed, onMounted, ref, watch } from "vue";
import { pluralize } from "@/shared/lib/utils/pluralize";
import type { EditorTarget } from "./sourceField";

/** How many weeks a draft is kept: long enough to come back to a post, short enough not to pile up. */
const DRAFT_WEEKS = 1;
const DRAFT_EXPIRATION_MS = DRAFT_WEEKS * 7 * 24 * 60 * 60 * 1000;

/** How long a draft is kept, as the help says it after "хранит": "неделю", "2 недели". */
export const DRAFT_KEPT_FOR =
  DRAFT_WEEKS === 1
    ? "неделю"
    : `${DRAFT_WEEKS} ${pluralize(DRAFT_WEEKS, "неделю", "недели", "недель")}`;
const DRAFT_PREFIX = "bbcode_draft_";

interface DraftData {
  content: string;
  timestamp: number;
}

/**
 * The draft as the status line shows it: none, one to load, being kept, or
 * kept.
 */
export type DraftState = "none" | "available" | "saving" | "saved";

/** A stored value as this editor writes it, or null for anything else. */
function readStored(raw: string): DraftData | null {
  try {
    return JSON.parse(raw) as DraftData;
  } catch {
    return null;
  }
}

const isExpired = (draft: DraftData) =>
  Date.now() - draft.timestamp > DRAFT_EXPIRATION_MS;

/** The text kept under a key, or null for none or one past its week. */
function readDraft(key: string): string | null {
  const raw = key ? localStorage.getItem(key) : null;
  const draft = raw ? readStored(raw) : null;
  if (!draft) return null;
  if (isExpired(draft)) {
    localStorage.removeItem(key);
    return null;
  }
  return draft.content;
}

/** The text kept under a key, or false when localStorage would not take it. */
function writeDraft(key: string, content: string): boolean {
  const draftData: DraftData = { content, timestamp: Date.now() };
  try {
    localStorage.setItem(key, JSON.stringify(draftData));
    return true;
  } catch {
    return false;
  }
}

function cleanupExpiredDrafts() {
  const expired: string[] = [];
  for (let i = 0; i < localStorage.length; i++) {
    const key = localStorage.key(i);
    const raw = key?.startsWith(DRAFT_PREFIX)
      ? localStorage.getItem(key)
      : null;
    const draft = raw ? readStored(raw) : null;
    if (key && draft && isExpired(draft)) expired.push(key);
  }
  expired.forEach((key) => localStorage.removeItem(key));
}

/** One editor's draft under its subject's key: the router reuses the composer when the subject changes. */
export function useEditorDraft(target: EditorTarget) {
  const hasDraft = ref(false);
  const draftStatus = ref<"idle" | "saving" | "saved">("idle");
  let lastSavedDraft = "";

  const key = () =>
    target.props.draftKey ? `${DRAFT_PREFIX}${target.props.draftKey}` : "";
  const loadDraft = () => readDraft(key());

  function saveDraft(content: string) {
    const draftKey = key();
    if (!draftKey || content === lastSavedDraft) return;

    draftStatus.value = "saving";

    if (content.trim()) {
      if (writeDraft(draftKey, content)) {
        lastSavedDraft = content;
        draftStatus.value = "saved";
      } else {
        draftStatus.value = "idle";
      }
    } else {
      localStorage.removeItem(draftKey);
      lastSavedDraft = "";
      draftStatus.value = "idle";
    }
  }

  function clearDraft() {
    const draftKey = key();
    if (draftKey) {
      localStorage.removeItem(draftKey);
      lastSavedDraft = "";
    }
  }

  function restoreDraft() {
    const draft = loadDraft();
    if (!draft) return;
    target.apply(draft);
    hasDraft.value = false;
  }

  // A change of subject empties the box and offers that subject's draft.
  watch(
    () => target.props.draftKey,
    (draftKey, previous) => {
      lastSavedDraft = "";
      draftStatus.value = "idle";
      if (previous) target.apply("");
      const draft = draftKey ? loadDraft() : null;
      hasDraft.value = Boolean(draft?.trim());
    },
  );

  onMounted(() => {
    cleanupExpiredDrafts();
    const draft = loadDraft();
    if (draft && draft.trim() && draft !== target.props.modelValue) {
      hasDraft.value = true;
    }
  });

  const state = computed<DraftState>(() => {
    if (!target.props.draftKey) return "none";
    if (hasDraft.value) return "available";
    if (draftStatus.value === "saving") return "saving";
    if (draftStatus.value === "saved") return "saved";
    return "none";
  });

  return { state, saveDraft, clearDraft, restoreDraft };
}
