export * from "./module-status";
// Note: Single resources are returned directly without wrapper (per API_STANDARDS.md)
// Only collections use ListEnvelope or CursorEnvelope

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

export type Paging = {
  pages: number;
  current: number;
  size: number;
  number: number;
  total: number;
};

export type PagingQuery = {
  skip?: number;
  take?: number;
  number?: number;
};

export type ListEnvelope<T> = {
  resources: T[];
  paging: Paging | null;
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

export type GeneralError = {
  type: string;
  title: string;
  status: number;
  traceId: string;
  message?: string;
};

export enum ValidationErrorCode {
  Empty = "Empty",
  Short = "Short",
  Long = "Long",
  Taken = "Taken",
  NotFound = "NotFound",
  Invalid = "Invalid",
}

// API can return either invalidProperties (manual) or errors (FluentValidation)
export type BadRequestError = GeneralError & {
  invalidProperties?: { [field: string]: string[] };
  errors?: { [field: string]: string[] };
};

export type ApiResult<T> = {
  data: T | null;
  error: GeneralError | null;
};
