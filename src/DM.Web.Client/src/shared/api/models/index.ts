type InternalServed<T> = T & { __type: T };

// Idempotent: Served<number> = Served<Served<number>>. A null stays null, since
// null & { __type } is never and would erase it.
export type Served<T> = T extends null
  ? null
  : T extends InternalServed<infer U>
    ? InternalServed<U>
    : InternalServed<T>;

/**
 * A value taken for what the API serves: the like the client has just added,
 * an answer drawn as a comment, the data of a spec. The brand has no value of
 * its own to check, so this is the one cast that makes it.
 */
export function asServed<T>(value: T): Served<T> {
  return value as Served<T>;
}

type IfEquals<X, Y, A, B> =
  (<T>() => T extends X ? 1 : 2) extends <T>() => T extends Y ? 1 : 2 ? A : B;

export type Post<T> = {
  [P in keyof T as IfEquals<T[P], Served<T[P]>, never, P>]: T[P];
};

export type Patch<T> = {
  [P in keyof T as IfEquals<T[P], Served<T[P]>, never, P>]?: T[P];
};
