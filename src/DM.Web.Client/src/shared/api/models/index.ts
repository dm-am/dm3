// Branded type
type InternalServed<T> = T & { __type: T };

// Idempotent generics (Served<number> = Served<Served<number>>)
//
// A null stays null. The brand is an intersection, and `null & { __type }` is
// never, so Served<User | null> used to read as a plain Served<User>: the null
// the mirror declared was erased, and a reader of a removed author's comment
// was typed as if the author were always there.
export type Served<T> = T extends null
  ? null
  : T extends InternalServed<infer U>
    ? InternalServed<U>
    : InternalServed<T>;

type IfEquals<X, Y, A, B> =
  (<T>() => T extends X ? 1 : 2) extends <T>() => T extends Y ? 1 : 2 ? A : B;

export type Id<T> = Served<T>;

export type Post<T> = {
  [P in keyof T as IfEquals<T[P], Served<T[P]>, never, P>]: T[P];
};

export type Patch<T> = {
  [P in keyof T as IfEquals<T[P], Served<T[P]>, never, P>]?: T[P];
};
