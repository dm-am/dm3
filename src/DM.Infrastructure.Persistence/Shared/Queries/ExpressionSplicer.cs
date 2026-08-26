using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace DM.Infrastructure.Persistence.Shared.Queries;

/// <summary>
/// Inlines a named lambda into a bigger projection expression.
///
/// EF translates only what it can see: a shared rule stored as an
/// <see cref="Expression{TDelegate}"/> (the pendency selection is the case
/// in point) cannot be called from inside another query lambda, and writing
/// its text a second time is how two answers to one question drift apart.
/// The splicer keeps the single text: the projection lambda marks the spot
/// with <see cref="Splice{T,TResult}"/>, and <see cref="Expand{TDelegate}"/>
/// rewrites the marker into the rule's body with the argument substituted.
/// Expand runs whenever the projection is composed - some call sites cache
/// the result in a static field, others expand on every query build. The
/// repeated expansions produce structurally identical trees, so EF's query
/// cache recognises them as the same query and compiles it once.
/// </summary>
internal static class ExpressionSplicer
{
    /// <summary>
    /// Marker for the spot where <paramref name="expression"/>'s body is
    /// inlined with <paramref name="argument"/> substituted for its
    /// parameter. Never executed - the projection must go through
    /// <see cref="Expand{TDelegate}"/> before anything invokes it.
    /// </summary>
    public static TResult Splice<T, TResult>(this Expression<Func<T, TResult>> expression, T argument) =>
        throw new InvalidOperationException(
            "Splice is a rewrite marker; run the expression through ExpressionSplicer.Expand first.");

    /// <summary>
    /// Rewrites every <see cref="Splice{T,TResult}"/> marker in the lambda
    /// into the referenced expression's body.
    /// </summary>
    public static Expression<TDelegate> Expand<TDelegate>(Expression<TDelegate> lambda) =>
        (Expression<TDelegate>)new SpliceRewriter().Visit(lambda);

    /// <summary>
    /// <c>Select</c> with the selector expanded first.
    /// </summary>
    /// <remarks>
    /// Both type arguments are inferred - the source from the query, the result
    /// from the selector - which is the only way a projection into an ANONYMOUS
    /// type can splice anything: <see cref="Expand{TDelegate}"/> needs the
    /// delegate type written out, and an anonymous type has no name to write.
    /// Without this, a query shaped by a join had to copy by hand whatever
    /// formula the named projections share.
    /// </remarks>
    public static IQueryable<TResult> SelectSpliced<TSource, TResult>(
        this IQueryable<TSource> query,
        Expression<Func<TSource, TResult>> selector) =>
        query.Select(Expand(selector));

    /// <summary>
    /// Builds the formula for a derived target out of the base target's: one
    /// member-init over <typeparamref name="TTier"/> carrying the bindings of
    /// <paramref name="baseFields"/> first, rebased onto the tier lambda's
    /// parameter, and the tier's own bindings after them. The shared member
    /// list stays a single text; a tier only appends to it.
    /// </summary>
    /// <remarks>
    /// A binding whose member is declared on the base type is legal inside a
    /// member-init over the derived one, and the provider reads the binding
    /// EXPRESSIONS - the member only says where to put the value - so the
    /// merged formula asks for the same columns the two hand-written copies
    /// asked for. Splice markers inside the base bindings survive the merge and
    /// are expanded by <see cref="Expand{TDelegate}"/> at the projection root,
    /// exactly as they were before it.
    ///
    /// A second copy of a member list is not a specialisation: it is the same
    /// answer written twice, and the two drift the moment one target grows a
    /// member. That happened here before the merge existed - "the two blocks
    /// must stay identical member for member" was a comment, not a check.
    /// </remarks>
    public static Expression<Func<TSource, TTier>> WithBaseBindings<TSource, TBase, TTier>(
        Expression<Func<TSource, TBase>> baseFields,
        Expression<Func<TSource, TTier>> tierFields)
        where TTier : TBase
    {
        var baseInit = (MemberInitExpression)baseFields.Body;
        // A tier that adds no members of its own is written `t => new Tier()`,
        // and the compiler emits a bare New for that rather than an empty
        // member-init. Both are the same starting point here.
        var tierInit = tierFields.Body as MemberInitExpression;
        var tierNew = tierInit?.NewExpression ?? (NewExpression)tierFields.Body;
        var tierBindings = tierInit?.Bindings ?? (IEnumerable<MemberBinding>)[];
        var parameter = tierFields.Parameters[0];
        var rebase = new ParameterRewriter(baseFields.Parameters[0], parameter);
        var baseBindings = baseInit.Bindings
            .Cast<MemberAssignment>()
            .Select(binding => binding.Update(rebase.Visit(binding.Expression)));
        return Expression.Lambda<Func<TSource, TTier>>(
            Expression.MemberInit(tierNew, baseBindings.Concat(tierBindings)),
            parameter);
    }

    private sealed class SpliceRewriter : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType != typeof(ExpressionSplicer) ||
                node.Method.Name != nameof(Splice))
            {
                return base.VisitMethodCall(node);
            }

            var spliced = EvaluateLambdaArgument(node.Arguments[0]);
            var argument = Visit(node.Arguments[1]);
            var body = new ParameterRewriter(spliced.Parameters[0], argument).Visit(spliced.Body);
            return Visit(body);
        }

        private static LambdaExpression EvaluateLambdaArgument(Expression argument)
        {
            // The rule arrives as a reference to a static readonly field
            // (or property); read it instead of compiling a throwaway
            // delegate for every expansion.
            if (argument is MemberExpression { Expression: null } member)
            {
                var value = member.Member switch
                {
                    FieldInfo field => field.GetValue(null),
                    PropertyInfo property => property.GetValue(null),
                    _ => null
                };

                if (value is LambdaExpression lambda)
                {
                    return lambda;
                }
            }

            if (argument is ConstantExpression { Value: LambdaExpression constant })
            {
                return constant;
            }

            throw new InvalidOperationException(
                "Splice expects the rule as a static field, property or constant lambda.");
        }
    }

    /// <summary>
    /// Replaces one lambda parameter with an arbitrary expression. Shared
    /// with the binding-merge in GeneralUserProjections, which rebases the
    /// row formula onto the tier lambda's parameter.
    /// </summary>
    /// <remarks>
    /// The constructor is internal (not primary, which is always public) so the
    /// blanket assembly scan leaves the type alone: nothing in the container
    /// answers for a ParameterExpression.
    /// </remarks>
    internal sealed class ParameterRewriter : ExpressionVisitor
    {
        private readonly ParameterExpression _parameter;
        private readonly Expression _replacement;

        internal ParameterRewriter(ParameterExpression parameter, Expression replacement)
        {
            _parameter = parameter;
            _replacement = replacement;
        }

        protected override Expression VisitParameter(ParameterExpression node) =>
            node == _parameter ? _replacement : base.VisitParameter(node);
    }
}
