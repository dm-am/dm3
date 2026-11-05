/**
 * The size ratchet of the client: how large a file, a block of a single-file
 * component and a function may be, and the register of what was already larger
 * on the day the limits arrived.
 *
 * Size is what an agent pays for on every read: a file is read whole, comments
 * included, so the limits count every line the way the reader meets it. A limit
 * that the tree cannot meet on its first day gets switched off on its second, so
 * the files past it are frozen rather than exempted: each record is the measured
 * size, not a ceiling. Growth fails, and so does shrinkage, because a record left
 * above its file would let the file grow back up to it unnoticed.
 *
 * src/gates/sizeRatchet.spec.ts holds the register to the tree from both ends: an
 * offender with no record fails, and so does a record with no offender left
 * behind it. eslint.config.js reads the same register to raise the limits per
 * file, which is the only way past them: the size rules carry no "off" and no
 * ignores, and a disable comment is a suppression SuppressedRulesShould refuses.
 *
 * The register only shrinks. A new record widens the exceptions, and that takes
 * the owner's word; the lawful move is to split the code. When a file is split,
 * its record may move to one of the parts with a number no larger than before.
 *
 * ESLint holds a function to the largest recorded function of its file, because
 * its thresholds are per file; the spec holds every function to its own record.
 *
 * @typedef {{ lines?: number, complexity?: number }} FunctionSize
 * @typedef {{ script?: number, template?: number, style?: number }} BlockSizes
 * @typedef {{ lines?: number, blocks?: BlockSizes, functions?: Record<string, FunctionSize> }} FileSize
 */

/** The limits every file is held to unless the register says otherwise. */
export const LIMITS = Object.freeze({
  lines: 600,
  functionLines: 80,
  complexity: 15,
  blocks: Object.freeze({ script: 400, template: 300, style: 200 }),
});

/**
 * Where function length is held: everything but specs. A describe or an it
 * callback is the whole suite or the whole case, long by nature, and its length
 * says nothing a file limit does not already say.
 */
export const FUNCTION_SCOPE = [
  "**/!(*.spec).{js,jsx,cjs,mjs,ts,tsx,cts,mts,vue}",
];

/**
 * Path from the client folder -> recorded sizes. Only the measures past their
 * limit are recorded; a function is keyed by its chain of enclosing names, the
 * way src/shared/lib/testing/sizes.ts names it.
 *
 * One line per file and one per function, hence prettier-ignore: printed the
 * way prettier prints objects, the register would itself be past the file
 * limit it records.
 *
 * @type {Record<string, FileSize>}
 */
// prettier-ignore
export const REGISTER = {
  "src/entities/achievement/lib/getMetricValue.ts": { functions: {
    "getMetricValue": { complexity: 28 },
  } },
  "src/entities/blog/model/detailsStore.ts": { functions: {
    "defineStore:useBlogDetailsStore": { lines: 224 },
  } },
  "src/entities/blog/model/store.ts": { functions: {
    "defineStore:useBlogsStore": { lines: 105 },
  } },
  "src/entities/blog/model/useBlogDisplay.ts": { functions: {
    "useBlogDisplay": { lines: 89 },
  } },
  "src/entities/game/api/gameCatalogApi.ts": { functions: {
    "searchGames": { complexity: 32 },
  } },
  "src/entities/game/model/detailsStore.ts": { functions: {
    "defineStore:useGameDetailsStore": { lines: 156 },
  } },
  "src/entities/game/model/pulseStore.ts": { functions: {
    "defineStore:usePulseStore": { lines: 96 },
  } },
  "src/entities/game/model/ratedPostsParams.ts": { functions: {
    "buildRatedPostsParams": { complexity: 19 },
  } },
  "src/entities/game/model/ratedPostsStore.ts": { functions: {
    "fetchBestPostOfUser": { complexity: 16 },
  } },
  "src/entities/global-chat/model/eventsStore.ts": { functions: {
    "defineStore:useGlobalChatEventsStore": { lines: 117 },
  } },
  "src/entities/global-chat/model/store.ts": { functions: {
    "defineStore:useGlobalChatStore": { lines: 393 },
  } },
  "src/entities/message/model/store.ts": { functions: {
    "defineStore:useMessagingStore": { lines: 179 },
  } },
  "src/entities/poll/model/store.ts": { functions: {
    "defineStore:usePollsStore": { lines: 84 },
  } },
  "src/entities/testimonial/model/store.ts": { functions: {
    "defineStore:useTestimonialStore": { lines: 95 },
  } },
  "src/entities/user/model/communityStore.ts": { functions: {
    "buildApiParams": { complexity: 16 },
    "defineStore:useCommunityStore": { lines: 122 },
  } },
  "src/entities/user/model/useAvatarUpload.ts": { functions: {
    "useAvatarUpload": { lines: 121 },
  } },
  "src/features/blog-filter/model/useBlogsFilter.ts": { functions: {
    "useBlogsFilter": { lines: 97 },
  } },
  "src/features/create-game/ui/CreateGameForm.vue": { functions: {
    "handleSubmit": { complexity: 21 },
  } },
  "src/features/create-poll/model/useCreatePoll.ts": { functions: {
    "useCreatePoll": { lines: 138 },
  } },
  "src/features/game-filter/model/gamesFilterQuery.ts": { functions: {
    "buildQueryFromState": { complexity: 20 },
  } },
  "src/features/game-filter/model/gamesFilterReducer.ts": { functions: {
    "reducer": { lines: 94, complexity: 21 },
  } },
  "src/features/message-search/model/searchStore.ts": { functions: {
    "defineStore:useMessageSearchStore": { lines: 121 },
  } },
  "src/features/poll-filter/model/usePollsFilter.ts": { functions: {
    "usePollsFilter": { lines: 90 },
  } },
  "src/features/pulse-filter/model/usePulseFilter.ts": { functions: {
    "parseQueryToState": { complexity: 17 },
    "reducer": { complexity: 29 },
  } },
  "src/features/room-post-filter/model/useRoomPostFilter.ts": { functions: {
    "reducer": { complexity: 21 },
  } },
  "src/features/upload/model/postAttachments.ts": { functions: {
    "reserveAttachmentImageBoxes": { complexity: 16 },
  } },
  "src/features/user-filter/model/useUsersFilter.ts": { functions: {
    "usersSearchParams": { complexity: 18 },
  } },
  "src/gates/accessibleNames.spec.ts": { functions: {
    "namedByContent": { complexity: 21 },
  } },
  "src/gates/confirmCopy.spec.ts": { functions: {
    "literalsIn": { complexity: 16 },
    "valueRegion": { complexity: 18 },
  } },
  "src/gates/e2eDiscipline.spec.ts": { functions: {
    "gates": { complexity: 17 },
  } },
  "src/gates/e2eSelectors.spec.ts": { functions: {
    "vocabulary": { complexity: 16 },
  } },
  "src/gates/emptyStateAlignment.spec.ts": { functions: {
    "insetSides": { complexity: 21 },
  } },
  "src/gates/namedFiles.spec.ts": { functions: {
    "commentsOf": { complexity: 18 },
  } },
  "src/gates/pagingVocabulary.spec.ts": { functions: {
    "keysOf": { complexity: 37 },
  } },
  "src/pages/account/sections/AccountSecurityHistorySection.vue": { functions: {
    "eventTitle": { complexity: 20 },
  } },
  "src/pages/game/settings/GameInfoSection.vue": { functions: {
    "watch(game)": { complexity: 17 },
  } },
  "src/shared/lib/bbcode/bbcodeGrammar.spec.ts": { functions: {
    "seen": { complexity: 27 },
  } },
  "src/shared/lib/bbcode/bbcodeSpelling.ts": { functions: {
    "readOpening": { lines: 113, complexity: 41 },
  } },
  "src/shared/lib/bbcode/bbcodeToHtml.ts": { functions: {
    "HtmlWriter.container > runs.map:paragraphs": { complexity: 21 },
    "HtmlWriter.inline": { complexity: 16 },
  } },
  "src/shared/lib/bbcode/bbcodeWarnings.ts": { functions: {
    "wordIssue": { complexity: 35 },
  } },
  "src/shared/lib/bbcode/htmlToBbcode.ts": { functions: {
    "BbcodeWriter.markedLink": { complexity: 16 },
    "BbcodeWriter.node": { lines: 116, complexity: 59 },
  } },
  "src/shared/lib/composables/createFilterDispatcher.ts": { functions: {
    "createFilterDispatcher": { lines: 98 },
  } },
  "src/shared/lib/composables/useAnchoredInfiniteScroll.ts": { functions: {
    "useAnchoredInfiniteScroll": { lines: 125 },
  } },
  "src/shared/lib/composables/useApiResource.ts": { functions: {
    "useApiResource": { lines: 123 },
  } },
  "src/shared/lib/composables/useCodeInput.ts": { functions: {
    "useCodeInput": { lines: 114 },
  } },
  "src/shared/lib/composables/useDialogAction.ts": { functions: {
    "useDialogAction": { lines: 141 },
  } },
  "src/shared/lib/composables/useDialogShell.ts": { functions: {
    "useDialogShell": { lines: 101 },
  } },
  "src/shared/lib/composables/useExpandable.ts": { functions: {
    "useExpandable": { lines: 86 },
  } },
  "src/shared/lib/composables/useFlipReorder.ts": { functions: {
    "useFlipReorder": { lines: 81 },
  } },
  "src/shared/lib/composables/useMessageToolbar.ts": { functions: {
    "useMessageToolbar": { lines: 118 },
  } },
  "src/shared/lib/composables/useValidatedField.ts": { functions: {
    "useValidatedField": { lines: 131 },
  } },
  "src/shared/lib/testing/listAndPopupCopy.ts": { functions: {
    "copyOf": { complexity: 20 },
    "readTemplate > visit": { complexity: 35 },
  } },
  "src/shared/lib/testing/markupCorpus.ts": { functions: {
    "asBothSidesRead": { complexity: 18 },
  } },
  "src/shared/lib/testing/markupReading.ts": { functions: {
    "inThePreview": { lines: 108 },
    "inThePreview > walk": { lines: 87, complexity: 34 },
    "onThePage": { lines: 121 },
    "onThePage > walk": { lines: 116, complexity: 40 },
  } },
  "src/shared/lib/testing/toastScan.ts": { functions: {
    "expressionAt": { complexity: 18 },
    "scanOf": { lines: 181 },
  } },
  "src/shared/ui/Form/FormField.vue": { functions: {
    "bindControl": { complexity: 19 },
  } },
  "src/shared/ui/Layout/Dialog.spec.ts": { lines: 743 },
  "src/widgets/chat-feed/ChatFeed.vue": { lines: 626 },
  "src/widgets/chat-message/ChatMessage.vue": { blocks: { style: 214 } },
};

/**
 * The size rules one register entry sets for its file: each limit raised to the
 * largest size recorded under it, and only the rules the entry raises.
 *
 * @param {string} path
 * @param {FileSize} entry
 */
export function recordedRules(path, entry) {
  const functions = Object.values(entry.functions ?? {});
  const most = (measure) =>
    Math.max(0, ...functions.map((size) => size[measure] ?? 0));
  const rules = {};
  if (entry.lines) rules["max-lines"] = ["error", { max: entry.lines }];
  if (most("lines") > 0) {
    rules["max-lines-per-function"] = ["error", { max: most("lines") }];
  }
  if (most("complexity") > 0) {
    rules.complexity = [
      "error",
      Math.max(LIMITS.complexity, most("complexity")),
    ];
  }
  if (entry.blocks && path.endsWith(".vue")) {
    rules["vue/max-lines-per-block"] = [
      "error",
      { ...LIMITS.blocks, ...entry.blocks },
    ];
  }
  return rules;
}
