using System.Collections.Generic;

namespace FindSimilarTest
{
    public static class TestExtensions
    {
        public static IEnumerable<T> Flatten<T>(this T[,] items)
        {
            for (int i = 0; i < items.GetLength(0); i++)
                for (int j = 0; j < items.GetLength(1); j++)
                    yield return items[i, j];
        }

    }
}