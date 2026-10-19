export * from "./module-status";

export { type Served } from "../index";

// For FSD: entities import base types from shared.
export * from "./user";

export * from "./comment";

export * from "./message";

export type PagingInfo = {
  pages: number;
  current: number;
  skip: number;
  take: number;
  total: number;
};

export type PagingQuery = {
  skip?: number;
  take?: number;
  number?: number;
};

export type ListEnvelope<T> = {
  resources: T[];
  paging: PagingInfo | null;
};

export type CursorPaging = {
  nextCursor: string | null;
  prevCursor: string | null;
  hasNext: boolean;
  hasPrev: boolean;
};

export type CursorEnvelope<T> = {
  resources: T[];
  paging: CursorPaging;
};

export type Envelope<T> = {
  resource: T;
};

/**
 * RFC 9457 problem document, the shape of every failure the API produces. The
 * server's message is the title.
 */
export type GeneralError = {
  type: string;
  title: string;
  status: number;
  traceId: string;
  /**
   * Set by the server on a refusal that names no reason of its own: the
   * authorization check and an unhandled error. The title is then the general
   * one, and the plate says it with the call's phrase.
   */
  general?: boolean;
  /**
   * Whole seconds to wait: a 429 states it in Retry-After, which the transport
   * copies here; the lockout 403 writes it into the document itself.
   */
  retryAfterSeconds?: number;
};

/** What a call handed to a component answers: the failure, if there was one. */
export type SubmitResult = { error: GeneralError | null };

/** A 400 with field-level detail, as CreateValidationProblemDetails writes it. */
export type BadRequestError = GeneralError & {
  errors?: { [field: string]: string[] };
};

export type ApiResult<T> = {
  data: T | null;
  error: GeneralError | null;
};
