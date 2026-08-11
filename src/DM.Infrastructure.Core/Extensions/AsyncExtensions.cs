using System.Threading.Tasks;

namespace DM.Infrastructure.Core.Extensions;

/// <summary>
/// Some async extensions
/// </summary>
public static class AsyncExtensions
{
    /// <summary>
    /// Typed results for parallel task execution
    /// </summary>
    /// <returns>Tuple of task results</returns>
    public static async Task<(T1, T2)> WhenAll<T1, T2>(Task<T1> task1, Task<T2> task2)
    {
        await Task.WhenAll(task1, task2).ConfigureAwait(false);
        return (await task1, await task2);
    }

    /// <summary>
    /// Typed results for parallel task execution
    /// </summary>
    /// <returns>Tuple of task results</returns>
    public static async Task<(T1, T2, T3)> WhenAll<T1, T2, T3>(Task<T1> task1, Task<T2> task2, Task<T3> task3)
    {
        await Task.WhenAll(task1, task2, task3).ConfigureAwait(false);
        return (await task1, await task2, await task3);
    }

    /// <summary>
    /// Typed results for parallel task execution
    /// </summary>
    /// <returns>Tuple of task results</returns>
    public static async Task<(T1, T2, T3, T4)> WhenAll<T1, T2, T3, T4>(Task<T1> task1, Task<T2> task2, Task<T3> task3,
        Task<T4> task4)
    {
        await Task.WhenAll(task1, task2, task3, task4).ConfigureAwait(false);
        return (await task1, await task2, await task3, await task4);
    }
}
