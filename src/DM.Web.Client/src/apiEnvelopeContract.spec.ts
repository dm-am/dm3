import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, dirname, relative, sep } from "node:path";
import { fileURLToPath } from "node:url";

/**
 * Every API client method declares the shape the server actually sends.
 *
 * API_DESIGN.md gives one answer shape per kind of body: `Envelope<T>` for a
 * single resource, `ListEnvelope<T>` for a collection, `CursorEnvelope<T>` for
 * a cursor page. The axios layer hands the body over untouched — taking the
 * wrapper off is the caller's job (`unwrapResource`) — so the type argument
 * written on `Api.get<T>` is the only description of the answer the client has,
 * and nothing checks it: the server is not in this compilation.
 *
 * A method declared with the bare payload where the server wraps it fails
 * silently. The field the caller reads is `undefined`, and what happens next
 * depends on the screen: a list gets a resource-shaped nothing unshifted into
 * it, an editor seeds itself from whatever it had, a card draws neither author
 * nor text. Three of these were found in the author-edit round trip (see
 * authorEditRoundTrip.spec.ts) and fourteen more in the sweep that produced
 * this file.
 *
 * The rule reads source text because the type argument is written by hand and
 * there is nothing to check it against. What it cannot know is which routes
 * genuinely answer bare — so those are listed below one by one, each with the
 * server method whose signature says so. Signatures, not
 * `[ProducesResponseType]`: the attribute is documentation and can drift, the
 * return type cannot. Controllers pass the service result straight to `Ok(...)`
 * except in the handful of places that build `new Envelope<T>(...)` themselves,
 * and those are named where they matter.
 *
 * Adding a line here is a claim about the server that has to be checked in
 * src/DM.Web.API first. Removing one is what a server fixed into contract looks
 * like.
 */

const HERE = dirname(fileURLToPath(import.meta.url));

/** A call into the shared API client, with the answer type spelled out. */
const CALL = /\bApi\.(get|post|patch|put|delete)</g;

/** The three shapes API_DESIGN.md allows a body to have. */
const WRAPPER = /^(Envelope|ListEnvelope|CursorEnvelope)</;

/** `export type X = ...`, so a named alias of a wrapper still counts as one. */
const ALIAS = /export\s+type\s+([A-Za-z0-9_]+)\s*=\s*([^;]+);/g;

/** The method a call sits in; `public`, and `async` when it awaits. */
const METHOD = /^\s*(?:public\s+)?(?:async\s+)?([A-Za-z0-9_]+)\s*\(/;

/** Not a method — the word that opens a block the formatter broke a call into. */
const KEYWORDS = ["if", "for", "while", "switch", "catch", "return"];

/**
 * Routes that answer with a bare body, and the server method that says so.
 *
 * Keyed by the file and the client method, not by a line number: the list has
 * to survive an edit above it.
 */
const ANSWERS_BARE: Record<string, string> = {
  // --- Blog -----------------------------------------------------------------
  "entities/blog/api/blogApi.ts:subscribe":
    "IBlogUserApiService.Subscribe -> Task<BlogUser>. BlogReaderController.SubscribeToBlog answers StatusCode(201, reader).",
  "entities/blog/api/blogApi.ts:inviteAssistant":
    "IBlogInvitationApiService.CreateAssistantInvitation -> Task<BlogInvitation>. The controller answers StatusCode(201, result).",
  "entities/blog/api/blogApi.ts:inviteReader":
    "IBlogInvitationApiService.CreateReaderInvitation -> Task<BlogInvitation>. The controller answers StatusCode(201, result).",

  // --- Game -----------------------------------------------------------------
  "entities/game/api/gameApi.ts:subscribe":
    "IGameUserApiService.Subscribe -> Task<GameUser>. GameReaderController.SubscribeToGame answers StatusCode(201, reader).",
  "entities/game/api/gameApi.ts:invitePlayer":
    "IGameInvitationApiService.InvitePlayer -> Task<GameInvitation>.",
  "entities/game/api/gameApi.ts:inviteReader":
    "IGameInvitationApiService.InviteReader -> Task<GameInvitation>.",
  "entities/game/api/gameApi.ts:inviteAssistant":
    "IGameInvitationApiService.InviteAssistant -> Task<GameInvitation>.",

  // --- Moderation tags ------------------------------------------------------
  "entities/game/api/gameTagApi.ts:getTagGroup":
    "ITagApiService.GetGroup -> Task<TagGroup>.",
  "entities/game/api/gameTagApi.ts:createTagGroup":
    "ITagApiService.CreateGroup -> Task<TagGroup>. The controller answers CreatedAtAction with the group itself.",
  "entities/game/api/gameTagApi.ts:updateTagGroup":
    "ITagApiService.UpdateGroup -> Task<TagGroup>.",
  "entities/game/api/gameTagApi.ts:getTag":
    "ITagApiService.GetTag -> Task<Tag>.",
  "entities/game/api/gameTagApi.ts:createTag":
    "ITagApiService.CreateTag -> Task<Tag>. The controller answers CreatedAtAction with the tag itself.",
  "entities/game/api/gameTagApi.ts:updateTag":
    "ITagApiService.UpdateTag -> Task<Tag>.",

  // --- Messaging ------------------------------------------------------------
  "entities/message/api/messagingApi.ts:canStartChat":
    "IMessagingApiService.CanStartChatAsync -> Task<ChatAvailability>.",
  "entities/message/api/messagingApi.ts:getOrCreateDirectChat":
    "IMessagingApiService.GetDirectChatAsync -> Task<Chat>.",
  "entities/message/api/messagingApi.ts:getChat":
    "IMessagingApiService.GetChatAsync -> Task<Chat>.",
  "entities/message/api/messagingApi.ts:createChat":
    "IMessagingApiService.CreateChatAsync -> Task<Chat>.",
  "entities/message/api/messagingApi.ts:updateChat":
    "IMessagingApiService.UpdateChatAsync -> Task<Chat>.",

  // --- Moderation -----------------------------------------------------------
  "entities/moderation/api/moderationApi.ts:getModeratedProfile":
    "IModeratedProfileApiService.GetModeratedProfile -> Task<ModeratedProfile>.",
  "entities/moderation/api/moderationApi.ts:createModNote":
    "IModeratedProfileNoteApiService.CreateNote -> Task<ModeratedProfileNote>. Only the listing next door builds a ListEnvelope.",
  "entities/moderation/api/moderationApi.ts:updateModNote":
    "IModeratedProfileNoteApiService.UpdateNote -> Task<ModeratedProfileNote>.",
  "entities/moderation/api/moderationApi.ts:getWarnings":
    "IWarningApiService.GetUserWarnings -> Task<UserWarningsInfo>. The shape carries its own list inside.",
  "entities/moderation/api/moderationApi.ts:getBans":
    "IBanApiService.GetPublicUserBanStatus -> Task<PublicUserBanStatus>.",

  // --- Notifications --------------------------------------------------------
  "entities/notification/api/notificationApi.ts:getUnreadCount":
    "INotificationApiService.GetUnreadCount -> Task<NotificationCount>.",

  // --- Subscriptions --------------------------------------------------------
  "entities/subscription/api/subscriptionApi.ts:getById":
    "ISubscriptionApiService.GetByIdAsync -> Task<Subscription?>.",
  "entities/subscription/api/subscriptionApi.ts:checkSubscription":
    "ISubscriptionApiService.GetSubscriptionAsync -> Task<Subscription?>.",
  "entities/subscription/api/subscriptionApi.ts:subscribe":
    "ISubscriptionApiService.SubscribeAsync -> Task<Subscription>.",
  "entities/subscription/api/subscriptionApi.ts:updateSettings":
    "ISubscriptionApiService.UpdateSettingsAsync -> Task<Subscription>.",

  // --- Tickets --------------------------------------------------------------
  "entities/ticket/api/ticketApi.ts:createTicket":
    "ITicketIntakeApiService.CreateTicket -> Task<CreateTicketIntakeResponse>. The moderation-side ITicketApiService.CreateTicket is a different method and does wrap.",

  // --- Account --------------------------------------------------------------
  "entities/user/api/accountApi.ts:register":
    "IRegistrationApiService.Register -> Task. RegistrationController.Register answers StatusCode(201) with no body at all.",
  "entities/user/api/accountApi.ts:getActivationInfo":
    "IActivationApiService.GetPendingInfo -> Task<PendingInfoResponse?>.",
  "entities/user/api/accountApi.ts:checkUsername":
    "IAvailabilityApiService.CheckUsernameAvailability -> Task<UsernameAvailabilityResponse>.",
  "entities/user/api/accountApi.ts:checkEmail":
    "IAvailabilityApiService.CheckEmailAvailability -> Task<EmailAvailabilityResponse>.",
  "entities/user/api/accountApi.ts:recover":
    "IRecoveryApiService.Recover -> Task<RecoveryResponse>.",
  "entities/user/api/accountApi.ts:signIn":
    "IAuthenticationApiService.Login -> Task<LoginResponse>, and AuthenticationController.Login answers Ok(result). The second step of the same sign-in does build Envelope<LoginResponse>; the controller names the bare first step as inherited debt.",
  "entities/user/api/accountApi.ts:getPasswordResetTokenInfo":
    "IRecoveryApiService.GetTokenInfo -> Task<PasswordResetTokenInfo?>.",
  "entities/user/api/accountApi.ts:changePassword":
    "ICredentialsApiService.ChangePassword -> Task<User>.",
  "entities/user/api/accountApi.ts:completePasswordReset":
    "IRecoveryApiService.ResetPassword -> Task<User>.",
  "entities/user/api/accountApi.ts:changeEmail":
    "ICredentialsApiService.RequestEmailChange -> Task<User>.",
  "entities/user/api/accountApi.ts:getUsernameChangeRequest":
    "ICredentialsApiService.GetUsernameChangeStatusAsync -> Task<UsernameChangeResponse?>. Nothing pending is the null, not a 404.",
  "entities/user/api/accountApi.ts:createUsernameChangeRequest":
    "ICredentialsApiService.RequestUsernameChangeAsync -> Task<UsernameChangeResponse>.",
  "entities/user/api/accountApi.ts:getUsernameChangeApproval":
    "ICredentialsApiService.GetUsernameChangeApprovalAsync -> Task<UsernameChangeApprovalInfo?>.",
  "entities/user/api/accountApi.ts:completeUsernameChange":
    "ICredentialsApiService.CompleteUsernameChangeAsync -> Task<UsernameChangeResponse>.",
  "entities/user/api/accountApi.ts:getNotificationPreferences":
    "INotificationApiService.GetNotificationSettings -> Task<NotificationSettings>.",
  "entities/user/api/accountApi.ts:updateNotificationPreferences":
    "INotificationApiService.UpdateNotificationSettings -> Task<NotificationSettings>.",
  "entities/user/api/accountApi.ts:generateBotCode":
    "INotificationApiService.ConnectBot -> Task<BotLinkResult>.",
  "entities/user/api/accountApi.ts:disableTwoFactor":
    "ITwoFactorApiService.Disable -> Task. No body: the client declares void.",
  "entities/user/api/accountApi.ts:requestTwoFactorRemoval":
    "ITwoFactorApiService.RequestRemoval -> Task. No body: the client declares void.",
  "entities/user/api/accountApi.ts:scheduleTwoFactorRemoval":
    "ITwoFactorApiService.ScheduleRemoval -> Task. No body: the client declares void.",
  "entities/user/api/accountApi.ts:cancelTwoFactorRemoval":
    "ITwoFactorApiService.CancelRemoval -> Task. No body: the client declares void.",

  // --- Personal -------------------------------------------------------------
  "entities/user/api/blacklistApi.ts:getSettings":
    "IUserBlacklistApiService.GetSettings -> Task<BlacklistSettings>.",
  "entities/user/api/blacklistApi.ts:updateSettings":
    "IUserBlacklistApiService.UpdateSettings -> Task<BlacklistSettings>.",
  "entities/user/api/blacklistApi.ts:blockUser":
    "IUserBlacklistApiService.BlockUser -> Task<BlacklistEntry>. Only the listing next door builds a ListEnvelope.",
  "entities/user/api/personalApi.ts:getMyProfile":
    "IPersonalProfileApiService.GetMyProfile -> Task<PersonalProfile>.",
  "entities/user/api/personalApi.ts:updateMyProfile":
    "IPersonalProfileApiService.UpdateMyProfile -> Task<PersonalProfile>.",
  "entities/user/api/personalApi.ts:getMyPreferences":
    "IPreferencesApiService.GetMyPreferences -> Task<Preferences>.",
  "entities/user/api/personalApi.ts:updateMyPreferences":
    "IPreferencesApiService.UpdateMyPreferences -> Task<Preferences>.",

  // --- Community ------------------------------------------------------------
  "entities/user/api/userApi.ts:getUserProfileNote":
    "IUserProfileNoteApiService.GetNote -> Task<UserProfileNote?>.",
  "entities/user/api/userApi.ts:upsertUserProfileNote":
    "IUserProfileNoteApiService.UpsertNote -> Task<UserProfileNote?>.",
  "entities/user/api/userApi.ts:createUserEndorsement":
    "IUserEndorsementApiService.Create -> Task<UserEndorsement>.",
  "entities/user/api/userApi.ts:updateUserEndorsement":
    "IUserEndorsementApiService.Update -> Task<UserEndorsement>.",
};

type Call = {
  /** Path relative to src/, forward slashes, plus the client method. */
  key: string;
  /** Where to look, for the message a failure prints. */
  at: string;
  /** The type argument, exactly as it is written. */
  declared: string;
};

function sourceFiles(dir: string, out: string[] = []): string[] {
  for (const entry of readdirSync(dir)) {
    if (entry === "node_modules" || entry === "dist" || entry === "coverage")
      continue;
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) sourceFiles(full, out);
    else out.push(full);
  }
  return out;
}

const rel = (file: string) => relative(HERE, file).split(sep).join("/");

/** Files that declare calls into the API client. */
function apiClientFiles(): string[] {
  return sourceFiles(HERE)
    .filter((file) => {
      const path = rel(file);
      return (
        path.endsWith(".ts") &&
        !path.endsWith(".spec.ts") &&
        /(^|\/)api\/[^/]+$/.test(path)
      );
    })
    .sort();
}

/**
 * `export type X = Envelope<Y>` and the like, from anywhere under src.
 *
 * A named alias of a wrapper is a wrapper: the achievements module declares a
 * dozen of them and every one of its calls is honest.
 */
function aliasTable(): Map<string, string> {
  const table = new Map<string, string>();
  for (const file of sourceFiles(HERE)) {
    if (!file.endsWith(".ts") || file.endsWith(".spec.ts")) continue;
    const text = readFileSync(file, "utf8");
    let found: RegExpExecArray | null;
    ALIAS.lastIndex = 0;
    while ((found = ALIAS.exec(text)))
      table.set(found[1], found[2].replace(/\s+/g, " ").trim());
  }
  return table;
}

function isWrapper(declared: string, table: Map<string, string>): boolean {
  const seen = new Set<string>();
  let type = declared.trim();
  while (!seen.has(type)) {
    if (WRAPPER.test(type)) return true;
    const name = /^([A-Za-z0-9_]+)$/.exec(type);
    if (!name) return false;
    seen.add(type);
    const next = table.get(name[1]);
    if (!next) return false;
    type = next;
  }
  return false;
}

/** Every `Api.verb<Type>(` in the API client layer, type argument included. */
function apiCalls(): Call[] {
  const calls: Call[] = [];

  for (const file of apiClientFiles()) {
    const text = readFileSync(file, "utf8");
    const lines = text.split("\n");
    let found: RegExpExecArray | null;
    CALL.lastIndex = 0;

    while ((found = CALL.exec(text))) {
      // The type argument may hold angle brackets of its own
      // (`ListEnvelope<Comment>`), so it is read by balance, not by regex.
      let depth = 0;
      let at = found.index + found[0].length - 1;
      const opens = at + 1;
      for (; at < text.length; at++) {
        if (text[at] === "<") depth++;
        else if (text[at] === ">" && --depth === 0) break;
      }
      const declared = text.slice(opens, at).replace(/\s+/g, " ").trim();
      const line = text.slice(0, found.index).split("\n").length;

      // The method above the call names the route for a reader; the line
      // number would not survive an edit anywhere above it.
      let method = "?";
      for (let i = line - 1; i >= 0; i--) {
        const declaredHere = METHOD.exec(lines[i]);
        if (declaredHere && !KEYWORDS.includes(declaredHere[1])) {
          method = declaredHere[1];
          break;
        }
      }

      calls.push({
        key: rel(file) + ":" + method,
        at: rel(file) + ":" + line,
        declared,
      });
    }
  }

  return calls;
}

describe("API answer shapes", () => {
  const calls = apiCalls();
  const table = aliasTable();

  it("finds the API client calls by walking the tree", () => {
    // A rule over an empty set passes. Moving the API layer, renaming the
    // client or breaking the walk must fail here rather than go green over
    // nothing.
    expect(calls.length).toBeGreaterThanOrEqual(250);
  });

  it("declares an envelope everywhere the server sends one", () => {
    const offenders = calls
      .filter((call) => !isWrapper(call.declared, table))
      .filter((call) => !(call.key in ANSWERS_BARE))
      .map(
        (call) => `${call.at} -> ${call.key.split(":")[1]}: ${call.declared}`,
      );

    expect(offenders).toEqual([]);
  });

  /**
   * And the list of the ones that do not is kept, not accumulated.
   *
   * An entry left behind after its method was renamed or its route brought
   * into contract excuses nothing and reads as if it did. It is also the way
   * the list rots into a wall of text nobody re-checks against the server.
   */
  it("keeps no excuse for a call that no longer exists", () => {
    const live = new Set(
      calls
        .filter((call) => !isWrapper(call.declared, table))
        .map((call) => call.key),
    );

    const stale = Object.keys(ANSWERS_BARE).filter((key) => !live.has(key));

    expect(stale).toEqual([]);
  });

  it("gives every excuse a reason", () => {
    const empty = Object.entries(ANSWERS_BARE)
      .filter(([, reason]) => reason.trim().length < 20)
      .map(([key]) => key);

    expect(empty).toEqual([]);
  });
});
