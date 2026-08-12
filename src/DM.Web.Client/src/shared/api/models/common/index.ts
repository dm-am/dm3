export * from "./module-status";

// Re-export branded types from parent index
export { type Id, type Served, type Post, type Patch } from "../index";

// Base user types (for FSD compliance - entities import from shared)
export * from "./user";

// Base comment types (for FSD compliance - entities import from shared)
export * from "./comment";

// Base message types (for FSD compliance - entities import from shared)
export * from "./message";

// Shared ID types (for FSD compliance - shared modules use these instead of entity imports)
export * from "./ids";

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

// Single resource envelope (for consistency with list envelopes)
export type Envelope<T> = {
  resource: T;
};

/**
 * RFC 9457 problem document. Every failure the API produces has this shape:
 * ErrorHandlingMiddleware builds them all through ProblemDetailsFactory and
 * answers application/problem+json.
 *
 * The server's message is the title. There used to be a "message" field here
 * too, copied from a DTO that no code path ever wrote to the wire, and five
 * dialogs read it — so every one of them always showed its fallback string
 * instead of what the server said.
 */
export type GeneralError = {
  type: string;
  title: string;
  status: number;
  traceId: string;
};

export enum ValidationErrorCode {
  Empty = "Empty",
  Short = "Short",
  Long = "Long",
  Taken = "Taken",
  NotFound = "NotFound",
  Invalid = "Invalid",
}

/**
 * A 400 with field-level detail. "errors" is what
 * ProblemDetailsFactory.CreateValidationProblemDetails writes, and it is the
 * only shape on the wire; "invalidProperties" belonged to the hand-built DTO
 * that has been removed.
 */
export type BadRequestError = GeneralError & {
  errors?: { [field: string]: string[] };
};

export type ApiResult<T> = {
  data: T | null;
  error: GeneralError | null;
};
