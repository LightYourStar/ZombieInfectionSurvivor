using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Utility
{
    /// <summary>
    /// 游戏数学工具类，集中提供基于 sqrMagnitude 的距离判定和最近目标查找等高频操作。
    /// 所有方法均为静态且无状态，调用方可安全并发使用。
    /// </summary>
    /// <remarks>
    /// 为避免 <see cref="Mathf.Sqrt(float)"/> 开销，所有涉及距离比较的判定均基于平方距离完成。
    /// 感染/感知等判定统一采用严格小于（<c>&lt;</c>）语义：当两点距离恰好等于半径时，视为不在范围内，以匹配
    /// 设计文档 Property 7（distance &lt; R 当且仅当 IsWithinRange 返回 true）。
    /// </remarks>
    public static class MathUtils
    {
        /// <summary>
        /// 判定两点之间距离是否严格小于指定半径。
        /// 等价于 <c>Vector2.Distance(a, b) &lt; radius</c>，但使用平方距离避免开方开销。
        /// </summary>
        /// <param name="a">第一个点</param>
        /// <param name="b">第二个点</param>
        /// <param name="radius">判定半径；负数视为 0（永远返回 false）</param>
        /// <returns>当两点距离严格小于 <paramref name="radius"/> 时返回 true，否则 false</returns>
        public static bool IsWithinRange(Vector2 a, Vector2 b, float radius)
        {
            // 负半径不具备几何意义，统一视为不在范围内
            if (radius <= 0f)
            {
                return false;
            }

            float sqrDist = (b - a).sqrMagnitude;
            float sqrRadius = radius * radius;
            return sqrDist < sqrRadius;
        }

        /// <summary>
        /// 计算两点之间的平方距离。
        /// 当调用方只需比较大小（例如排序、挑选最近目标）时，应优先使用平方距离以避免 <see cref="Mathf.Sqrt(float)"/> 开销。
        /// </summary>
        /// <param name="a">第一个点</param>
        /// <param name="b">第二个点</param>
        /// <returns>两点平方距离，始终 &gt;= 0</returns>
        public static float SqrDistance(Vector2 a, Vector2 b)
        {
            return (b - a).sqrMagnitude;
        }

        /// <summary>
        /// 在候选集合中查找距离原点最近的元素。
        /// 使用平方距离比较，避免 <see cref="Mathf.Sqrt(float)"/> 开销。
        /// </summary>
        /// <typeparam name="T">候选元素类型</typeparam>
        /// <param name="origin">参考原点</param>
        /// <param name="candidates">候选集合；为 null 或空时返回 <c>default(T)</c></param>
        /// <param name="getPosition">获取候选元素二维位置的委托，必须非空</param>
        /// <returns>距离 <paramref name="origin"/> 最近的候选元素；集合为空时返回 <c>default(T)</c></returns>
        /// <exception cref="ArgumentNullException">当 <paramref name="getPosition"/> 为空时抛出</exception>
        public static T FindNearest<T>(Vector2 origin, IList<T> candidates, Func<T, Vector2> getPosition)
        {
            if (getPosition == null)
            {
                throw new ArgumentNullException(nameof(getPosition), "FindNearest 的位置获取委托不能为空");
            }

            if (candidates == null || candidates.Count == 0)
            {
                return default;
            }

            T nearest = default;
            float minSqrDist = float.PositiveInfinity;
            bool found = false;

            // 使用 for 循环避免 IEnumerable 迭代器的 GC 分配
            for (int i = 0; i < candidates.Count; i++)
            {
                T candidate = candidates[i];
                // 跳过 null 引用类型，避免调用方持有已回收对象时崩溃
                if (candidate == null)
                {
                    continue;
                }

                float sqrDist = (getPosition(candidate) - origin).sqrMagnitude;
                if (sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    nearest = candidate;
                    found = true;
                }
            }

            return found ? nearest : default;
        }
    }
}
