import Api from "./client";

export { Api };

export { setSessionExpiredHandler, type RequestOptions } from "./interceptor";
export { RENDER_AUDIENCE } from "./audience";
// Token-gated endpoints: the credential travels in a header, never in the URL.
export { X_DM_ACCOUNT_TOKEN, X_DM_TICKET_TOKEN } from "./tokenHeaders";
export { unwrapResource } from "./envelope";
// Upload is transport with two consumers in different domains, so its client
// stays here while every domain client lives in its entity slice.
export { default as uploadApi } from "./uploadApi";
export { notepadEndpoints } from "./notepadEndpoints";
export { toCommentsQueryParams, type CommentsQuery } from "./commentsQuery";
export * from "./models";
