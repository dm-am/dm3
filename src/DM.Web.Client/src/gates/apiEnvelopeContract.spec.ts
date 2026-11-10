import { describe, it, expect } from "vitest";
import { readFileSync } from "node:fs";
import { CLIENT_SRC, asPath, collect } from "@/shared/lib/testing/sourceTree";

/**
 * Every API client method declares the shape the server actually sends:
 * `Envelope<T>`, `ListEnvelope<T>` or `CursorEnvelope<T>` (API_DESIGN.md). The
 * axios layer hands the body over untouched, so the type argument written on
 * `Api.get<T>` is the client's only description of the answer, and a bare
 * payload where the server wraps it leaves the field the caller reads
 * undefined. The routes that answer bare are listed below, each with the
 * server method whose return type says so; a line added there is a claim about
 * src/DM.Web.API to check first.
 */

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
  "entities/blog/api/blogApi.ts:subscribe":
    "BlogUserApiService.Subscribe -> Task<BlogUser>. BlogReaderController.SubscribeToBlog answers StatusCode(201, reader).",
  "entities/blog/api/blogApi.ts:inviteAssistant":
    "BlogInvitationApiService.CreateAssistantInvitation -> Task<BlogInvitation>. The controller answers StatusCode(201, result).",
  "entities/blog/api/blogApi.ts:inviteReader":
    "BlogInvitationApiService.CreateReaderInvitation -> Task<BlogInvitation>. The controller answers StatusCode(201, result).",

  "entities/game/api/gameApi.ts:subscribe":
    "GameUserApiService.Subscribe -> Task<GameUser>. GameReaderController.SubscribeToGame answers StatusCode(201, reader).",
  "entities/game/api/gameApi.ts:invitePlayer":
    "GameInvitationApiService.InvitePlayer -> Task<GameInvitation>.",
  "entities/game/api/gameApi.ts:inviteReader":
    "GameInvitationApiService.InviteReader -> Task<GameInvitation>.",
  "entities/game/api/gameApi.ts:inviteAssistant":
    "GameInvitationApiService.InviteAssistant -> Task<GameInvitation>.",

  "entities/game/api/gameTagApi.ts:createTagGroup":
    "TagApiService.CreateGroup -> Task<TagGroup>. The controller answers CreatedAtAction with the group itself.",
  "entities/game/api/gameTagApi.ts:updateTagGroup":
    "TagApiService.UpdateGroup -> Task<TagGroup>.",
  "entities/game/api/gameTagApi.ts:createTag":
    "TagApiService.CreateTag -> Task<Tag>. The controller answers CreatedAtAction with the tag itself.",
  "entities/game/api/gameTagApi.ts:updateTag":
    "TagApiService.UpdateTag -> Task<Tag>.",
  "entities/game/api/gameTagApi.ts:getTagForEdit":
    "TagApiService.GetTag -> Task<Tag>. The controller answers Ok with the tag itself.",

  "entities/message/api/messagingApi.ts:getOrCreateDirectChat":
    "MessagingApiService.GetDirectChatAsync -> Task<Chat>.",
  "entities/message/api/messagingApi.ts:getChat":
    "MessagingApiService.GetChatAsync -> Task<Chat>.",

  "entities/moderation/api/banApi.ts:getBans":
    "BanApiService.GetPublicUserBanStatus -> Task<PublicUserBanStatus>.",
  "entities/moderation/api/moderationApi.ts:getModeratedProfile":
    "ModeratedProfileApiService.GetModeratedProfile -> Task<ModeratedProfile>.",
  "entities/moderation/api/moderationApi.ts:createModNote":
    "ModeratedProfileNoteApiService.CreateNote -> Task<ModeratedProfileNote>. Only the listing next door builds a ListEnvelope.",
  "entities/moderation/api/moderationApi.ts:updateModNote":
    "ModeratedProfileNoteApiService.UpdateNote -> Task<ModeratedProfileNote>.",
  "entities/moderation/api/warningApi.ts:getWarnings":
    "WarningApiService.GetUserWarnings -> Task<UserWarningsInfo>. The shape carries its own list inside.",

  "entities/notification/api/notificationApi.ts:getUnreadCount":
    "NotificationApiService.GetUnreadCount -> Task<UnreadCount>.",

  "entities/subscription/api/subscriptionApi.ts:subscribe":
    "SubscriptionApiService.SubscribeAsync -> Task<Subscription>.",
  "entities/subscription/api/subscriptionApi.ts:updateSettings":
    "SubscriptionApiService.UpdateSettingsAsync -> Task<Subscription>.",

  "entities/ticket/api/ticketApi.ts:createTicket":
    "TicketIntakeApiService.CreateTicket -> Task<CreateTicketIntakeResponse>. The moderation-side TicketApiService.CreateTicket is a different method and does wrap.",

  "entities/user/api/accountApi.ts:getActivationInfo":
    "IActivationApiService.GetPendingInfo -> Task<PendingInfoResponse?>.",
  "entities/user/api/accountApi.ts:checkUsername":
    "AvailabilityApiService.CheckUsernameAvailability -> Task<UsernameAvailabilityResponse>.",
  "entities/user/api/accountApi.ts:checkEmail":
    "AvailabilityApiService.CheckEmailAvailability -> Task<EmailAvailabilityResponse>.",
  "entities/user/api/accountApi.ts:recover":
    "RecoveryApiService.Recover -> Task<RecoveryResponse>.",
  "entities/user/api/accountApi.ts:signIn":
    "AuthenticationApiService.Login -> Task<LoginResponse>, and AuthenticationController.Login answers Ok(result). The second step of the same sign-in does build Envelope<LoginResponse>; the controller names the bare first step as inherited debt.",
  "entities/user/api/accountApi.ts:getPasswordResetTokenInfo":
    "RecoveryApiService.GetTokenInfo -> Task<PasswordResetTokenInfo?>.",
  "entities/user/api/accountApi.ts:changePassword":
    "CredentialsApiService.ChangePassword -> Task<User>.",
  "entities/user/api/accountApi.ts:completePasswordReset":
    "RecoveryApiService.ResetPassword -> Task<User>.",
  "entities/user/api/accountApi.ts:getUsernameChangeRequest":
    "CredentialsApiService.GetUsernameChangeStatusAsync -> Task<UsernameChangeResponse?>. Nothing pending is the null, not a 404.",
  "entities/user/api/accountApi.ts:createUsernameChangeRequest":
    "CredentialsApiService.RequestUsernameChangeAsync -> Task<UsernameChangeResponse>.",
  "entities/user/api/accountApi.ts:getUsernameChangeApproval":
    "CredentialsApiService.GetUsernameChangeApprovalAsync -> Task<UsernameChangeApprovalInfo?>.",
  "entities/user/api/accountApi.ts:completeUsernameChange":
    "CredentialsApiService.CompleteUsernameChangeAsync -> Task<UsernameChangeResponse>.",
  "entities/user/api/accountApi.ts:getNotificationPreferences":
    "NotificationApiService.GetNotificationSettings -> Task<NotificationSettings>.",
  "entities/user/api/accountApi.ts:updateNotificationPreferences":
    "NotificationApiService.UpdateNotificationSettings -> Task<NotificationSettings>.",
  "entities/user/api/accountApi.ts:generateBotCode":
    "NotificationApiService.ConnectBot -> Task<BotLinkResult>.",
  "entities/user/api/accountApi.ts:disableTwoFactor":
    "TwoFactorApiService.Disable -> Task. No body: the client declares void.",
  "entities/user/api/accountApi.ts:scheduleTwoFactorRemoval":
    "TwoFactorApiService.ScheduleRemoval -> Task. No body: the client declares void.",
  "entities/user/api/accountApi.ts:cancelTwoFactorRemoval":
    "TwoFactorApiService.CancelRemoval -> Task. No body: the client declares void.",

  "entities/user/api/blacklistApi.ts:getSettings":
    "UserBlacklistApiService.GetSettings -> Task<BlacklistSettings>.",
  "entities/user/api/blacklistApi.ts:updateSettings":
    "UserBlacklistApiService.UpdateSettings -> Task<BlacklistSettings>.",
  "entities/user/api/blacklistApi.ts:blockUser":
    "UserBlacklistApiService.BlockUser -> Task<BlacklistEntry>. Only the listing next door builds a ListEnvelope.",
  "entities/user/api/personalApi.ts:getMyProfile":
    "PersonalProfileApiService.GetMyProfile -> Task<PersonalProfile>.",
  "entities/user/api/personalApi.ts:getMyProfileForEdit":
    "PersonalProfileApiService.GetMyProfile -> Task<PersonalProfile>, the same read on the author_edit audience.",
  "entities/user/api/personalApi.ts:updateMyProfile":
    "PersonalProfileApiService.UpdateMyProfile -> Task<PersonalProfile>.",
  "entities/user/api/personalApi.ts:getMyPreferences":
    "PreferencesApiService.GetMyPreferences -> Task<Preferences>.",
  "entities/user/api/personalApi.ts:updateMyPreferences":
    "PreferencesApiService.UpdateMyPreferences -> Task<Preferences>.",

  "entities/user/api/userApi.ts:upsertUserProfileNote":
    "UserProfileNoteApiService.UpsertNote -> Task<UserProfileNote?>.",
  "entities/user/api/userApi.ts:createUserEndorsement":
    "UserEndorsementApiService.Create -> Task<UserEndorsement>.",
};

type Call = {
  /** Path relative to src/, forward slashes, plus the client method. */
  key: string;
  /** Where to look, for the message a failure prints. */
  at: string;
  /** The type argument, exactly as it is written. */
  declared: string;
};

const sourceFiles = (): string[] => collect(CLIENT_SRC, () => true);

/** Files that declare calls into the API client. */
function apiClientFiles(): string[] {
  return sourceFiles()
    .filter((file) => {
      const path = asPath(file);
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
  for (const file of sourceFiles()) {
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
        key: `${asPath(file)}:${method}`,
        at: `${asPath(file)}:${line}`,
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
    // A rule over an empty set passes: moving or renaming the API layer must
    // fail here.
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

  /** An entry left behind after its method was renamed excuses nothing. */
  it("keeps no excuse for a call that no longer exists", () => {
    const live = new Set(
      calls
        .filter((call) => !isWrapper(call.declared, table))
        .map((call) => call.key),
    );

    const stale = Object.keys(ANSWERS_BARE).filter((key) => !live.has(key));

    expect(stale).toEqual([]);
  });
});
