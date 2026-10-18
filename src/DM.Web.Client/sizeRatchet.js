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
 * src/sizeRatchet.spec.ts holds the register to the tree from both ends: an
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
  "e2e/fixtures/tables.ts": { functions: {
    "readTables": { lines: 107, complexity: 19 },
  } },
  "e2e/tests/common/one-line.spec.ts": { lines: 786, functions: {
    "hiddenOrCut": { complexity: 74 },
  } },
  "e2e/tests/common/tables-fit.spec.ts": { lines: 604, functions: {
    "edgesAndHeads": { complexity: 18 },
  } },
  "e2e/tests/forum/topics-table-fit.spec.ts": { functions: {
    "measureInPage > page.evaluate": { complexity: 19 },
  } },
  "e2e/tests/gaming/games-list.spec.ts": { lines: 918 },
  "e2e/tests/personal/tickets.spec.ts": { lines: 846, functions: {
    "open > page.route@1": { complexity: 20 },
  } },
  "e2e/tests/sidebar/sidebar.spec.ts": { lines: 625 },
  "src/accessibleNames.spec.ts": { functions: {
    "namedByContent": { complexity: 21 },
  } },
  "src/app/App.spec.ts": { lines: 624 },
  "src/app/providers/routePrefetch.ts": { functions: {
    "installRoutePrefetch": { lines: 157 },
  } },
  "src/app/providers/router.spec.ts": { lines: 872 },
  "src/app/providers/router.ts": { lines: 1006 },
  "src/confirmCopy.spec.ts": { functions: {
    "literalsIn": { complexity: 16 },
    "valueRegion": { complexity: 18 },
  } },
  "src/dialogSurface.spec.ts": { lines: 1013 },
  "src/e2eDiscipline.spec.ts": { functions: {
    "gates": { complexity: 17 },
  } },
  "src/e2eSelectors.spec.ts": { functions: {
    "vocabulary": { complexity: 16 },
  } },
  "src/emptyStateAlignment.spec.ts": { functions: {
    "insetSides": { complexity: 21 },
  } },
  "src/entities/achievement/lib/getMetricValue.ts": { functions: {
    "getMetricValue": { complexity: 28 },
  } },
  "src/entities/blog/model/detailsStore.ts": { functions: {
    "defineStore:useBlogDetailsStore": { lines: 437 },
  } },
  "src/entities/blog/model/store.spec.ts": { lines: 745 },
  "src/entities/blog/model/store.ts": { functions: {
    "buildApiParams": { complexity: 16 },
    "defineStore:useBlogsStore": { lines: 169 },
  } },
  "src/entities/blog/model/useBlogDisplay.ts": { functions: {
    "useBlogDisplay": { lines: 128 },
  } },
  "src/entities/forum/model/store.spec.ts": { lines: 1308 },
  "src/entities/forum/model/store.ts": { lines: 779, functions: {
    "defineStore:useBoardsStore": { lines: 674 },
    "defineStore:useBoardsStore > readListing": { lines: 101, complexity: 25 },
  } },
  "src/entities/game/api/gameApi.ts": { lines: 1073, functions: {
    "GameApi.searchGames": { complexity: 32 },
  } },
  "src/entities/game/model/detailsStore.ts": { lines: 709, functions: {
    "defineStore:useGameDetailsStore": { lines: 667 },
  } },
  "src/entities/game/model/pulseStore.ts": { functions: {
    "defineStore:usePulseStore": { lines: 124 },
  } },
  "src/entities/game/model/ratedPostsParams.ts": { functions: {
    "buildRatedPostsParams": { complexity: 22 },
  } },
  "src/entities/game/model/ratedPostsStore.ts": { functions: {
    "defineStore:useRatedPostsStore": { lines: 218 },
    "defineStore:useRatedPostsStore > fetchBestPostOfUser": { complexity: 19 },
  } },
  "src/entities/game/model/store.spec.ts": { lines: 1292 },
  "src/entities/game/model/store.ts": { functions: {
    "defineStore:useGamesStore": { lines: 237 },
  } },
  "src/entities/game/model/types.ts": { lines: 1005 },
  "src/entities/game/model/useGameDisplay.ts": { functions: {
    "useGameDisplay": { lines: 282 },
  } },
  "src/entities/global-chat/model/store.spec.ts": { lines: 733 },
  "src/entities/global-chat/model/store.ts": { lines: 729, functions: {
    "defineStore:useGlobalChatStore": { lines: 709 },
    "defineStore:useGlobalChatStore > fetchMessages": { complexity: 16 },
  } },
  "src/entities/message/model/store.ts": { lines: 845, functions: {
    "defineStore:useMessagingStore": { lines: 819 },
    "defineStore:useMessagingStore > fetchMoreAfter": { complexity: 16 },
    "defineStore:useMessagingStore > fetchMoreBefore": { complexity: 16 },
  } },
  "src/entities/moderation/api/moderationApi.ts": { lines: 746 },
  "src/entities/moderation/ui/WarningCard.spec.ts": { lines: 818 },
  "src/entities/poll/model/store.ts": { functions: {
    "defineStore:usePollsStore": { lines: 98 },
  } },
  "src/entities/statistics/model/store.ts": { functions: {
    "defineStore:useStatisticsStore": { lines: 89 },
  } },
  "src/entities/subscription/model/store.ts": { functions: {
    "defineStore:useSubscriptionsStore": { lines: 152 },
  } },
  "src/entities/testimonial/model/store.ts": { functions: {
    "defineStore:useTestimonialStore": { lines: 130 },
  } },
  "src/entities/user/lib/useProfileEdit.ts": { functions: {
    "useProfileEdit": { lines: 93 },
  } },
  "src/entities/user/model/communityStore.ts": { functions: {
    "buildApiParams": { complexity: 24 },
    "defineStore:useCommunityStore": { lines: 172 },
  } },
  "src/entities/user/model/useAvatarUpload.ts": { functions: {
    "useAvatarUpload": { lines: 249 },
    "useAvatarUpload > uploadFile": { lines: 94 },
  } },
  "src/features/account-time-zone/model/useTimeZoneCheck.ts": { functions: {
    "useTimeZoneCheck": { lines: 108 },
  } },
  "src/features/attribute-schema-editor/ui/AttributeSchemaEditor.vue": { lines: 604 },
  "src/features/auth/ui/LoginForm.spec.ts": { lines: 1379 },
  "src/features/auth/ui/LoginForm.vue": { lines: 856, blocks: { script: 605 }, functions: {
    "submit": { lines: 108, complexity: 17 },
  } },
  "src/features/auth/ui/RegistrationForm.spec.ts": { lines: 984 },
  "src/features/auth/ui/RegistrationForm.vue": { lines: 697, blocks: { script: 447 }, functions: {
    "submitPassword": { lines: 84 },
  } },
  "src/features/blog-filter/model/useBlogsFilter.ts": { functions: {
    "useBlogsFilter": { lines: 111 },
  } },
  "src/features/comment/ui/CommentItem.spec.ts": { lines: 1407 },
  "src/features/comment/ui/CommentItem.vue": { lines: 924, blocks: { script: 455, style: 214 } },
  "src/features/create-game/ui/CreateGameForm.vue": { functions: {
    "handleSubmit": { complexity: 21 },
  } },
  "src/features/create-poll/model/useCreatePoll.ts": { functions: {
    "useCreatePoll": { lines: 163 },
  } },
  "src/features/edit-character/ui/CharacterForm.spec.ts": { lines: 840 },
  "src/features/game-filter/model/useGamesFilter.ts": { lines: 632, functions: {
    "buildQueryFromState": { complexity: 20 },
    "parseQueryToState": { complexity: 22 },
    "reducer": { lines: 125, complexity: 26 },
    "useGamesFilter": { lines: 232 },
    "useGamesFilter > computed:searchParams": { complexity: 19 },
  } },
  "src/features/game-filter/ui/GamesFilter.vue": { lines: 947, blocks: { script: 676 }, functions: {
    "getDropdownTitle": { complexity: 16 },
  } },
  "src/features/message-search/model/searchStore.ts": { functions: {
    "defineStore:useMessageSearchStore": { lines: 148 },
  } },
  "src/features/poll-filter/model/usePollsFilter.ts": { functions: {
    "usePollsFilter": { lines: 103 },
  } },
  "src/features/pulse-filter/model/usePulseFilter.ts": { functions: {
    "parseQueryToState": { complexity: 17 },
    "reducer": { complexity: 29 },
    "usePulseFilter": { lines: 81 },
  } },
  "src/features/room-post-filter/model/useRoomPostFilter.ts": { functions: {
    "reducer": { complexity: 21 },
  } },
  "src/features/support-ticket/ui/SupportTicketForm.vue": { lines: 951, blocks: { script: 621 }, functions: {
    "computed:REASON_NOTES": { lines: 95 },
    "showRefusal": { complexity: 21 },
    "submit": { complexity: 16 },
    "validate": { complexity: 18 },
  } },
  "src/features/topic/ui/TopicView.spec.ts": { lines: 651 },
  "src/features/upload/model/postAttachments.ts": { functions: {
    "reserveAttachmentImageBoxes": { complexity: 17 },
  } },
  "src/features/user-filter/model/useUsersFilter.ts": { functions: {
    "buildQueryFromState": { complexity: 19 },
    "parseQueryToState": { lines: 85, complexity: 29 },
    "useUsersFilter": { lines: 157 },
    "useUsersFilter > computed:searchParams": { complexity: 18 },
  } },
  "src/fillContrast.spec.ts": { lines: 818 },
  "src/guestSurface.spec.ts": { lines: 628 },
  "src/namedFiles.spec.ts": { functions: {
    "commentsOf": { complexity: 18 },
  } },
  "src/onelineCopy.spec.ts": { lines: 612, functions: {
    "describe \"one-line compositions\" > it \"`.${strip} keeps its parts inline and in the flow`\"": { complexity: 16 },
  } },
  "src/pages/account/AccountActivationPage.spec.ts": { lines: 698 },
  "src/pages/account/AccountActivationPage.vue": { lines: 626 },
  "src/pages/account/sections/AccountNotificationsSection.spec.ts": { lines: 706 },
  "src/pages/account/sections/AccountNotificationsSection.vue": { lines: 619 },
  "src/pages/account/sections/AccountSecurityHistorySection.vue": { functions: {
    "eventClass": { complexity: 18 },
    "eventTitle": { complexity: 20 },
  } },
  "src/pages/account/sections/AccountTwoFactorSection.spec.ts": { lines: 743 },
  "src/pages/account/sections/AccountTwoFactorSection.vue": { lines: 765, blocks: { template: 304 } },
  "src/pages/forum/TopicsList.spec.ts": { lines: 1340 },
  "src/pages/forum/TopicsList.vue": { lines: 944, blocks: { script: 487, template: 305 } },
  "src/pages/forum/usePinnedOrder.ts": { functions: {
    "usePinnedOrder": { lines: 250 },
  } },
  "src/pages/game/CharacterEdit.spec.ts": { lines: 713 },
  "src/pages/game/CharacterEdit.vue": { blocks: { script: 445 }, functions: {
    "computed:actions": { lines: 98, complexity: 19 },
  } },
  "src/pages/game/GameChatRoom.spec.ts": { lines: 1562 },
  "src/pages/game/GameChatRoom.vue": { lines: 852, blocks: { script: 628 }, functions: {
    "pollForNewer": { lines: 95, complexity: 26 },
  } },
  "src/pages/game/GameRoom.spec.ts": { lines: 819 },
  "src/pages/game/GameRoom.vue": { lines: 1182, blocks: { script: 695, style: 244 }, functions: {
    "submitPost": { complexity: 18 },
  } },
  "src/pages/game/settings/GameInfoSection.vue": { functions: {
    "watch(game)": { complexity: 17 },
  } },
  "src/pages/game/settings/RoomsSection.spec.ts": { lines: 729 },
  "src/pages/game/settings/RoomsSection.vue": { lines: 976, blocks: { script: 557 } },
  "src/pages/global-chat/ChatEventsPanel.vue": { lines: 617 },
  "src/pages/global-chat/GlobalChatPage.vue": { blocks: { script: 425 } },
  "src/pages/messenger/ChatView.spec.ts": { lines: 1889 },
  "src/pages/messenger/ChatView.vue": { lines: 660, blocks: { script: 544 }, functions: {
    "readTail": { lines: 87, complexity: 20 },
  } },
  "src/pages/moderation/ModerationBans.spec.ts": { lines: 699 },
  "src/pages/moderation/ModerationWarnings.spec.ts": { lines: 737 },
  "src/pages/personal/NotificationsPage.spec.ts": { lines: 1086 },
  "src/pages/profile/ProfileAchievementsSection.vue": { lines: 668, blocks: { style: 280 } },
  "src/pages/profile/ProfilePage.vue": { lines: 1607, blocks: { script: 732, style: 379, template: 488 } },
  "src/pages/profile/ProfilePageActions.spec.ts": { lines: 632 },
  "src/pages/support/SupportTicketForm.spec.ts": { lines: 924 },
  "src/pagingVocabulary.spec.ts": { functions: {
    "keysOf": { complexity: 37 },
  } },
  "src/sentencePeriods.spec.ts": { lines: 659, functions: {
    "copyOf": { complexity: 20 },
    "readTemplate > visit": { complexity: 35 },
  } },
  "src/shared/api/client.spec.ts": { lines: 739 },
  "src/shared/api/client.ts": { lines: 641, functions: {
    "Api.constructor": { lines: 98 },
    "Api.constructor > this.axios.interceptors.response.use@1": { lines: 90, complexity: 22 },
  } },
  "src/shared/lib/composables/createCommentSection.ts": { functions: {
    "createCommentSection": { lines: 113 },
  } },
  "src/shared/lib/composables/createFilterDispatcher.ts": { functions: {
    "createFilterDispatcher": { lines: 130 },
    "createFilterDispatcher > dispatch": { lines: 93 },
  } },
  "src/shared/lib/composables/useAnchoredInfiniteScroll.ts": { functions: {
    "useAnchoredInfiniteScroll": { lines: 147 },
  } },
  "src/shared/lib/composables/useApiResource.ts": { functions: {
    "useApiResource": { lines: 162 },
    "useApiResource > fetch": { lines: 87 },
  } },
  "src/shared/lib/composables/useChatComposer.ts": { functions: {
    "useChatComposer": { lines: 81 },
  } },
  "src/shared/lib/composables/useCodeInput.ts": { functions: {
    "useCodeInput": { lines: 136 },
  } },
  "src/shared/lib/composables/useDialogAction.ts": { functions: {
    "useDialogAction": { lines: 149 },
  } },
  "src/shared/lib/composables/useDialogShell.ts": { functions: {
    "useDialogShell": { lines: 117 },
  } },
  "src/shared/lib/composables/useExpandable.ts": { functions: {
    "useExpandable": { lines: 88 },
  } },
  "src/shared/lib/composables/useExpandableSection.ts": { functions: {
    "useExpandableSection": { lines: 82 },
  } },
  "src/shared/lib/composables/useFilterDropdown.ts": { functions: {
    "useFilterDropdown": { lines: 92 },
  } },
  "src/shared/lib/composables/useFlipReorder.ts": { functions: {
    "useFlipReorder": { lines: 87 },
  } },
  "src/shared/lib/composables/useMessageToolbar.ts": { functions: {
    "useMessageToolbar": { lines: 134 },
  } },
  "src/shared/lib/composables/useSignalR.ts": { functions: {
    "useGlobalSignalR": { lines: 98 },
  } },
  "src/shared/lib/composables/useValidatedField.ts": { functions: {
    "useValidatedField": { lines: 155 },
  } },
  "src/shared/lib/copy-rules.spec.ts": { lines: 656 },
  "src/shared/lib/filters/createAuthorDateFilter.ts": { functions: {
    "createAuthorDateFilter": { lines: 203 },
  } },
  "src/shared/lib/testing/fontFace.ts": { functions: {
    "loadFace": { lines: 227, complexity: 29 },
  } },
  "src/shared/lib/testing/markupFuzz.ts": { lines: 826, functions: {
    "asBothSidesRead": { complexity: 18 },
    "inThePreview": { lines: 108 },
    "inThePreview > walk": { lines: 87, complexity: 34 },
    "onThePage": { lines: 121 },
    "onThePage > walk": { lines: 116, complexity: 40 },
  } },
  "src/shared/lib/testing/toastScan.ts": { functions: {
    "expressionAt": { complexity: 18 },
    "scanOf": { lines: 181 },
  } },
  "src/shared/lib/utils/bbcode.spec.ts": { lines: 2225 },
  "src/shared/lib/utils/bbcode.ts": { lines: 2967, functions: {
    "BbcodeWriter.hidden": { complexity: 16 },
    "BbcodeWriter.markedLink": { complexity: 16 },
    "BbcodeWriter.node": { lines: 116, complexity: 59 },
    "HtmlWriter.container > runs.map:paragraphs": { complexity: 21 },
    "HtmlWriter.inline": { complexity: 16 },
    "readOpening": { lines: 113, complexity: 41 },
    "wordIssue": { complexity: 35 },
  } },
  "src/shared/lib/utils/bbcodeGrammar.spec.ts": { functions: {
    "seen": { complexity: 27 },
  } },
  "src/shared/lib/utils/bbcodeInteractive.spec.ts": { lines: 661 },
  "src/shared/lib/utils/bbcodeInteractive.ts": { lines: 1005, functions: {
    "initNsfw": { lines: 166 },
    "initNsfw > nsfwHeads.forEach": { lines: 150 },
    "initPrivateRecipients": { lines: 110 },
    "initPrivateRecipients > pieces.forEach": { lines: 99 },
    "initSpoilers": { lines: 155 },
    "initSpoilers > spoilerHeads.forEach": { lines: 121 },
  } },
  "src/shared/lib/utils/statusCopy.spec.ts": { functions: {
    "stripComments": { complexity: 23 },
  } },
  "src/shared/stores/ui.ts": { functions: {
    "defineStore:useUiStore": { lines: 93 },
  } },
  "src/shared/ui/BBCodeEditor/BBCodeEditor.spec.ts": { lines: 2279 },
  "src/shared/ui/BBCodeEditor/BBCodeEditor.vue": { lines: 1884, blocks: { script: 1373, style: 314 } },
  "src/shared/ui/BBCodeEditor/lib/visualMode.spec.ts": { lines: 644 },
  "src/shared/ui/DatePicker/CalendarGrid.vue": { lines: 722, blocks: { script: 419 } },
  "src/shared/ui/DatePicker/DateInput.vue": { lines: 668, blocks: { script: 485 } },
  "src/shared/ui/ExpandableList/ExpandableList.vue": { lines: 623 },
  "src/shared/ui/Form/FormField.vue": { functions: {
    "bindControl": { complexity: 19 },
  } },
  "src/shared/ui/Layout/Dialog.spec.ts": { lines: 772 },
  "src/shared/ui/Paging/Paging.spec.ts": { lines: 622 },
  "src/shared/ui/SpeechBubble/SpeechBubble.vue": { blocks: { style: 210 } },
  "src/shared/ui/Tooltip/Tooltip.spec.ts": { lines: 705 },
  "src/styleSystem.spec.ts": { lines: 1195 },
  "src/widgets/chat-feed/ChatFeed.vue": { lines: 910, blocks: { script: 424 } },
  "src/widgets/chat-feed/useChatHistoryWindow.ts": { functions: {
    "useChatHistoryWindow": { lines: 251 },
  } },
  "src/widgets/chat-message/ChatMessage.vue": { lines: 725, blocks: { style: 276 } },
  "src/widgets/game-post/GamePost.spec.ts": { lines: 1758 },
  "src/widgets/game-post/GamePost.vue": { lines: 1805, blocks: { script: 939, style: 451, template: 393 }, functions: {
    "saveEditPost": { complexity: 17 },
  } },
  "src/widgets/header/Header.vue": { blocks: { style: 335 } },
  "src/widgets/notepad/NotepadBoard.spec.ts": { lines: 774 },
  "src/widgets/notepad/NotepadBoard.vue": { lines: 724, blocks: { style: 204 } },
  "src/widgets/sidebar/SidebarBlock.spec.ts": { functions: {
    "describe \"SidebarBlock\" > describe \"The +/- sign\" > opacityOf": { complexity: 16 },
  } },
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
