/**
 * The size ratchet of the client and scripts/: how large a file, a block of a single-file
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
 * @typedef {{ lines?: number, complexity?: number, depth?: number, why?: string }} FunctionSize
 * @typedef {{ script?: number, template?: number, style?: number }} BlockSizes
 * @typedef {{ lines?: number, depth?: number, blocks?: BlockSizes, functions?: Record<string, FunctionSize>, why?: string }} FileSize
 */

/** The limits every file is held to unless the register says otherwise. */
export const LIMITS = Object.freeze({
  lines: 600,
  functionLines: 80,
  complexity: 15,
  // Statements nested inside one another, per function: a fourth level is a
  // part of its own waiting for a name.
  depth: 3,
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
 * Path from the client folder -> recorded sizes, and for scripts/ the path from
 * the repository root, the folder its lint runs in. Only the measures past their
 * limit are recorded; a function is keyed by its chain of enclosing names, the
 * way src/shared/lib/testing/sizes.ts names it, and the nesting depth at the top
 * level of a module, outside every function, is the file's own `depth`. The
 * scripts/ records are what that folder held when the owner put it under these
 * limits; the depth records are what the tree held when depth was added.
 *
 * Every other record carries its `why`, at the function for a function and at
 * the file for a file's lines or blocks: one line, in the code's own terms, on
 * what makes it large and why a cut would not make it clearer. A record of
 * depth alone carries none.
 *
 * One line per file and one per function, hence prettier-ignore: printed the
 * way prettier prints objects, the register would itself be past the file
 * limit it records.
 *
 * @type {Record<string, FileSize>}
 */
// prettier-ignore
export const REGISTER = {
  "scripts/docs/lib/router.mjs": { functions: {
    "readRoutes > walkRecords": { depth: 4 },
  } },
  "scripts/visual/api-fixture.mjs": { functions: {
    "discover": { lines: 107, why: "one fixture object, an id per route parameter of the snapshot, each looked up by its own request as the member or the administrator" },
  } },
  "scripts/visual/capture.ts": { functions: {
    "domOf > page.evaluate > walk": { complexity: 20, why: "the serialiser runs inside page.evaluate, where no helper from outside reaches, and gives each kind of node its own line: text, element, form control, shadow root" },
  } },
  "scripts/visual/compare-code.mjs": { functions: {
    "compareBuilds": { depth: 4 },
  } },
  "scripts/visual/compare-css.mjs": { functions: {
    "main": { depth: 4 },
  } },
  "scripts/visual/compare/csharp.mjs": { functions: {
    "csharpTokens": { complexity: 25, why: "the main loop of the C# lexer: a branch per lexical form, directive, comment, literal, word and operator, over one shared position" },
    "holeEnd": { complexity: 22, why: "a scanner of one interpolation hole: a branch per thing that can hold a brace, comment, string, character, bracket and format specifier, over one position and depth" },
    "stringEnd": { complexity: 32, depth: 4, why: "a scanner of every C# string form, regular, verbatim, interpolated and raw, whose prefix and run of quotes steer one loop over the body" },
  } },
  "scripts/visual/compare/tokens.mjs": { functions: {
    "sassCode": { complexity: 26, depth: 4, why: "a scanner of one Sass line whose quote, url() and pending space states steer every character" },
    "sassTokens": { depth: 4 },
  } },
  "src/app/providers/routeTitles.spec.ts": { functions: {
    "describe \"document titles\" > it \"keeps every title string inside the interface copy rules\"": { depth: 4 },
  } },
  "src/entities/achievement/lib/getMetricValue.ts": { functions: {
    "getMetricValue": { complexity: 28, why: "one switch over AchievementMetric, a case per metric, matched case for case with the server's AchievementMetricResolver" },
  } },
  "src/entities/blog/model/detailsStore.ts": { functions: {
    "defineStore:useBlogDetailsStore": { lines: 157, why: "assembles the slices of a blog's page into one store: its lines are their members, named on the way in and on the way out" },
  } },
  "src/entities/blog/model/store.ts": { functions: {
    "defineStore:useBlogsStore": { lines: 105, why: "a Pinia setup store: the search, its cache and the prefetch of the next page share one closure" },
  } },
  "src/entities/game/api/gameCatalogApi.ts": { functions: {
    "searchGames": { complexity: 32, why: "one optional query parameter per games filter, each sent only when set" },
  } },
  "src/entities/game/model/detailsStore.ts": { functions: {
    "defineStore:useGameDetailsStore": { lines: 156, why: "assembles the slices of a game's page into one store: its lines are their members, named on the way in and on the way out" },
  } },
  "src/entities/game/model/pulseStore.ts": { functions: {
    "defineStore:usePulseStore": { lines: 96, why: "a Pinia setup store: the posts, their page cache and the prefetch share one closure" },
  } },
  "src/entities/game/model/ratedPostsParams.ts": { functions: {
    "buildRatedPostsParams": { complexity: 19, why: "one request field per rated posts filter, each sent only when set, then the scope of the page over them" },
  } },
  "src/entities/global-chat/model/eventsStore.ts": { functions: {
    "defineStore:useGlobalChatEventsStore": { lines: 117, why: "a Pinia setup store: the events, their details by id and the event actions that refresh them share one closure" },
  } },
  "src/entities/message/model/store.ts": { functions: {
    "defineStore:useMessagingStore": { lines: 179, why: "a Pinia setup store over the chat list and the open chat, whose window, unread counts and changes are composed in and read the selection" },
  } },
  "src/entities/testimonial/model/store.ts": { functions: {
    "defineStore:useTestimonialStore": { lines: 95, why: "a Pinia setup store: the list, its cache and the create, update and remove that refresh them share one closure" },
  } },
  "src/entities/user/model/useAvatarUpload.ts": { functions: {
    "useAvatarUpload": { lines: 121, why: "the upload and its drag-and-drop and paste handlers share the progress, the refusal and the flag set on unmount" },
  } },
  "src/features/blog-filter/model/useBlogsFilter.ts": { functions: {
    "useBlogsFilter": { lines: 95, why: "the blogs filter over the route: its state, its search params and one setter per filter, each dispatched to the reducer" },
  } },
  "src/features/create-poll/model/useCreatePoll.ts": { functions: {
    "useCreatePoll": { lines: 138, why: "the poll form: its fields, the bounds of its dates and the list of options, all read by the submit" },
  } },
  "src/features/game-filter/model/gamesFilterQuery.ts": { functions: {
    "buildQueryFromState": { complexity: 20, why: "one query key per games filter, each written only when it differs from the default" },
  } },
  "src/features/game-filter/model/gamesFilterReducer.ts": { functions: {
    "reducer": { lines: 94, complexity: 21, why: "one switch over the actions of the games filter, a case per action" },
  } },
  "src/features/message-search/model/searchStore.ts": { functions: {
    "defineStore:useMessageSearchStore": { lines: 121, why: "a Pinia setup store: the query, the scope and the results, which the search and the next page both write" },
  } },
  "src/features/pulse-filter/model/usePulseFilter.ts": { functions: {
    "reducer": { complexity: 29, why: "one switch over the actions of the pulse filter, a case per action" },
  } },
  "src/features/room-post-filter/model/useRoomPostFilter.ts": { functions: {
    "reducer": { complexity: 21, why: "one switch over the actions of the room post filter, a case per action" },
  } },
  "src/features/user-filter/model/useUsersFilter.ts": { functions: {
    "usersSearchParams": { complexity: 18, why: "one request parameter per users filter, each sent only when set" },
  } },
  "src/gates/accessibleNames.spec.ts": { functions: {
    "namedByContent": { complexity: 21, why: "one walk over an element's children, a branch per kind of child that can give it a name: text, expression, slot, image alt, component" },
  } },
  "src/gates/apiEnvelopeContract.spec.ts": { functions: {
    "apiCalls": { depth: 4 },
  } },
  "src/gates/busyControls.spec.ts": { functions: {
    "describe \"a caption\" > it \"never swaps itself for the state of the request\"": { depth: 5 },
  } },
  "src/gates/confirmCopy.spec.ts": { functions: {
    "literalsIn": { depth: 5 },
    "valueRegion": { complexity: 18, why: "a character scanner: quotes skipped whole and brackets counted until the value ends at the top level" },
  } },
  "src/gates/countedNouns.spec.ts": { functions: {
    "countsNamedIn": { depth: 4 },
    "describe \"a counted noun agrees with its count\" > it \"never leaves a bare noun after a count\"": { depth: 5 },
  } },
  "src/gates/draftKeys.spec.ts": { functions: {
    "declaredProps > visit": { depth: 4 },
  } },
  "src/gates/e2eSelectors.spec.ts": { functions: {
    "vocabulary": { depth: 4 },
  } },
  "src/gates/emptyStateAlignment.spec.ts": { functions: {
    "describe \"empty states and login prompts\" > it \"keeps every deliberate exception centered, so the list cannot rot\"": { depth: 4 },
    "describe \"the lines that stand where a form would be\" > it \"take no inset on the sides held for them\"": { depth: 5 },
    "insetSides": { complexity: 21, why: "one switch over the padding and margin properties, a case per side they set" },
  } },
  "src/gates/focusRing.spec.ts": { functions: {
    "describe \"the focus ring\" > it \"stays on a field the browser filled in\"": { depth: 4 },
  } },
  "src/gates/formButtonPair.spec.ts": { functions: {
    "describe \"the two buttons at the end of a form, against each other\" > it \"keeps the main action apart from the one beside it, in every state\"": { depth: 4 },
  } },
  "src/gates/guestSurface.spec.ts": { functions: {
    "nestedHas": { depth: 4 },
  } },
  "src/gates/guestTitleFits.spec.ts": { functions: {
    "captionsInCode": { depth: 5 },
  } },
  "src/gates/namedFiles.spec.ts": { functions: {
    "commentsOf": { complexity: 18, depth: 4, why: "a character scanner, a branch per lexical form: string, line comment, block comment, HTML comment" },
    "describe \"files named in comments\"": { depth: 4 },
  } },
  "src/gates/pagingVocabulary.spec.ts": { functions: {
    "describe \"paging on the wire\" > it \"is asked for by no name the API retired\"": { depth: 4 },
    "mask": { depth: 4 },
  } },
  "src/gates/pictures.spec.ts": { functions: {
    "describe \"a picture\" > it \"is never rounded, nor the frame or the shimmer that stands for it\"": { depth: 5 },
  } },
  "src/gates/profileTiles.spec.ts": { functions: {
    "metalFallbacks": { depth: 6 },
  } },
  "src/gates/pseudoContent.spec.ts": { functions: {
    "writingPseudos": { depth: 4 },
  } },
  "src/gates/scriptCallers.spec.ts": { depth: 4 },
  "src/gates/styleSystem.spec.ts": { functions: {
    "describe \"the opacity scale\" > it \"is where every opacity comes from\"": { depth: 4 },
  } },
  "src/pages/account/sections/AccountSecurityHistorySection.vue": { functions: {
    "eventTitle": { complexity: 20, why: "one switch over SecurityEventType, a caption per event" },
  } },
  "src/shared/lib/bbcode/bbcodeGrammar.spec.ts": { functions: {
    "seen": { complexity: 27, why: "one walk of the preview, a branch per element it draws" },
  } },
  "src/shared/lib/bbcode/bbcodeSpelling.ts": { functions: {
    "readOpening": { complexity: 26, why: "a scanner of a tag's opening, an exit per way its spelling breaks, each with the reason it names" },
  } },
  "src/shared/lib/bbcode/bbcodeToHtml.ts": { functions: {
    "HtmlWriter.container > runs.map:paragraphs": { complexity: 21, why: "one run between two blocks, whose cases, empty, a lone break and the end of the root, each set the glue of the blocks on both sides" },
  } },
  "src/shared/lib/bbcode/bbcodeWarnings.ts": { functions: {
    "wordIssue": { complexity: 35, why: "one switch over the reasons a tag breaks, a sentence per reason as BBCODE_RENDERING.md words it" },
  } },
  "src/shared/lib/composables/useAnchoredInfiniteScroll.ts": { functions: {
    "useAnchoredInfiniteScroll": { lines: 125, why: "the two observers, older and newer, their loading flags and the suspension share one closure" },
  } },
  "src/shared/lib/composables/useApiResource.ts": { functions: {
    "useApiResource": { lines: 123, why: "one fetch whose fresh, stale and in-flight paths read the same freshness, guard and requests in flight" },
  } },
  "src/shared/lib/composables/useCodeInput.ts": { functions: {
    "useCodeInput": { lines: 114, why: "one input's value and caret, and the input and keydown handlers that rewrite them by the mode" },
  } },
  "src/shared/lib/composables/useDialogAction.ts": { functions: {
    "useDialogAction": { lines: 141, why: "the busy flag, the line and the field refusals, which run, settle and claim all write" },
  } },
  "src/shared/lib/composables/useDialogShell.ts": { functions: {
    "useDialogShell": { lines: 101, why: "the shell of an open window: the caret kept inside and given back, the page lock and the backdrop click, all tied to its open flag" },
  } },
  "src/shared/lib/composables/useDocumentTitle.spec.ts": { functions: {
    "describe \"titles across the client sources\" > it \"keeps the forbidden signs out of every composed title\"": { depth: 6 },
    "describe \"titles across the client sources\" > it \"puts the distinguishing segment first in every composition\"": { depth: 4 },
  } },
  "src/shared/lib/composables/useMessageToolbar.ts": { functions: {
    "useMessageToolbar": { lines: 118, why: "the hover and focus handlers of the messages and of the toolbar share the hovered id and the hide timer" },
  } },
  "src/shared/lib/composables/useValidatedField.ts": { functions: {
    "useValidatedField": { lines: 125, why: "one field's value under its sync and async checks, which blur, input and submit each run" },
  } },
  "src/shared/lib/copy-rules.spec.ts": { functions: {
    "describe \"interface copy\" > it \"draws an icon with an icon and not with an emoji\"": { depth: 4 },
  } },
  "src/shared/lib/testing/fontFace.ts": { functions: {
    "glyphsOf": { depth: 4 },
    "kerningOf": { depth: 6 },
  } },
  "src/shared/lib/testing/listAndPopupCopy.ts": { functions: {
    "readTemplate > visit": { complexity: 35, why: "one template walk carrying two flags, inside a popup and under a read line, which every kind of node reads and passes down" },
  } },
  "src/shared/lib/testing/markupCorpus.ts": { functions: {
    "asBothSidesRead": { complexity: 18, why: "one pass over the characters: the ranges both sides drop, then the surrogate pairs kept and the lone halves replaced" },
  } },
  "src/shared/lib/testing/markupReading.ts": { functions: {
    "inThePreview": { lines: 104, why: "its length is the walk inside it, a closure over the runs it fills and the lines it may keep" },
    "inThePreview > walk": { complexity: 27, why: "one switch over the elements the preview draws, a case per element turning it into marks" },
    "onThePage": { lines: 121, why: "its length is the walk inside it, a closure over the runs it fills" },
    "onThePage > walk": { lines: 116, complexity: 40, depth: 5, why: "one switch over the elements BbParserProvider writes, a case per element turning it into marks" },
  } },
  "src/shared/lib/testing/styles.ts": { functions: {
    "withoutWhere": { depth: 5 },
  } },
  "src/shared/lib/testing/toastScan.ts": { functions: {
    "expressionAt": { complexity: 18, why: "a character scanner: quotes skipped whole and brackets counted until the argument ends" },
    "scanOf": { lines: 181, why: "a factory whose readers follow one another, values to props to declarations, over one shared read cache, memo and list of gaps" },
    "scanOf > propValues": { depth: 5 },
  } },
  "src/shared/ui/BBCodeEditor/MarkupHelpDialog.spec.ts": { functions: {
    "describe \"the rows of the help\" > it \"keep the four cells of the table in every row, a note in its example's cell\"": { depth: 4 },
  } },
  "src/shared/ui/Form/FormField.vue": { functions: {
    "bindControl": { complexity: 19, why: "wires the slot's one control to the label, the description and the error state, and takes that off a swapped control, all off one rendered row" },
  } },
  "src/shared/ui/Layout/Dialog.spec.ts": { lines: 743, why: "the cases of one component share the vue-final-modal mock and the mounting helpers, which vi.mock ties to its file" },
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
  const depth = Math.max(entry.depth ?? 0, most("depth"));
  if (depth > 0) {
    rules["max-depth"] = ["error", Math.max(LIMITS.depth, depth)];
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
