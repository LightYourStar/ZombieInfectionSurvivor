using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Utility
{
    /// <summary>
    /// 泛型对象池，管理 MonoBehaviour 实例的复用。
    /// 初始化时可通过 <see cref="Prewarm"/> 预创建指定数量的实例，运行时通过 <see cref="Get"/> / <see cref="Return"/> 实现取出与回收。
    /// 当池空时自动扩展新实例并输出 <see cref="Debug.LogWarning(object)"/>，避免运行时 Instantiate/Destroy 导致的 GC 和卡顿。
    /// </summary>
    /// <remarks>
    /// 本池不负责预制体引用校验以外的业务状态重置，调用方在 <see cref="Get"/> 后应自行初始化实例状态，在 <see cref="Return"/> 前应清理业务状态。
    /// 池内部采用 LIFO（栈）存储空闲实例，兼顾缓存局部性与实现简洁。
    /// </remarks>
    /// <typeparam name="T">需要复用的组件类型，必须继承自 <see cref="MonoBehaviour"/></typeparam>
    public class ObjectPool<T> where T : MonoBehaviour
    {
        /// <summary>用于实例化新对象的预制体引用</summary>
        private readonly T m_prefab;

        /// <summary>池实例统一挂载的父节点，便于 Hierarchy 视图组织；可为 null（挂到场景根）</summary>
        private readonly Transform m_parent;

        /// <summary>空闲实例栈，LIFO 便于缓存局部性</summary>
        private readonly Stack<T> m_freeInstances = new Stack<T>();

        /// <summary>当前活跃（已取出尚未归还）实例数量</summary>
        private int m_activeCount;

        /// <summary>当前活跃（已取出尚未归还）实例数量</summary>
        public int ActiveCount => m_activeCount;

        /// <summary>池中空闲可复用的实例数量</summary>
        public int FreeCount => m_freeInstances.Count;

        /// <summary>
        /// 创建对象池
        /// </summary>
        /// <param name="prefab">用于实例化的预制体，必须非空</param>
        /// <param name="parent">池实例统一挂载的父节点，可为 null（挂到场景根）</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="prefab"/> 为 null 时抛出</exception>
        public ObjectPool(T prefab, Transform parent = null)
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab), "ObjectPool 的 prefab 不能为空");
            }
            m_prefab = prefab;
            m_parent = parent;
        }

        /// <summary>
        /// 预创建指定数量的实例并放入池中（已禁用状态）。
        /// 通常在游戏初始化时调用，避免运行时首次取出产生 GC 开销。
        /// </summary>
        /// <param name="count">预创建数量；小于等于 0 时不执行任何操作</param>
        public void Prewarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                T instance = CreateInstance();
                instance.gameObject.SetActive(false);
                m_freeInstances.Push(instance);
            }
        }

        /// <summary>
        /// 从池中取出一个实例并激活。
        /// 若池中无空闲实例，则动态实例化一个新对象，同时输出 <see cref="Debug.LogWarning(object)"/> 提示调用方考虑调大预热数量。
        /// </summary>
        /// <returns>已激活的实例，保证非 null</returns>
        public T Get()
        {
            T instance;
            if (m_freeInstances.Count > 0)
            {
                instance = m_freeInstances.Pop();
            }
            else
            {
                // 池空：动态扩展并提示调用方考虑调大 Prewarm 数量以避免运行时 Instantiate 开销
                Debug.LogWarning($"[ObjectPool<{typeof(T).Name}>] 池已耗尽，动态扩展新实例。建议增大 Prewarm 数量以避免运行时 Instantiate 开销。");
                instance = CreateInstance();
            }

            instance.gameObject.SetActive(true);
            m_activeCount++;
            return instance;
        }

        /// <summary>
        /// 将实例归还池中并禁用。
        /// 对 null 实例输出警告并忽略，以保证 <see cref="ActiveCount"/> 与 <see cref="FreeCount"/> 之和始终等于曾创建的实例总数。
        /// </summary>
        /// <param name="instance">需要归还的实例</param>
        public void Return(T instance)
        {
            if (instance == null)
            {
                Debug.LogWarning($"[ObjectPool<{typeof(T).Name}>] 尝试归还 null 实例，已忽略");
                return;
            }

            instance.gameObject.SetActive(false);
            m_freeInstances.Push(instance);
            // 保护性钳制，避免计数因误用降到负数
            m_activeCount = Mathf.Max(0, m_activeCount - 1);
        }

        /// <summary>
        /// 实际创建一个预制体实例并挂到指定父节点下
        /// </summary>
        private T CreateInstance()
        {
            return UnityEngine.Object.Instantiate(m_prefab, m_parent);
        }
    }
}
